using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SER.Utilitties.NetCore.Models
{
    public class TelegramViewModel
    {
        [JsonPropertyName("chat_id")]
        public string ChatId { get; set; }

        [JsonPropertyName("parse_mode")]
        public string ParseMode { get; set; } = "html";

        [JsonPropertyName("text")]
        public string Text { get; set; }

        [JsonPropertyName("reply_markup")]
        public TelegramMarkup ReplyMarkup { get; set; } 
       
    }

    public class TelegramMarkup
    {
        [JsonPropertyName("inline_keyboard")]
        public TelegramKeyboard[][] InlineKeyboard { get; set; }
    }

    public class TelegramKeyboard
    {
        [JsonPropertyName("text")]
        public string Text { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }
    }
}
