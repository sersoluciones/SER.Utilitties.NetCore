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

        private static string ParamsToString(NpgsqlParameter[] dictionary)
        {
            return "{" + string.Join(",", dictionary.Select(kv => kv.ParameterName + "=" + kv.Value).ToArray()) + "}";
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
                    _logger.LogInformation($"Executed DbCommand [Parameters=[{ParamsToString(cmd.Parameters.ToArray())}], " +
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
                return GenerateExcel<object>(result, modelName);
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
        {
            return GenerateExcel<object>(results, modelName, dict: dict, returnBytes: returnBytes);
        }

        public static dynamic GenerateExcel<T>(string results, string modelName, Dictionary<string, string> dict = null, bool returnBytes = false,
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

                var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(results));
                var draw = true;
                var numberformat = "#,##0";
                while (reader.Read())
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
                            if (dict != null && dict.Values != null && dict.Any(x => x.Key == name))
                            {
                                // Headers
                                if (row == 1)
                                {
                                    using ExcelRange celdas = worksheet.Cells[row, column];
                                    celdas.Value = dict.First(x => x.Key == name).Value.ToUpper();
                                }
                                draw = true;
                                column++;
                            }
                            else if (dict?.Values == null)
                            {
                                // Headers
                                if (row == 1)
                                {
                                    using ExcelRange celdas = worksheet.Cells[row, column];
                                    celdas.Value = localizer != null ? localizer[name] : name.ToUpper();
                                }
                                draw = true;
                                column++;
                            }
                            else
                                draw = false;
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
                                numberformat = "#,###0.0";
                                Cells.Style.Numberformat.Format = numberformat;
                                Cells.Value = @Double;
                            }
                            else
                            {
                                Cells.Value = reader.GetDouble();
                            }
                            break;
                        case JsonTokenType.None:
                        case JsonTokenType.Null: break;
                        case JsonTokenType.False: if (!draw) break; Cells.Value = "No"; break;
                        case JsonTokenType.True: if (!draw) break; Cells.Value = "Si"; break;
                        default: break;
                    }
                }

                if (row == 1 && dict != null)
                {
                    foreach (var key in dict.Values)
                    {
                        using (ExcelRange Cells = worksheet.Cells[row, column])
                        {
                            Cells.Value = key.ToUpper();
                        }

                        column++;
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
                //worksheet.Cells[1, 1, row - 1, column - 1].AutoFitColumns();
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

        public static dynamic GenerateXlsx<M>(List<M> items, Dictionary<string, string> keys, bool returnBytes = false) where M : class
        {
            var _xlsxHelpers = new XlsxHelpers();
            MemoryStream stream = new();
            byte[] bytes = Array.Empty<byte>();
            using (ExcelPackage package = new(stream))
            {
                ExcelWorksheet Worksheet = package.Workbook.Worksheets.Add("Hoja 1");

                int Row = 1;
                int column = 1;

                foreach (var key in keys.Values)
                {
                    using (ExcelRange Cells = Worksheet.Cells[Row, column])
                    {
                        Cells.Value = key;
                        //_xlsxHelpers.MakeTitle(Cells);
                    }

                    column++;
                }

                var numberformat = "#,##0";
                Row++;

                foreach (var order in items)
                {
                    column = 1;
                    foreach (var key in keys.Keys)
                    {
                        using (ExcelRange Cells = Worksheet.Cells[Row, column])
                        {
                            var prop = typeof(M).GetProperties().FirstOrDefault(x => x.Name == key);
                            if (prop != null)
                            {
                                var type = prop.PropertyType;
                                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
                                    type = type.GetGenericArguments()[0];

                                var value = prop.GetValue(order);
                                if (value is null)
                                {
                                    Cells.Value = "-";
                                }
                                else if (type == typeof(string))
                                {
                                    if (string.IsNullOrEmpty(value.ToString())) Cells.Value = "-";
                                    else Cells.Value = value.ToString();

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
                                    numberformat = "#,###0.0";
                                    Cells.Style.Numberformat.Format = numberformat;
                                    Cells.Value = (float)value;
                                }
                                else if (type == typeof(decimal))
                                {
                                    //number with 2 decimal places and thousand separator and money symbol
                                    numberformat = "$#,##0.00";
                                    Cells.Style.Numberformat.Format = numberformat;
                                    Cells.Value = (decimal)value;
                                }
                                else if (type == typeof(double))
                                {
                                    numberformat = "#,###0.00";
                                    Cells.Style.Numberformat.Format = numberformat;
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
                                    if (time == time.Date) Cells.Style.Numberformat.Format = "dd/mm/yyyy";
                                    else Cells.Style.Numberformat.Format = "dd/mm/yyyy HH:MM";
                                    Cells.Value = time;
                                }
                                else
                                {
                                    Cells.Value = value;
                                }
                            }
                        }

                        column++;
                    }

                    Row++;
                }

                Worksheet.Cells[Worksheet.Dimension.Address].AutoFitColumns();
                Worksheet.Cells[Worksheet.Dimension.Address].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                Worksheet.Cells[Worksheet.Dimension.Address].Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                Worksheet.Cells[Worksheet.Dimension.Address].Style.Font.Name = "Arial";
                Worksheet.Cells[Worksheet.Dimension.Address].Style.Font.Size = 10;

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
    }
}
