using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RestSharp;
using SER.Utilitties.NetCore.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SER.Utilitties.NetCore.Services
{
    public class TelegramMessageWriter
    {
        private readonly IConfiguration _config;
        private readonly RestClient _client;
        private const string BASE_URL = "https://api.telegram.org";
        private readonly ILogger _logger;

        public TelegramMessageWriter(IConfiguration config, ILoggerFactory loggerFactory)
        {
            _config = config;
            _client = new RestClient(BASE_URL);
            _logger = loggerFactory.CreateLogger("TelegramMessageWriter");
        }

        public async Task WriteMessage(string message, string channel, string mode = "html")
        {
            string token = _config.GetSection("Telegram").GetSection("Token").Value;
            string channelId = _config.GetSection("Telegram").GetSection("Channels").GetSection(channel).Value;

            await Execute(MakePostRequest(new TelegramViewModel
            {
                ChatId = channelId,
                ParseMode = mode,
                Text = message,
            }, token));
        }

        private RestRequest MakePostRequest(TelegramViewModel model, string token)
        {
            var request = new RestRequest($"/bot{token}/sendMessage", Method.POST)
            {
                RequestFormat = DataFormat.Json,
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            var jsonString = JsonSerializer.Serialize(model, options);
            _logger.LogInformation($"Request:\n{jsonString}");

            request.AddJsonBody(jsonString);
            return request;
        }

        private async Task<bool> Execute(RestRequest request)
        {
            var response = await _client.ExecuteAsync(request);
            try
            {
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                    return true;
                return false;
            }
            catch (Exception e)
            {
                _logger.LogError(e.ToString());
                return false;
            }
        }
    }
}