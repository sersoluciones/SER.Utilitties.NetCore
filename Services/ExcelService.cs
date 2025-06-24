using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using OfficeOpenXml.Table;
using SER.Utilitties.NetCore.Configuration;
using SER.Utilitties.NetCore.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SER.Utilitties.NetCore.Services
{
    public class ExcelService
    {
        private readonly ILogger _logger;
        private IConfiguration _config;
        private readonly IHttpContextAccessor _contextAccessor;
        private readonly IOptionsMonitor<SERRestOptions> _optionsDelegate;


        public ExcelService(
            ILogger<ExcelService> logger,
            IHttpContextAccessor httpContextAccessor,
            IOptionsMonitor<SERRestOptions> optionsDelegate,
            IConfiguration config)
        {
            _logger = logger;
            _config = config;
            _contextAccessor = httpContextAccessor;
            _optionsDelegate = optionsDelegate;

        }

        private string Pagination(string query, out PagedResultBase result,
        Dictionary<string, object> Params)
        {
            result = new PagedResultBase();
            StringBuilder st = new StringBuilder();
            var ParamsPagination = new Dictionary<string, object>();
            int count = Params == null ? 0 : Params.Count;
            // Pagination
            if (int.TryParse(_contextAccessor.HttpContext.Request.Query.FirstOrDefault(x => x.Key.Equals("page")).Value.ToString(), out int pageNumber))
            {
                var pageSizeRequest = _contextAccessor.HttpContext.Request.Query.FirstOrDefault(x => x.Key.Equals("take")).Value;

                int pageSize = string.IsNullOrEmpty(pageSizeRequest) ? 20 : int.Parse(pageSizeRequest);
                pageNumber = pageNumber == 0 ? 1 : pageNumber;

                for (int i = 0; i < 2; i++)
                {
                    var param = $"@P_{count + i}_";
                    if (i == 0)
                    {
                        st.Append("LIMIT ");
                        st.Append(param);
                        ParamsPagination.Add(param, pageSize);
                    }
                    else
                    {
                        st.Append(" OFFSET ");
                        st.Append(param);
                        var value = (pageNumber * pageSize) - pageSize;
                        ParamsPagination.Add(param, value);
                    }
                }

                result.current_page = pageNumber;
                result.page_size = pageSize;
                result.row_count = GetCountDBAsync(query, Params).Result;

                var pageCount = (double)result.row_count / pageSize;
                result.page_count = (int)Math.Ceiling(pageCount);

                foreach (var param in ParamsPagination)
                    Params.Add(param.Key, param.Value);

                return st.ToString();
            }
            return string.Empty;
        }

        public async Task<int> GetCountDBAsync(string query, Dictionary<string, object> Params = null)
        {
            string SqlConnectionStr = _optionsDelegate.CurrentValue.ConnectionString;
            string Query = @"select count(*) from ( " + query + " ) as p";
            Stopwatch sw = new Stopwatch();

            using (NpgsqlConnection _conn = new NpgsqlConnection(SqlConnectionStr))
            {
                try
                {
                    _conn.Open();
                    sw.Start();
                    using var cmd = _conn.CreateCommand();
                    cmd.CommandText = Query;
                    cmd.CommandTimeout = 120;
                    cmd.Parameters.AddRange(cmd.SetSqlParamsPsqlSQL(Params, _logger));
                    _logger.LogInformation($"Executed DbCommand [Parameters=[{PostgresQLService.ParamsToString(cmd.Parameters.ToArray())}], " +
                        $"CommandType={cmd.CommandType}, CommandTimeout='{cmd.CommandTimeout}']\n" +
                        $"      Query\n      {cmd.CommandText}");
                    return int.Parse((await cmd.ExecuteScalarAsync()).ToString());
                }
                catch (Exception ex)
                {
                    _logger.LogCritical("Error {0} {1} {2}\n{3}", ex.Message, ex.StackTrace, ex.Data, ex.InnerException);
                }
                finally
                {
                    //_conn.Close();
                    sw.Stop();
                    //_logger.LogDebug($"Closing connection to database {_context.GetType().name}");
                    _logger.LogDebug($"Executed DbCommand Time total: {sw.Elapsed} ({sw.Elapsed.Milliseconds}ms)");
                }
            }
            return 0;
        }

        public async Task<object> GetDataFromPsqlDB(PostgresQLService postgresService, string modelName,
           string columnStr = "", string orderBy = "", string parameters = "", bool download = false, string parametersExcept = "")
        {
            if (!string.IsNullOrEmpty(columnStr))
            {
                string[] columns = columnStr.Split(',');
                columnStr = string.Join("\",\"", columns);
                columnStr = string.Format("\"{0}\"", columnStr).Replace(" ", string.Empty);
            }
            else
            {
                columnStr = "*";
            }

            StringBuilder sb = new();
            PagedResultBase pageResult = null;

            string Query = $"SELECT {columnStr} FROM \"{modelName}\"";
            var Params = new Dictionary<string, object>();
            int index = 0;

            if (!string.IsNullOrEmpty(parameters))
            {
                var jObjParams = JsonExtensions.ToJsonDocument(parameters);
                var props = jObjParams.EnumerateObject();
                while (props.MoveNext())
                {
                    var pair = props.Current;
                    string propertyName = pair.Name;
                    var propType = pair.Value.GetType();
                    var evaluate = " = ";

                    if (bool.TryParse(pair.Value.ToString().Trim(), out bool @bool))
                        Params.Add(string.Format("@{0}", propertyName), pair.Value.GetBoolean());
                    else if (int.TryParse(pair.Value.ToString().Trim(), out int @int))
                        Params.Add(string.Format("@{0}", propertyName), @int);
                    else if (double.TryParse(pair.Value.ToString().Trim(), out double @double))
                        Params.Add(string.Format("@{0}", propertyName), @double);
                    else
                    {
                        Params.Add(string.Format("@{0}", propertyName), $"%{pair.Value.ToString().ToLower().Trim()}%");
                        evaluate = " ilike ";
                    }

                    if (index == 0)
                        Query = string.Format(@"{0} WHERE ""{2}"" {1} @{2}", Query, evaluate, propertyName);
                    else
                        Query = string.Format(@"{0} AND ""{2}"" {1} @{2}", Query, evaluate, propertyName);

                    index++;
                }
            }

            if (!string.IsNullOrEmpty(parametersExcept))
            {
                var jObjParams = JsonExtensions.ToJsonDocument(parametersExcept);
                var props = jObjParams.EnumerateObject();
                while (props.MoveNext())
                {
                    var pair = props.Current;
                    string propertyName = pair.Name;
                    var propType = pair.Value.GetType();
                    var evaluate = " <> ";

                    if (bool.TryParse(pair.Value.ToString().Trim(), out bool @bool))
                        Params.Add(string.Format("@{0}", propertyName), pair.Value.GetBoolean());
                    else if (int.TryParse(pair.Value.ToString().Trim(), out int @int))
                        Params.Add(string.Format("@{0}", propertyName), @int);
                    else if (double.TryParse(pair.Value.ToString().Trim(), out double @double))
                        Params.Add(string.Format("@{0}", propertyName), @double);
                    else
                        Params.Add(string.Format("@{0}", propertyName), pair.Value.ToString().Trim());

                    if (index == 0)
                        Query = string.Format(@"{0} WHERE ""{2}"" {1} @{2}", Query, evaluate, propertyName);
                    else
                        Query = string.Format(@"{0} AND ""{2}"" {1} @{2}", Query, evaluate, propertyName);

                    index++;
                }
            }

            if (!string.IsNullOrEmpty(orderBy))
            {
                Query = string.Format("{0} ORDER BY \"{1}\"", Query, orderBy);
            }

            // Pagination
            if (_contextAccessor.HttpContext.Request.Query.Any(x => x.Key.Equals("page")))
            {
                if (Params == null) Params = new Dictionary<string, object>();
                var paginate = Pagination(Query, out pageResult, Params);

                if (!string.IsNullOrEmpty(paginate))
                    Query = string.Format("{0}\n{1}", Query, paginate);

                if (pageResult != null)
                {
                    sb.Append(JsonSerializer.Serialize(pageResult));
                    sb.Replace("}", ",", sb.Length - 2, 2);
                    sb.Append("\n\"results\": ");
                }
            }

            var result = await postgresService.GetDataFromDBAsync(Query, Params: Params.Count == 0 ? null : Params);

            if (download)
            {
                return GenerateExcel(result, modelName);
            }

            if (!string.IsNullOrEmpty(result))
            {
                if (pageResult != null)
                {
                    sb.Append(result);
                    sb.Append("}");
                    return sb.ToString();
                }
                return result;
            }

            return string.Empty;
        }

        public static dynamic GenerateExcel(string results, string modelName, Dictionary<string, string> dict = null, bool returnBytes = false)
            => GenerateExcel<object>(results, modelName, dict: dict, returnBytes: returnBytes);

        public static dynamic GenerateExcel<T>(string results, string modelName, Dictionary<string, string> dict = null, bool returnBytes = false,
            IStringLocalizer<T> localizer = null)
            where T : class
            => GenerateExcel(results, modelName, customColumns: dict == null ? null : CustomColumnExcel.FromMap(dict), returnBytes: returnBytes, localizer: localizer);

        public static dynamic GenerateExcel(string results, string modelName, List<CustomColumnExcel> customColumns, bool returnBytes = false)
            => GenerateExcel<object>(results, modelName, customColumns: customColumns, returnBytes: returnBytes);

        public static dynamic GenerateExcel<T>(string results, string modelName, List<CustomColumnExcel> customColumns = null, bool returnBytes = false,
            IStringLocalizer<T> localizer = null) where T : class
        {
            byte[] bytes = Array.Empty<byte>();
            MemoryStream stream = new();
            var _xlsxHelpers = new XlsxHelpers();

            using (ExcelPackage package = new(stream))
            {
                ExcelWorksheet worksheet = package.Workbook.Worksheets.Add(modelName);

                int row = 1;
                int column = 1;
                var draw = true;
                var numberformat = "#,##0";
                var listMap = new List<Dictionary<string, dynamic>>();

                var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(results));
                var map = new Dictionary<string, dynamic>();

                while (reader.Read())
                {
                    if (customColumns != null)
                    {
                        JsonTokenType tokenType = reader.TokenType;

                        switch (tokenType)
                        {
                            case JsonTokenType.StartArray:
                            case JsonTokenType.EndArray: break;
                            case JsonTokenType.EndObject:
                                //Console.WriteLine($" ----------- map {JsonSerializer.Serialize(map)} ------------- ");
                                listMap.Add(map);
                                break;
                            case JsonTokenType.StartObject:
                                map = new Dictionary<string, dynamic>();
                                break;
                            case JsonTokenType.PropertyName:
                                var name = reader.GetString();
                                //  reader.ValueTextEquals(Encoding.UTF8.GetBytes("name"))
                                if (customColumns.Any(x => x.Key == name))
                                {
                                    // Assume valid JSON, known schema
                                    reader.Read();
                                    switch (reader.TokenType)
                                    {
                                        case JsonTokenType.String:
                                            if (reader.TryGetDateTime(out DateTime @Datetime))
                                                map.Add(name, @Datetime);
                                            else
                                                map.Add(name, reader.GetString());

                                            break;
                                        case JsonTokenType.Number:
                                            if (reader.TryGetInt32(out int @int))
                                            {
                                                map.Add(name, @int);
                                            }
                                            else if (reader.TryGetDouble(out double @Double))
                                            {
                                                map.Add(name, @Double);
                                            }
                                            else
                                                map.Add(name, reader.GetDouble());

                                            break;
                                        case JsonTokenType.None:
                                        case JsonTokenType.Null:
                                            map.Add(name, null);
                                            break;
                                        case JsonTokenType.False:
                                            map.Add(name, false);
                                            break;
                                        case JsonTokenType.True:
                                            map.Add(name, true);
                                            break;
                                        default: break;
                                    }
                                }
                                break;

                        }

                    }
                    else
                    {

                        using ExcelRange Cells = worksheet.Cells[row + 1, column - 1 == 0 ? 1 : column - 1];
                        switch (reader.TokenType)
                        {
                            case JsonTokenType.StartObject: column = 1; break;
                            case JsonTokenType.EndObject: row++; break;
                            case JsonTokenType.StartArray:
                            case JsonTokenType.EndArray: break;
                            case JsonTokenType.PropertyName:
                                var name = reader.GetString();
                                // Headers
                                if (row == 1)
                                {
                                    using ExcelRange celdas = worksheet.Cells[row, column];
                                    celdas.Value = localizer != null ? localizer[name] : name.ToUpper();
                                }
                                draw = true;
                                column++;
                                break;
                            case JsonTokenType.String:
                                if (!draw) break;

                                if (reader.TryGetDateTime(out DateTime @Datetime))
                                {
                                    if (@Datetime == @Datetime.Date) Cells.Style.Numberformat.Format = "dd/mm/yyyy";
                                    else Cells.Style.Numberformat.Format = "dd/mm/yyyy HH:MM:ss";
                                    Cells.Value = @Datetime;
                                }
                                /*else if (decimal.TryParse(reader.GetString(), out decimal @Decimal))
                                {
                                    //number with 2 decimal places and thousand separator and money symbol
                                    numberformat = "$#,##0.00";
                                    Cells.Style.Numberformat.Format = numberformat;
                                    Cells.Value = @Decimal;
                                }*/
                                else
                                    Cells.Value = reader.GetString();
                                break;
                            case JsonTokenType.Number:
                                if (!draw) break;

                                if (reader.TryGetInt32(out int @int))
                                {
                                    Cells.Value = @int;
                                }
                                else if (reader.TryGetDouble(out double @Double))
                                {
                                    // verifica si es entero el numero
                                    if (@Double % 1 != 0)
                                    {
                                        numberformat = "#,###0.0";
                                        Cells.Style.Numberformat.Format = numberformat;
                                    }
                                    Cells.Value = @Double;
                                }
                                else
                                {
                                    Cells.Value = reader.GetDouble();
                                }
                                break;
                            case JsonTokenType.None:
                            case JsonTokenType.Null: break;
                            case JsonTokenType.False:
                                if (!draw) break; Cells.Value = "No";
                                break;
                            case JsonTokenType.True:
                                if (!draw) break; Cells.Value = "Si";
                                break;
                            default: break;
                        }
                    }

                }

                if (customColumns != null)
                {
                    column = 1;
                    foreach (var key in customColumns.Select(x => x.Key))
                    {
                        using ExcelRange Cells = worksheet.Cells[row, column];
                        Cells.Value = customColumns.First(x => x.Key == key).Value.ToUpper();
                        column++;
                    }
                    row++;

                    foreach (var dictionary in listMap)
                    {
                        column = 1;
                        foreach (var key in customColumns.Select(x => x.Key))
                        {
                            using ExcelRange Cells = worksheet.Cells[row, column];
                            var obj = dictionary[key];
                            if (obj is string)
                            {
                                Cells.Value = obj;
                            }
                            else if (obj is DateTime)
                            {
                                if (obj == obj.Date) Cells.Style.Numberformat.Format = "dd/mm/yyyy";
                                else Cells.Style.Numberformat.Format = "dd/mm/yyyy HH:MM:ss";
                                Cells.Value = obj;
                            }
                            else if (obj is int)
                            {
                                Cells.Value = obj;
                            }
                            else if (obj is double)
                            {
                                // verifica si es entero el numero
                                if (obj % 1 != 0)
                                {
                                    numberformat = "#,###0.0";
                                    Cells.Style.Numberformat.Format = numberformat;
                                }
                                Cells.Value = obj;
                            }
                            else if (obj is bool)
                            {
                                if (obj == true) Cells.Value = "Si";
                                else Cells.Value = "No";
                            }
                            else if (obj is null)
                            {
                                Cells.Value = "";
                            }
                            else
                            {
                                Cells.Value = "";
                            }

                            column++;
                        }
                        row++;
                    }


                }

                // para booleanos
                // Cells.Value = "Si";
                // Cells.Style.Fill.PatternType = ExcelFillStyle.Solid;
                // Cells.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(135, 236, 109));
                // Cells.Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(33, 119, 27));
                // _xlsxHelpers.BorderStyle(Cells, System.Drawing.Color.FromArgb(87, 175, 81));

                // Add to table / Add summary row
                //var tbl = worksheet.Tables.Add(new ExcelAddressBase(fromRow: 1, fromCol: 1, toRow: row - 1, toColumn: column - 1), "Data");
                //tbl.ShowHeader = true;
                //tbl.ShowTotal = true;
                //tbl.TableStyle = TableStyles.None;

                //tbl.Columns[3].DataCellStyleName = dataCellStyleName;
                //tbl.Columns[3].TotalsRowFunction = RowFunctions.Sum;

                // AutoFitColumns
                // worksheet.Cells[1, 1, row - 1, column - 1].AutoFitColumns();
                if (row > 1)
                    worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

                // Printer Settings
                worksheet.PrinterSettings.RepeatRows = new ExcelAddress("1:2");
                worksheet.PrinterSettings.BlackAndWhite = false;
                worksheet.PrinterSettings.PaperSize = ePaperSize.A4;
                worksheet.PrinterSettings.Orientation = eOrientation.Landscape;
                worksheet.PrinterSettings.TopMargin = 0.333333M;
                worksheet.PrinterSettings.RightMargin = 0.333333M;
                worksheet.PrinterSettings.BottomMargin = 0.44M;
                worksheet.PrinterSettings.LeftMargin = 0.333333M;
                worksheet.PrinterSettings.FitToPage = true;
                worksheet.PrinterSettings.FitToWidth = 1;
                worksheet.PrinterSettings.FitToHeight = 0;
                worksheet.PrinterSettings.PrintArea = worksheet.Cells[1, 1, row, 46];

                if (returnBytes)
                    bytes = package.GetAsByteArray();
                else
                    package.Save();
            }
            if (returnBytes)
                return bytes;
            else
                return stream;
        }


        public static dynamic GenerateXlsx<M>(List<M> items, Dictionary<string, string> dict, int sizeHeader = 1, bool returnBytes = false,
            int generalWidth = 0, int generalHeight = 0, bool autoFitColumns = true, bool wrapText = false) where M : class
            => GenerateXlsx(items, CustomColumnExcel.FromMap(dict), sizeHeader: sizeHeader, returnBytes: returnBytes,
                generalWidth: generalWidth, generalHeight: generalHeight, autoFitColumns: autoFitColumns, wrapText: wrapText);

        public static dynamic GenerateXlsx<M>(List<M> items, List<CustomColumnExcel> columns, int sizeHeader = 1, bool returnBytes = false,
            int generalWidth = 0, int generalHeight = 0, bool autoFitColumns = true, bool wrapText = false) where M : class
        {
            var _xlsxHelpers = new XlsxHelpers();
            MemoryStream stream = new();
            byte[] bytes = Array.Empty<byte>();
            using (ExcelPackage package = new(stream))
            {
                ExcelWorksheet Worksheet = package.Workbook.Worksheets.Add("Hoja 1");

                int Row = 1;
                int column = 1;

                foreach (var param in columns)
                {
                    using (ExcelRange Cells = Worksheet.Cells[param.Row, column])
                    {
                        Cells.Value = param.Value.FirstCharToUpper();
                        Cells.Style.Font.Bold = param.HeaderFontBold;
                        if (param.HeaderFontColor != null)
                            Cells.Style.Font.Color.SetColor(param.HeaderFontColor.Value);
                        if (param.HeaderBackgroundColor != null)
                        {
                            Cells.Style.Fill.PatternType = ExcelFillStyle.Solid;
                            Cells.Style.Fill.BackgroundColor.SetColor(param.HeaderBackgroundColor.Value);
                        }

                        if (!string.IsNullOrEmpty(param.Merge))
                        {
                            var range = Cells[param.Merge].Merge = true;
                            Cells.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                            Cells.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                        }
                    }

                    if (param.CustomRow != null)
                    {
                        using ExcelRange Cells = Worksheet.Cells[param.CustomRow.Row, column];
                        Cells.Value = param.CustomRow.Value.FirstCharToUpper();

                        Cells.Style.Font.Bold = param.CustomRow.FontBold;
                        if (param.CustomRow.FontColor != null)
                            Cells.Style.Font.Color.SetColor(param.CustomRow.FontColor.Value);
                        if (param.CustomRow.BackgroundColor != null)
                        {
                            Cells.Style.Fill.PatternType = ExcelFillStyle.Solid;
                            Cells.Style.Fill.BackgroundColor.SetColor(param.CustomRow.BackgroundColor.Value);
                        }

                        if (!string.IsNullOrEmpty(param.CustomRow.Merge))
                        {
                            var range = Cells[param.CustomRow.Merge].Merge = true;
                            Cells.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                            Cells.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                        }
                    }
                    column++;
                }

                Row = sizeHeader + 1;

                foreach (var item in items)
                {
                    column = 1;
                    foreach (var key in columns.Select(x => x.Key))
                    {
                        using (ExcelRange Cells = Worksheet.Cells[Row, column])
                        {
                            var prop = typeof(M).GetProperties().FirstOrDefault(x => x.Name == key);

                            if (prop != null)
                            {
                                var type = prop.PropertyType;
                                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
                                    type = type.GetGenericArguments()[0];

                                var value = prop.GetValue(item);
                                RenderRow(Cells, key, value, type, columns.FirstOrDefault(x => x.Key == key), wrapText);
                            }
                            else if (typeof(M) == typeof(ExpandoObject))
                            {
                                Worksheet.Row(Row).CustomHeight = true;

                                IDictionary<string, object> propertyValues = item as ExpandoObject;

                                var property = propertyValues.FirstOrDefault(x => x.Key == key);
                                RenderRow(Cells, property.Key, propertyValues[property.Key], propertyValues[property.Key]?.GetType(), columns.FirstOrDefault(x => x.Key == key), wrapText);

                            }
                        }

                        column++;
                    }

                    Row++;
                }

                if (generalWidth > 0)
                    Worksheet.Cells[Worksheet.Dimension.Address].EntireColumn.Width = generalWidth;
                if (generalHeight > 0)
                    Worksheet.Cells[Worksheet.Dimension.Address].EntireRow.Height = generalHeight;

                if (autoFitColumns && Row > 1)
                    Worksheet.Cells[Worksheet.Dimension.Address].AutoFitColumns();

                Worksheet.Cells[Worksheet.Dimension.Address].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                Worksheet.Cells[Worksheet.Dimension.Address].Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                Worksheet.Cells[Worksheet.Dimension.Address].Style.Font.Name = "Arial";
                Worksheet.Cells[Worksheet.Dimension.Address].Style.Font.Size = 10;
                if (Row > 1)
                    Worksheet.Row(1).Height = 44;

                // Printer Settings
                Worksheet.PrinterSettings.RepeatRows = new ExcelAddress("1:2");
                Worksheet.PrinterSettings.BlackAndWhite = false;
                Worksheet.PrinterSettings.PaperSize = ePaperSize.A4;
                Worksheet.PrinterSettings.Orientation = eOrientation.Landscape;
                Worksheet.PrinterSettings.TopMargin = 0.333333M;
                Worksheet.PrinterSettings.RightMargin = 0.333333M;
                Worksheet.PrinterSettings.BottomMargin = 0.44M;
                Worksheet.PrinterSettings.LeftMargin = 0.333333M;
                Worksheet.PrinterSettings.FitToPage = true;
                Worksheet.PrinterSettings.FitToWidth = 1;
                Worksheet.PrinterSettings.FitToHeight = 0;
                Worksheet.PrinterSettings.PrintArea = Worksheet.Cells[1, 1, Row, 46];

                if (returnBytes)
                    bytes = package.GetAsByteArray();
                else
                    package.Save();
            }
            if (returnBytes)
                return bytes;
            else
                return stream;
        }

        private static void RenderRow(ExcelRange Cells, string key, object value, Type type, CustomColumnExcel customColumnExcel, bool wrapText)
        {
            var numberformat = "#,##0";

            if (customColumnExcel != null)
            {
                Cells.Style.Font.Bold = customColumnExcel.FontBold;
                if (customColumnExcel.FontColor != null)
                    Cells.Style.Font.Color.SetColor(customColumnExcel.FontColor.Value);
                if (customColumnExcel.BackgroundColor != null)
                {
                    Cells.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    Cells.Style.Fill.BackgroundColor.SetColor(customColumnExcel.BackgroundColor.Value);
                }
            }

            if (value is null)
            {
                Cells.Value = "-";
            }
            else if (type == typeof(string))
            {
                if (string.IsNullOrEmpty(value.ToString())) Cells.Value = "-";
                else Cells.Value = value.ToString();

                Cells.Style.WrapText = wrapText;
                if (key == "email")
                {
                    Cells.Style.Font.UnderLine = true;
                    Cells.Style.Font.Color.SetColor(Color.Blue);
                    Cells.Hyperlink = new Uri("mailto:" + value.ToString(), UriKind.Absolute);
                }
                if ((key == "image" || key == "attachment_id") && !string.IsNullOrEmpty(value.ToString()))
                {
                    Cells.Style.Font.UnderLine = true;
                    Cells.Style.Font.Color.SetColor(Color.Blue);
                    Cells.Hyperlink = new Uri(value.ToString(), UriKind.Absolute);
                }
                //if (key == "phone") Cells.Hyperlink = new Uri(value.ToString(), UriKind.Absolute);

            }
            else if (type == typeof(int))
            {
                //numberformat = "#";
                //Cells.Style.Numberformat.Format = numberformat;
                Cells.Value = (int)value;
            }
            else if (type == typeof(float))
            {
                //if ((float)value % 1 != 0)
                //{
                numberformat = customColumnExcel?.CellFormat ?? "#,###0.0";
                Cells.Style.Numberformat.Format = numberformat;
                //}
                Cells.Value = (float)value;
            }
            else if (type == typeof(decimal))
            {
                //if ((decimal)value % 1 != 0)
                //{
                //number with 2 decimal places and thousand separator and money symbol
                numberformat = customColumnExcel?.CellFormat ?? "$#,##0.00";
                Cells.Style.Numberformat.Format = numberformat;
                //}
                //else
                //{
                //    numberformat = customColumnExcel?.CellFormat ?? "$#,##";
                //    Cells.Style.Numberformat.Format = numberformat;
                //}
                Cells.Value = (decimal)value;
            }
            else if (type == typeof(double))
            {
                //if ((double)value % 1 != 0)
                //{
                numberformat = customColumnExcel?.CellFormat ?? "#,###0.00";
                Cells.Style.Numberformat.Format = numberformat;
                //}
                Cells.Value = (double)value;
            }
            else if (type == typeof(bool))
            {
                Cells.Style.Fill.PatternType = ExcelFillStyle.Solid;
                Cells.Value = value.ToString();
                if ((bool)value)
                    Cells.Value = "Si";
                else
                    Cells.Value = "No";
            }

            else if (type == typeof(DateTime))
            {
                var time = (DateTime)value;
                if (time == time.Date) Cells.Style.Numberformat.Format = customColumnExcel?.CellFormat ?? "dd/mm/yyyy";
                else Cells.Style.Numberformat.Format = customColumnExcel?.CellFormat ?? "dd/mm/yyyy HH:MM";
                Cells.Value = time;
            }
            else
            {
                Cells.Value = value;
            }
        }
    }


    public partial class CustomColumnExcel
    {
        public CustomColumnExcel() { }

        public CustomColumnExcel(string Key, string Value) { this.Key = Key; this.Value = Value; }
        public CustomColumnExcel(string Key, string Value, string Merge) { this.Key = Key; this.Value = Value; this.Merge = Merge; }
        public string Key { get; set; }
        public string Value { get; set; }
        public string Merge { get; set; }
        public int Row { get; set; } = 1;

        public bool HeaderFontBold { get; set; }
        public Color? HeaderBackgroundColor { get; set; }
        public Color? HeaderFontColor { get; set; }

        public bool FontBold { get; set; }
        public Color? BackgroundColor { get; set; }
        public Color? FontColor { get; set; }
        public string CellFormat { get; set; } = null;

        public CustomRowExcel CustomRow { get; set; }
    }

    public partial class CustomColumnExcel
    {
        public static List<CustomColumnExcel> FromMap(Dictionary<string, string> dict)
        {
            List<CustomColumnExcel> items = new();
            foreach (var item in dict)
            {
                items.Add(new CustomColumnExcel(item.Key, item.Value));
            }
            return items;
        }
    }

    public class CustomRowExcel
    {
        public string Value { get; set; }
        public string Merge { get; set; }
        public int Row { get; set; } = 1;
        public bool FontBold { get; set; }
        public Color? BackgroundColor { get; set; }
        public Color? FontColor { get; set; }


    }
}
