using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SER.Utilitties.NetCore.WhatsAppAPI.Models
{
    public partial class MsgWhatsApp
    {
        [JsonPropertyName("messaging_product")]
        public string MessagingProduct { get; set; } = "whatsapp";

        [JsonPropertyName("to")]
        public string To { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; } = "template";

        [JsonPropertyName("template")]
        public Template Template { get; set; }
    }

    public partial class Template
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "sending_code";

        [JsonPropertyName("language")]
        public Language Language { get; set; }

        [JsonPropertyName("components")]
        public Component[] Components { get; set; }
    }

    public partial class Component
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "body";

        [JsonPropertyName("parameters")]
        public Parameter[] Parameters { get; set; }
    }

    public partial class Parameter
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "text";

        [JsonPropertyName("text")]
        public string Text { get; set; }
    }

    public partial class Language
    {
        [JsonPropertyName("code")]
        public string Code { get; set; } = "es";

        [JsonPropertyName("policy")]
        public string Policy { get; set; } = "deterministic";
    }

}
