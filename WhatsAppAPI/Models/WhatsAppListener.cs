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

        /// <summary>Present when <see cref="Type"/> is <c>unsupported</c>: says which kind of message Cloud API could not render.</summary>
        [JsonPropertyName("unsupported")]
        public WUnsupported? Unsupported { get; set; }

        /// <summary>Why the message could not be processed, typically alongside <see cref="Unsupported"/>.</summary>
        [JsonPropertyName("errors")]
        public WStatusError[]? Errors { get; set; }

        /// <summary>Click-to-WhatsApp ad or post the user came from.</summary>
        [JsonPropertyName("referral")]
        public WReferral? Referral { get; set; }

        [JsonPropertyName("image")]
        public WMedia? Image { get; set; }

        [JsonPropertyName("audio")]
        public WMedia? Audio { get; set; }

        [JsonPropertyName("video")]
        public WMedia? Video { get; set; }

        [JsonPropertyName("document")]
        public WMedia? Document { get; set; }

        [JsonPropertyName("sticker")]
        public WMedia? Sticker { get; set; }

        /// <summary>The media object of an image, audio, video, document or sticker message.</summary>
        [JsonIgnore]
        public WMedia? Media => Image ?? Audio ?? Video ?? Document ?? Sticker;
    }

    /// <summary>
    /// A file the user sent. <see cref="Id"/> is exchanged for a download URL at <c>GET /{media-id}</c>, valid five
    /// minutes and fetched with the line's token; the id itself stays retrievable for seven days.
    /// </summary>
    public partial class WMedia
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("mime_type")]
        public string? MimeType { get; set; }

        /// <summary>Base64 SHA-256 of the file.</summary>
        [JsonPropertyName("sha256")]
        public string? Sha256 { get; set; }

        [JsonPropertyName("caption")]
        public string? Caption { get; set; }

        /// <summary>Original file name of a document.</summary>
        [JsonPropertyName("filename")]
        public string? Filename { get; set; }

        /// <summary>The audio is a voice note recorded in WhatsApp.</summary>
        [JsonPropertyName("voice")]
        public bool? Voice { get; set; }

        [JsonPropertyName("animated")]
        public bool? Animated { get; set; }
    }

    /// <summary>
    /// Kind of an unrenderable message. <c>hsm</c>, <c>interactive</c>, <c>button</c>, <c>list</c> and <c>product</c>
    /// arrive when the sender is itself a business on the API.
    /// </summary>
    public partial class WUnsupported
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }
    }

    public partial class WReferral
    {
        [JsonPropertyName("source_url")]
        public string? SourceUrl { get; set; }

        [JsonPropertyName("source_type")]
        public string? SourceType { get; set; }

        [JsonPropertyName("source_id")]
        public string? SourceId { get; set; }

        [JsonPropertyName("headline")]
        public string? Headline { get; set; }

        [JsonPropertyName("body")]
        public string? Body { get; set; }

        [JsonPropertyName("ctwa_clid")]
        public string? CtwaClid { get; set; }
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

        /// <summary>wamid of the message being replied to, when the user quoted it or tapped one of its buttons.</summary>
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        /// <summary>The message was forwarded (up to five times).</summary>
        [JsonPropertyName("forwarded")]
        public bool? Forwarded { get; set; }

        /// <summary>The message was forwarded more than five times.</summary>
        [JsonPropertyName("frequently_forwarded")]
        public bool? FrequentlyForwarded { get; set; }
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
