using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace SER.Utilitties.NetCore.Services
{
    public class SlackMessageWriter
    {
        private IConfiguration _config;

        public SlackMessageWriter(IConfiguration config)
        {
            _config = config;
        }

        public async Task WriteMessage(string message, string channel)
        {
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _config.GetSection("Slack").GetSection("Token").Value);

            var postObject = new { channel, text = message };
            var json = JsonConvert.SerializeObject(postObject);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            await client.PostAsync("https://slack.com/api/chat.postMessage", content);
        }
    }
}