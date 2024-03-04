using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SER.Utilitties.NetCore.WhatsAppAPI.Models
{
#nullable enable
    public class WInteractive
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "list";

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("text")]
        public WText? Text { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("body")]
        public WBody? Body { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("footer")]
        public WFooter? Footer { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("action")]
        public WAction? Action { get; set; }

        public WInteractive(string body)
        {
            Body = new WBody(body);
        }

        public WInteractive()
        {
        }
    }

    public partial class WAction
    {
        [JsonPropertyName("button")]
        public string Button { get; set; } = "Opciones";

        [JsonPropertyName("sections")]
        public WSection[] Sections { get; set; }
    }

    public partial class WSection
    {
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("rows")]
        public List<WRow> Rows { get; set; } = new List<WRow>();
    }

    public partial class WRow
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        public WRow(string id, string title, string? description = null)
        {
            Id = id;
            Title = title;
            Description = description;
        }
    }

    public partial class WText
    {
        [JsonPropertyName("body")]
        public string Body { get; set; }

        public WText(string text)
        {
            Body = text;
        }
    }

    public partial class WBody
    {
        [JsonPropertyName("text")]
        public string Text { get; set; }

        public WBody(string text)
        {
            Text = text;
        }
    }

    public partial class WFooter
    {
        [JsonPropertyName("text")]
        public string Text { get; set; } = "Con la tecnología de Wasamblea";
    }
#nullable disable
}
