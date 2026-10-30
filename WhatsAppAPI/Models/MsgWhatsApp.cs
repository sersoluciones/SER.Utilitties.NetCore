using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SER.Utilitties.NetCore.WhatsAppAPI.Models
{
#nullable enable
    public partial class MsgWhatsApp
    {
        [JsonPropertyName("messaging_product")]
        public string MessagingProduct { get; set; } = "whatsapp";

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("to")]
        public string? To { get; set; }

        /// <summary>Business-scoped user id (BSUID) of the recipient. Meta uses <see cref="To"/> when both are set.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("recipient")]
        public string? Recipient { get; set; }

        /// <summary>
        /// Addresses the message to a phone number or to a BSUID (<c>CO.2208218207240616</c>), whichever
        /// <paramref name="id"/> is. Users with a WhatsApp username may only be reachable by BSUID.
        /// </summary>
        public MsgWhatsApp SetRecipient(string id)
        {
            if (BsuidPattern.IsMatch(id))
            {
                Recipient = id;
                To = null;
            }
            else
            {
                To = id;
                Recipient = null;
            }
            return this;
        }

        private static readonly Regex BsuidPattern = new(@"^[A-Z]{2}\.[A-Za-z0-9]+$", RegexOptions.Compiled);

        [JsonPropertyName("type")]
        public string Type { get; set; } = "template";

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("template")]
        public Template? Template { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("interactive")]
        public WInteractive? Interactive { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("text")]
        public WText? Text { get; set; }
    }

    public partial class Template
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "sending_code";

        [JsonPropertyName("language")]
        public Language? Language { get; set; }

        [JsonPropertyName("components")]
        public Component[]? Components { get; set; }
    }

    public partial class Component
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "body";

        [JsonPropertyName("sub_type")]
        public string? SubType { get; set; }

        [JsonPropertyName("index")]
        public string? Index { get; set; }

        [JsonPropertyName("parameters")]
        public Parameter[]? Parameters { get; set; }
    }

    public partial class Parameter
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "text";

        /// <summary>
        /// Placeholder this value fills, for templates whose body uses named variables (<c>{{name}}</c>).
        /// </summary>
        /// <remarks>
        /// Meta refuses a named-parameter template with «Parameter name is missing or empty» when the
        /// payload omits it. Left null for positional templates, and omitted from the JSON when null so
        /// the templates that already work keep sending exactly what they sent before.
        /// </remarks>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("parameter_name")]
        public string? ParameterName { get; set; }

        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("payload")]
        public string? Payload { get; set; }

        [JsonPropertyName("image")]
        public ImageComponent? Image { get; set; }

        [JsonPropertyName("video")]
        public VideoComponent? Video { get; set; }
    }

    public class ImageComponent
    {
        [JsonPropertyName("link")]
        public string? Link { get; set; }
    }

    public class VideoComponent
    {
        [JsonPropertyName("link")]
        public string? Link { get; set; }
    }


#nullable disable
    public partial class Language
    {
        [JsonPropertyName("code")]
        public string Code { get; set; } = "es";

        [JsonPropertyName("policy")]
        public string Policy { get; set; } = "deterministic";
    }

}
