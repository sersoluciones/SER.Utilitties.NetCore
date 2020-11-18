using Newtonsoft.Json;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SER.Utilitties.NetCore.Utilities
{
    public static class JsonExtensions
    {
        public static T ElementToObject<T>(JsonElement element)
        {
            var bufferWriter = new ArrayBufferWriter<byte>();
            using (var writer = new Utf8JsonWriter(bufferWriter))
                element.WriteTo(writer);
            return System.Text.Json.JsonSerializer.Deserialize<T>(bufferWriter.WrittenSpan, null);
        }
        public static T ToObject<T>(this JsonElement element, JsonSerializerOptions options = null)
        {
            var bufferWriter = new ArrayBufferWriter<byte>();
            using (var writer = new Utf8JsonWriter(bufferWriter))
                element.WriteTo(writer);
            return System.Text.Json.JsonSerializer.Deserialize<T>(bufferWriter.WrittenSpan, options);
        }

        public static T ToObject<T>(this JsonDocument document, JsonSerializerOptions options = null)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));
            return document.RootElement.ToObject<T>(options);
        }


        public static T LoadFromFileWithGeoJson<T>(string path, JsonSerializerSettings settings = null)
        {
            var serializer = NetTopologySuite.IO.GeoJsonSerializer.CreateDefault(settings);
            serializer.CheckAdditionalContent = true;
            using (var textReader = new StreamReader(path))
            using (var jsonReader = new JsonTextReader(textReader))
            {
                return serializer.Deserialize<T>(jsonReader);
            }
        }

        public static string SerializeWithGeoJson<T>(T obj, Formatting formatting = Formatting.Indented, JsonSerializerSettings settings = null)
        {
            var serializer = NetTopologySuite.IO.GeoJsonSerializer.CreateDefault(settings);
            var sb = new StringBuilder();
            using (var writer = new StringWriter(sb))
            {
                serializer.Formatting = formatting;
                serializer.Serialize(writer, obj);
            }
            return sb.ToString();
        }

        public static T DeserializeWithGeoJson<T>(string json, Formatting formatting = Formatting.Indented, JsonSerializerSettings settings = null)
        {
            var serializer = NetTopologySuite.IO.GeoJsonSerializer.CreateDefault(settings);
            serializer.CheckAdditionalContent = true;
            using (var textReader = new StringReader(json))
            using (var jsonReader = new JsonTextReader(textReader))
            {
                return serializer.Deserialize<T>(jsonReader);
            }
        }
    }
}
