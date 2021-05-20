using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
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
           string columnStr = "", string orderBy = "", string parameters = "", bool download = false)
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

            if (!string.IsNullOrEmpty(parameters))
            {
                var jObjParams = JsonExtensions.ToJsonDocument(parameters);
                var props = jObjParams.EnumerateObject();
                int index = 0;
                while (props.MoveNext())
                {
                    var pair = props.Current;
                    string propertyName = pair.Name;
                    var propType = pair.Value.GetType();

                    if (bool.TryParse(pair.Value.ToString(), out bool @bool))
                        Params.Add(string.Format("@{0}", propertyName), pair.Value.GetBoolean());
                    else if (int.TryParse(pair.Value.ToString(), out int @int))
                        Params.Add(string.Format("@{0}", propertyName), @int);
                    else if (double.TryParse(pair.Value.ToString(), out double @double))
                        Params.Add(string.Format("@{0}", propertyName), @double);
                    else
                        Params.Add(string.Format("@{0}", propertyName), $"%{pair.Value}%");

                    if (index == 0)
                        Query = string.Format(@"{0} WHERE ""{1}"" ilike @{1}", Query, propertyName);
                    else
                        Query = string.Format(@"{0} AND ""{1}"" ilike @{1}", Query, propertyName);

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

        public byte[] GenerateExcel(string results, string modelName, string[] except = null)
        {
            byte[] bytes;
            MemoryStream stream = new MemoryStream();
            var _xlsxHelpers = new XlsxHelpers();

            using (ExcelPackage package = new ExcelPackage(stream))
            {
                ExcelWorksheet worksheet = package.Workbook.Worksheets.Add(modelName);

                int row = 1;
                int column = 1;

                var jsonElement = JsonExtensions.ToJsonDocument(results);
                var array = jsonElement.EnumerateArray();

                // Headers
                if (array.Any())
                {
                    string[] keys = array.First().EnumerateObject().Select(p => p.Name).ToArray();

                    if (except != null && except.Length > 0)
                        keys = array.First().EnumerateObject().Where(x => !except.Contains(x.Name)).Select(p => p.Name).ToArray();

                    foreach (var key in keys)
                    {
                        using (ExcelRange Cells = worksheet.Cells[row, column])
                        {
                            Cells.Value = key.ToUpper();
                            // _xlsxHelpers.MakeTitle(Cells);
                        }

                        column++;
                    }
                }

                var numberformat = "#,##0";
                row++;

                while (array.MoveNext())
                {
                    var objectElement = array.Current;

                    var props = objectElement.EnumerateObject();
                    column = 1;
                    while (props.MoveNext())
                    {
                        var pair = props.Current;
                        string propertyName = pair.Name;
                        if (except != null && except.Contains(propertyName)) continue;

                        var propType = pair.Value.GetType();

                        using (ExcelRange Cells = worksheet.Cells[row, column])
                        {

                            if (propType == null)
                            {
                                Cells.Value = string.Empty;
                            }
                            else if (propType == typeof(string))
                            {
                                Cells.Value = pair.Value.ToString();
                            }
                            else if (propType == typeof(int))
                            {
                                numberformat = "#";
                                Cells.Style.Numberformat.Format = numberformat;
                                Cells.Value = pair.Value.GetInt32();
                            }
                            else if (propType == typeof(bool))
                            {
                                Cells.Style.Fill.PatternType = ExcelFillStyle.Solid;

                                if (pair.Value.GetBoolean())
                                {
                                    Cells.Value = "active";
                                    Cells.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(135, 236, 109));
                                    Cells.Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(33, 119, 27));
                                    _xlsxHelpers.BorderStyle(Cells, System.Drawing.Color.FromArgb(87, 175, 81));
                                }
                                else
                                {
                                    Cells.Value = "inactive";
                                    Cells.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(255, 165, 165));
                                    Cells.Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(169, 34, 34));
                                    _xlsxHelpers.BorderStyle(Cells, System.Drawing.Color.FromArgb(253, 78, 78));
                                }
                            }
                            else if (propType == typeof(decimal) || propType == typeof(float) || propType == typeof(double))
                            {
                                numberformat = "#,###0";
                                Cells.Style.Numberformat.Format = numberformat;
                                Cells.Value = pair.Value;
                            }
                            else if (propType == typeof(DateTime))
                            {
                                Cells.Style.Numberformat.Format = "yyyy-mm-dd HH:MM:ss";
                                Cells.Value = pair.Value.GetDateTime();
                            }
                            else
                            {
                                Cells.Value = pair.Value;
                            }
                        }

                        column++;
                    }
                    row++;
                }

                // Add to table / Add summary row
                var tbl = worksheet.Tables.Add(new ExcelAddressBase(fromRow: 1, fromCol: 1, toRow: row - 1, toColumn: column - 1), "Data");
                tbl.ShowHeader = true;
                tbl.ShowTotal = true;
                tbl.TableStyle = TableStyles.None;

                //tbl.Columns[3].DataCellStyleName = dataCellStyleName;
                //tbl.Columns[3].TotalsRowFunction = RowFunctions.Sum;
                //worksheet.Cells[5, 4].Style.Numberformat.Format = "#,##0";

                // AutoFitColumns
                worksheet.Cells[1, 1, row - 1, column - 1].AutoFitColumns();
                bytes = package.GetAsByteArray();
            }
            return bytes;
        }

        public MemoryStream GenerateXlsx<M>(List<M> items, Dictionary<string, string> keys) where M : class
        {
            var _xlsxHelpers = new XlsxHelpers();
            MemoryStream stream = new MemoryStream();
            using (ExcelPackage package = new ExcelPackage(stream))
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
                                var value = prop.GetValue(order);
                                if (value is null)
                                {
                                    Cells.Value = "-";
                                }
                                else if (value is string)
                                {
                                    if (string.IsNullOrEmpty(value.ToString())) Cells.Value = "-";
                                    else Cells.Value = value.ToString();

                                    if (key == "email")
                                    {
                                        Cells.Style.Font.UnderLine = true;
                                        Cells.Style.Font.Color.SetColor(Color.Blue);
                                        Cells.Hyperlink = new Uri("mailto:" + value.ToString(), UriKind.Absolute);
                                    }
                                    if (key == "image" && !string.IsNullOrEmpty(value.ToString()))
                                    {
                                        Cells.Style.Font.UnderLine = true;
                                        Cells.Style.Font.Color.SetColor(Color.Blue);
                                        Cells.Hyperlink = new Uri(value.ToString(), UriKind.Absolute);
                                    }
                                    //if (key == "phone") Cells.Hyperlink = new Uri(value.ToString(), UriKind.Absolute);

                                }
                                else if (value is int @int)
                                {
                                    numberformat = "#";
                                    Cells.Style.Numberformat.Format = numberformat;
                                    Cells.Value = @int;
                                }
                                else if (value is float single)
                                {
                                    numberformat = "#,###0";
                                    Cells.Style.Numberformat.Format = numberformat;
                                    Cells.Value = single;
                                }
                                else if (value is decimal decim)
                                {
                                    //number with 2 decimal places and thousand separator and money symbol
                                    numberformat = "$#,##0.00";
                                    Cells.Style.Numberformat.Format = numberformat;
                                    Cells.Value = decim;
                                }
                                else if (value is bool boolean)
                                {
                                    Cells.Style.Fill.PatternType = ExcelFillStyle.Solid;
                                    Cells.Value = value.ToString();
                                    if (boolean)
                                    {
                                        Cells.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(135, 236, 109));
                                        Cells.Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(33, 119, 27));
                                        _xlsxHelpers.BorderStyle(Cells, System.Drawing.Color.FromArgb(87, 175, 81));
                                    }
                                    else
                                    {
                                        Cells.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(255, 165, 165));
                                        Cells.Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(169, 34, 34));
                                        _xlsxHelpers.BorderStyle(Cells, System.Drawing.Color.FromArgb(253, 78, 78));
                                    }
                                }

                                else if (value is DateTime)
                                {
                                    Cells.Style.Numberformat.Format = "dd/mm/yyyy HH:MM";
                                    Cells.Value = (DateTime)value;
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

                package.Save();

            }
            return stream;
        }
    }
}
