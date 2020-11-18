using Newtonsoft.Json.Linq;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using OfficeOpenXml.Table;
using SER.Utilitties.NetCore.Utilities;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SER.Utilitties.NetCore.Services
{
    public class ExcelService
    {
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

            JArray jArray = new JArray();
            string Query = $"SELECT {columnStr} FROM \"{modelName}\"";
            var Params = new Dictionary<string, object>();

            if (!string.IsNullOrEmpty(parameters))
            {
                JObject jObjParams = JObject.Parse(parameters);
                int index = 0;
                foreach (JProperty pair in jObjParams.Properties())
                {
                    string propertyName = pair.Name;

                    switch (pair.Value.Type)
                    {
                        case JTokenType.String:
                            Params.Add(string.Format("@{0}", propertyName), (string)pair.Value);
                            break;
                        case JTokenType.Integer:
                            Params.Add(string.Format("@{0}", propertyName), (int)pair.Value);
                            break;
                        case JTokenType.Boolean:
                            Params.Add(string.Format("@{0}", propertyName), (bool)pair.Value);
                            break;
                        case JTokenType.Float:
                            Params.Add(string.Format("@{0}", propertyName), (float)pair.Value);
                            break;
                    }

                    if (index == 0)
                        Query = string.Format(@"{0} WHERE ""{1}"" = @{1}", Query, propertyName);
                    else
                        Query = string.Format(@"{0} AND ""{1}"" = @{1}", Query, propertyName);

                    index++;
                }
            }

            if (!string.IsNullOrEmpty(orderBy))
            {
                Query = string.Format("{0} ORDER BY \"{1}\"", Query, orderBy);
            }

            var result = await postgresService.GetDataFromDBAsync(Query, Params: string.IsNullOrEmpty(parameters) ? null : Params);

            if (!string.IsNullOrEmpty(result))
            {
                jArray = JArray.Parse(result);
            }

            if (download)
            {
                return GenerateExcel(jArray, modelName);
            }

            return jArray;
        }

        public byte[] GenerateExcel(JArray jArray, String modelName)
        {
            if (jArray.Count == 0) return null;
            byte[] bytes;
            MemoryStream stream = new MemoryStream();
            using (ExcelPackage package = new ExcelPackage(stream))
            {
                int column = 1;
                ExcelWorksheet worksheet = package.Workbook.Worksheets.Add(modelName);
                //First add the headers
                int row = 1;
                foreach (JObject parsedObject in jArray.Children<JObject>())
                {
                    column = 1;
                    foreach (JProperty pair in parsedObject.Properties())
                    {
                        string propertyName = pair.Name;
                        worksheet.Cells[row, column].Value = propertyName.ToUpper();
                        column++;
                    }
                }
                row++;

                var numberformat = "#,##0";
                //var dataCellStyleName = "TableNumber";
                //var numStyle = package.Workbook.Styles.CreateNamedStyle(dataCellStyleName);
                //numStyle.Style.Numberformat.Format = numberformat;

                foreach (JObject parsedObject in jArray.Children<JObject>())
                {
                    column = 1;
                    foreach (JProperty pair in parsedObject.Properties())
                    {
                        string propertyName = pair.Name;
                        //_logger.LogInformation($"Name: {propertyName}, Type: {parsedObject[propertyName].Type}, Value: {pair.Value}");

                        if (pair.Value.Type == JTokenType.None || pair.Value.Type == JTokenType.Null)
                        {
                            worksheet.Cells[row, column].Value = string.Empty;
                        }
                        else if (pair.Value.Type == JTokenType.String)
                        {
                            worksheet.Cells[row, column].Value = (string)pair.Value;
                        }
                        else if (pair.Value.Type == JTokenType.Integer)
                        {
                            numberformat = "#";
                            worksheet.Cells[row, column].Style.Numberformat.Format = numberformat;
                            worksheet.Cells[row, column].Value = (int)pair.Value;
                        }
                        else if (pair.Value.Type == JTokenType.Boolean)
                        {
                            worksheet.Cells[row, column].Value = (bool)pair.Value;
                        }
                        else if (pair.Value.Type == JTokenType.Float)
                        {
                            numberformat = "#,###0";
                            worksheet.Cells[row, column].Style.Numberformat.Format = numberformat;
                            worksheet.Cells[row, column].Value = (float)pair.Value;
                        }
                        else if (pair.Value.Type == JTokenType.Date)
                        {
                            worksheet.Cells[row, column].Style.Numberformat.Format = "yyyy-mm-dd HH:MM:ss";
                            worksheet.Cells[row, column].Value = (DateTime)pair.Value;
                        }
                        else if (pair.Value.Type == JTokenType.Array)
                        {
                            worksheet.Cells[row, column].Value = ((JArray)pair.Value).ToString(Newtonsoft.Json.Formatting.None);
                        }
                        else
                        {
                            worksheet.Cells[row, column].Value = pair.Value;
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
                                    Cells.Style.Numberformat.Format = "dd/mm/yyyy";
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
