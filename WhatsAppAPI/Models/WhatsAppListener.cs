using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;

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
        public Metadata? Metadata { get; set; }

        [JsonPropertyName("contacts")]
        public Contact[]? Contacts { get; set; }

        [JsonPropertyName("messages")]
        public Message[]? Messages { get; set; }

        /// <summary>
        /// Delivery receipts (sent, delivered, read, failed) of messages the business sent. A webhook carries
        /// either messages or statuses.
        /// </summary>
        [JsonPropertyName("statuses")]
        public WStatus[]? Statuses { get; set; }
    }

    public partial class Contact
    {
        [JsonPropertyName("profile")]
        public Profile? Profile { get; set; }

        /// <summary>
        /// Sender's phone number. Meta omits it for users with a username unless the number was in contact with
        /// the business in the last 30 days; <see cref="UserId"/> is always present.
        /// </summary>
        [JsonPropertyName("wa_id")]
        public string? WaId { get; set; }

        /// <summary>Business-scoped user id (BSUID), e.g. <c>CO.2208218207240616</c>.</summary>
        [JsonPropertyName("user_id")]
        public string? UserId { get; set; }

        [JsonPropertyName("parent_user_id")]
        public string? ParentUserId { get; set; }
    }

    public partial class Profile
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("username")]
        public string? Username { get; set; }
    }

    public partial class Message
    {
        /// <summary>Sender's phone number; omitted under the same conditions as <see cref="Contact.WaId"/>.</summary>
        [JsonPropertyName("from")]
        public string? From { get; set; }

        /// <summary>Sender's business-scoped user id (BSUID).</summary>
        [JsonPropertyName("from_user_id")]
        public string? FromUserId { get; set; }

        [JsonPropertyName("from_parent_user_id")]
        public string? FromParentUserId { get; set; }

        /// <summary>
        /// Stable sender identifier: the BSUID, or the phone number on payloads from before BSUIDs existed.
        /// Both are accepted by <see cref="MsgWhatsApp.SetRecipient"/> to reply.
        /// </summary>
        [JsonIgnore]
        public string? SenderId => FromUserId ?? From;

        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("timestamp")]
        public string? Timestamp { get; set; }

        [JsonPropertyName("text")]
        public Text? Text { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("button")]
        public WButton? Button { get; set; }

        [JsonPropertyName("interactive")]
        public WRInteractive? Interactive { get; set; }

        [JsonPropertyName("context")]
        public WContext? Context { get; set; }

        /// <summary>Present when <see cref="Type"/> is <c>system</c>, such as a user changing number or BSUID.</summary>
        [JsonPropertyName("system")]
        public WSystem? System { get; set; }
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

    /// <summary>The user's answer to an interactive message: a reply button or a list row.</summary>
    public partial class WRInteractive
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("list_reply")]
        public WListReply? ListReply { get; set; }

        [JsonPropertyName("button_reply")]
        public WButtonReply? ButtonReply { get; set; }
    }

    public partial class WListReply
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }
    }

    public partial class WButtonReply
    {
        [JsonPropertyName("id")]
        public string? Payload { get; set; }

        [JsonPropertyName("title")]
        public string? Text { get; set; }
    }

    public partial class WContext
    {
        [JsonPropertyName("from")]
        public string? From { get; set; }

        [JsonPropertyName("id")]
        public string? Id { get; set; }
    }

    /// <summary>
    /// System notice about the sender. <c>user_changed_number</c> and <c>user_changed_user_id</c> mean the BSUID
    /// changed from <see cref="PreviousUserId"/> to <see cref="UserId"/>.
    /// </summary>
    public partial class WSystem
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("body")]
        public string? Body { get; set; }

        [JsonPropertyName("user_id")]
        public string? UserId { get; set; }

        [JsonPropertyName("previous_user_id")]
        public string? PreviousUserId { get; set; }

        [JsonPropertyName("wa_id")]
        public string? WaId { get; set; }
    }

    public partial class WStatus
    {
        /// <summary>wamid of the message this receipt is about.</summary>
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        /// <summary><c>sent</c>, <c>delivered</c>, <c>read</c> or <c>failed</c>.</summary>
        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("timestamp")]
        public string? Timestamp { get; set; }

        [JsonPropertyName("recipient_id")]
        public string? RecipientId { get; set; }

        [JsonPropertyName("recipient_user_id")]
        public string? RecipientUserId { get; set; }

        [JsonPropertyName("errors")]
        public WStatusError[]? Errors { get; set; }
    }

    public partial class WStatusError
    {
        [JsonPropertyName("code")]
        public int? Code { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }
#nullable disable
}
