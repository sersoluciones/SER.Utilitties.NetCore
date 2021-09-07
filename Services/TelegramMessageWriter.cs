using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RestSharp;
using SER.Utilitties.NetCore.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
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

            var model = new TelegramViewModel
            {
                ChatId = channelId,
                ParseMode = mode,
                Text = message,
            };
            await MakePostClientRequest(model, token);
            //Execute(MakePostRequest(model, token));
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
                _logger.LogInformation($" ----------- StatusCode {response.StatusCode}");
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


        private async Task<bool> MakePostClientRequest(TelegramViewModel model, string token)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            var jsonString = JsonSerializer.Serialize(model, options);
            _logger.LogInformation($"Request:\n{jsonString}");
            _logger.LogInformation(BASE_URL + $"/bot{token}/sendMessage");

            using var client = new HttpClient();

            var response = await client.PostAsJsonAsync(BASE_URL + $"/bot{token}/sendMessage", model, options);
            try
            {
                //if (response.Content != null)
                //{
                //    string result = response.Content.ReadAsStringAsync().Result;
                //    _logger.LogInformation($"Response:\n{result}");
                //}
                _logger.LogInformation($" ------------- StatusCode {response.StatusCode}");
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