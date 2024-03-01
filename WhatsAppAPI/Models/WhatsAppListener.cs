using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace SER.Utilitties.NetCore.WhatsAppAPI.Models
{
#nullable enable
    public class WhatsAppVerify
    {
        [JsonPropertyName("hub.mode")]
        [FromQuery(Name = "hub.mode")]
        public string? Mode { get; set; }

        [JsonPropertyName("hub.challenge")]
        [FromQuery(Name = "hub.challenge")]
        public string? Challenge { get; set; }

        [JsonPropertyName("hub.verify_token")]
        [FromQuery(Name = "hub.verify_token")]
        public string? Token { get; set; }
    }

    public class WhatsAppVerifyResponse
    {
        [JsonPropertyName("hub.challenge")]
        public string? Challenge { get; set; }
    }


    public class WhatsAppListener
    {
        [JsonPropertyName("object")]
        public string? Object { get; set; }

        [JsonPropertyName("entry")]
        public Entry[]? Entry { get; set; }
    }

    public partial class Entry
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("changes")]
        public Change[]? Changes { get; set; }
    }

    public partial class Change
    {
        [JsonPropertyName("value")]
        public Value? Value { get; set; }

        [JsonPropertyName("field")]
        public string? Field { get; set; }
    }

    public partial class Value
    {
        [JsonPropertyName("messaging_product")]
        public string? MessagingProduct { get; set; }

        [JsonPropertyName("metadata")]
        public object? Metadata { get; set; }

        [JsonPropertyName("contacts")]
        public object? Contacts { get; set; }

        [JsonPropertyName("messages")]
        public Message[]? Messages { get; set; }
    }

    public partial class Contact
    {
        [JsonPropertyName("profile")]
        public object? Profile { get; set; }

        /// <summary>
        /// numero de whastapp de la persona que envia
        /// </summary>
        [JsonPropertyName("wa_id")]
        public string? WaId { get; set; }
    }

    public partial class Profile
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    public partial class Message
    {
        [JsonPropertyName("from")]
        public string? From { get; set; }

        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("timestamp")]
        public string? Timestamp { get; set; }

        [JsonPropertyName("text")]
        public object? Text { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("button")]
        public WButton? Button { get; set; }

        [JsonPropertyName("context")]
        public WContext? Context { get; set; }
    }

    public partial class Text
    {
        [JsonPropertyName("body")]
        public string? Body { get; set; }
    }

    public partial class Metadata
    {
        [JsonPropertyName("display_phone_number")]
        public string? DisplayPhoneNumber { get; set; }

        [JsonPropertyName("phone_number_id")]
        public string? PhoneNumberId { get; set; }
    }

    public partial class WButton
    {
        [JsonPropertyName("payload")]
        public string? Payload { get; set; }

        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }

    public partial class WContext
    {
        [JsonPropertyName("from")]
        public string? From { get; set; }

        [JsonPropertyName("id")]
        public string? Id { get; set; }
    }
#nullable disable
}
