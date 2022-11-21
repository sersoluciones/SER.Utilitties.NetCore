using System.Text.Json.Serialization;

namespace SER.Utilitties.NetCore.WhatsAppAPI.Models
{
#nullable enable
    public class ResponseWhatsApp
    {
        [JsonPropertyName("error")]
        public object? Error { get; set; }

        [JsonPropertyName("messaging_product")]
        public string? MessagingProduct { get; set; }

        [JsonPropertyName("contacts")]
        public object? Contacts { get; set; }

        [JsonPropertyName("messages")]
        public object? Messages { get; set; }
    }
#nullable disable
}
