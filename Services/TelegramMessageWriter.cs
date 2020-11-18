using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace SER.Utilitties.NetCore.Services
{
    public class TelegramMessageWriter
    {
        private IConfiguration _config;

        public TelegramMessageWriter(IConfiguration config)
        {
            _config = config;
        }

        public async Task WriteMessage(string message, string channel)
        {
            string Token = _config.GetSection("Telegram").GetSection("Token").Value;
            string Channel = _config.GetSection("Telegram").GetSection("Channels").GetSection(channel).Value;

            HttpClient client = new HttpClient();
            await client.GetAsync($"https://api.telegram.org/bot{ Token }/sendMessage?chat_id={ Channel }&parse_mode=Markdown&text={ message }");
        }
    }
}