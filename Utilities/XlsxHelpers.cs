using OfficeOpenXml;
using OfficeOpenXml.Style;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SER.Utilitties.NetCore.Utilities
{
    public class XlsxHelpers
    {
        public void BorderStyle(ExcelRange Range, Color? BorderColor = null, ExcelBorderStyle style = ExcelBorderStyle.Medium)
        {
            Range.Style.Border.Top.Style = style;
            Range.Style.Border.Right.Style = style;
            Range.Style.Border.Bottom.Style = style;
            Range.Style.Border.Left.Style = style;

            Range.Style.Border.Top.Color.SetColor(BorderColor ?? Color.Black);
            Range.Style.Border.Right.Color.SetColor(BorderColor ?? Color.Black);
            Range.Style.Border.Bottom.Color.SetColor(BorderColor ?? Color.Black);
            Range.Style.Border.Left.Color.SetColor(BorderColor ?? Color.Black);

        }

        public void MakeTitle(ExcelRange Range, Color? BorderColor = null, Color? BackgroundColor = null, ExcelBorderStyle style = ExcelBorderStyle.Medium)
        {
            Range.Merge = true;
            Range.Style.WrapText = true;
            Range.Style.Font.Bold = true;
            Range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            Range.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
            Range.Style.Fill.PatternType = ExcelFillStyle.Solid;
            Range.Style.Fill.BackgroundColor.SetColor(BackgroundColor ?? Color.LightGray);
            BorderStyle(Range, BorderColor: BorderColor ?? Color.Black, style: style);
        }

        public void SetValue(ExcelRange Range, string Value)
        {
            Range.Merge = true;
            Range.Style.WrapText = true;
            Range.Value = Value;
        }

        public void SetCheck(ExcelRange Range)
        {
            Range.Value = "\u2713";
            Range.Style.Font.Color.SetColor(Color.Green);
            //Range.Style.Font.Name = "Arial";
            //Range.Style.Font.Size = 24;
        }
    }
}

