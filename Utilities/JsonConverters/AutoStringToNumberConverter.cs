using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SER.Utilitties.NetCore.Utilities.JsonConverters
{
    public class AutoStringToNumberConverter : JsonConverter<int>
    {
        //public override bool CanConvert(Type typeToConvert)
        //{
        //    // see https://stackoverflow.com/questions/1749966/c-sharp-how-to-determine-whether-a-type-is-a-number
        //    switch (Type.GetTypeCode(typeToConvert))
        //    {
        //        case TypeCode.Byte:
        //        case TypeCode.SByte:
        //        case TypeCode.UInt16:
        //        case TypeCode.UInt32:
        //        case TypeCode.UInt64:
        //        case TypeCode.Int16:
        //        case TypeCode.Int32:
        //        case TypeCode.Int64:
        //        case TypeCode.Decimal:
        //        case TypeCode.Double:
        //        case TypeCode.Single:
        //            return true;
        //        default:
        //            return false;
        //    }
        //}

        public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                var s = reader.GetString();
                return int.TryParse(s, out var i) ? i : throw new Exception($"unable to parse {s} to number");
                //?
                //i :
                //(double.TryParse(s, out var d) ?
                //    d :
                //    throw new Exception($"unable to parse {s} to number")
                //);
            }
            if (reader.TokenType == JsonTokenType.Number)
            {
                return reader.TryGetInt32(out int l) ? l : reader.GetInt32();
                //reader.GetDouble();
            }
            using (JsonDocument document = JsonDocument.ParseValue(ref reader))
            {
                throw new Exception($"unable to parse {document.RootElement.ToString()} to number");
            }
        }


        public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
        {
            var str = value.ToString();             // I don't want to write int/decimal/double/...  for each case, so I just convert it to string . You might want to replace it with strong type version.
            if (int.TryParse(str, out var i))
            {
                writer.WriteNumberValue(i);
            }
            else if (double.TryParse(str, out var d))
            {
                writer.WriteNumberValue(d);
            }
            else
            {
                throw new Exception($"unable to parse {str} to number");
            }
        }
    }
}