using SER.Utilitties.NetCore.WhatsAppAPI.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RestSharp;
using System;
using System.Text.Json;
using System.Threading.Tasks;
using Parameter = SER.Utilitties.NetCore.WhatsAppAPI.Models.Parameter;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using RateLimiter;

namespace SER.Utilitties.NetCore.WhatsAppAPI
{
#nullable enable
    public class WhatsAppSender
    {
        #region Atributes
        private static readonly string _baseUrl = "https://graph.facebook.com/v15.0/";
        private readonly ILogger _logger;
        private RestClient _client;
        private readonly IConfiguration _config;
        private string _accessToken = string.Empty;
        private string _phoneNumberId = string.Empty;

        #endregion

        public WhatsAppSender(ILogger<WhatsAppSender> logger, IConfiguration config)
        {
            _config = config;
            _accessToken = _config["WhatsAppAPI:token"];
            _phoneNumberId = _config["WhatsAppAPI:phone_number_id"];
            _client = new RestClient(_baseUrl);
            _logger = logger;
        }



        /// <summary>
        /// /{_phoneNumberId}/messages
        /// </summary>
        /// <param name="number"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public async Task SendSmsAsync(string number, string? message = null, string templateName = "sending_code", string language = "es")
        {
            var model = new MsgWhatsApp
            {
                To = number,
                Template = new Template
                {
                    Name = templateName,
                    Language = new Language()
                    {
                        Code = language
                    },
                    Components = new Component[]
                    {
                        new Component
                        {
                            Parameters = message == null ? null : new Parameter[]
                            {
                                new Parameter
                                {
                                    Text = message,
                                }
                            }
                        }
                    }
                }
            };
            await Execute<ResponseWhatsApp>(MakePostRequest(model, endPoint: $"{_phoneNumberId}/messages"));
        }


        public async Task SendSmsAsync(string number, List<string> messages, string templateName, string language = "es")
        {
            var model = new MsgWhatsApp
            {
                To = number,
                Template = new Template
                {
                    Name = templateName,
                    Language = new Language()
                    {
                        Code = language
                    },
                    Components = new Component[]
                    {
                        new Component
                        {
                            Parameters = messages.Select(message => new Parameter
                            {
                                Text = message,
                            }).ToArray(),
                        }
                    }
                }
            };
            await Execute<ResponseWhatsApp>(MakePostRequest(model, endPoint: $"{_phoneNumberId}/messages"));
        }


        public async Task SendSmsAsync(string number, List<Component> components, string templateName, string language = "es")
        {
            var model = new MsgWhatsApp
            {
                To = number,
                Template = new Template
                {
                    Name = templateName,
                    Language = new Language()
                    {
                        Code = language
                    },
                    Components = components.ToArray(),
                }
            };
            await Execute<ResponseWhatsApp>(MakePostRequest(model, endPoint: $"{_phoneNumberId}/messages"));
        }


        /// <summary>
        /// source https://github.com/David-Desmaisons/RateLimiter
        /// </summary>
        /// <param name="msgs"></param>
        /// <returns></returns>
        public async Task SendingMultipleRequests(List<string> numbers, List<Component> components, string templateName, string language = "es")
        {
            var limit = 80; // limite de requests por minuto           
            var maxLimit = (int)Math.Ceiling(numbers.Count * 1.0 / limit * 1.0);
            //_logger.LogInformation($" -------------------- maxLimit {maxLimit} ----------------- ");
            int start = 0;
            int count = limit;
            // Create Time constraint: max 80 times by second
            var timeConstraint = TimeLimiter.GetFromMaxCountByInterval(limit, TimeSpan.FromSeconds(60));

            // Use it
            for (int i = 0; i < maxLimit; i++)
            {
                _logger.LogInformation($" -------------------- grupo index[{start * count}] {start * count} - {(start * count) + count} ------------------ ");

                var j = 1 + (start * count);
                var requestToSend = numbers.GetRange(start * count, (start * count) + count > numbers.Count ? numbers.Count - (start * count) : count);
                foreach (var number in requestToSend)
                {
                    //_logger.LogInformation($" -------------------- enviando request {j} ------------------ ");
                    await timeConstraint.Enqueue(() => SendSmsAsync(number, components, templateName, language).ConfigureAwait(false), CancellationToken.None);
                    j++;
                }


                start += 1;
            }

            _logger.LogInformation($" -------------------- finising job send requests ------------------ ");

        }

        private RestRequest MakePostRequest(dynamic model, string endPoint = "", string baseUrl = "")
        {
            if (!string.IsNullOrEmpty(baseUrl))
            {
                _client = new RestClient(baseUrl);
            }
            var request = new RestRequest(endPoint, Method.Post);
            request.AddHeader("authorization", string.Format("Bearer {0}", _accessToken));

            string jsonString = JsonSerializer.Serialize(model);
            Console.WriteLine($" ---------------- BODY {jsonString} -----------------");
            request.AddJsonBody(jsonString);

            return request;
        }

        private async Task<T?> Execute<T>(RestRequest request) where T : class
        {
            var response = await _client.ExecuteAsync(request);
            try
            {
                if (response.StatusCode == System.Net.HttpStatusCode.OK && !string.IsNullOrEmpty(response.Content))
                {
                    //_logger.LogInformation($"WhatsApp API StatusCode {response.StatusCode} {response.Content}");
                    return JsonSerializer.Deserialize<T>(response.Content);
                }
                else
                {
                    _logger.LogWarning($"WhatsApp API StatusCode {response.StatusCode} {response.Content}");
                    return null;
                }
            }
            catch (Exception e)
            {
                _logger.LogError($"WhatsApp API {response.Content}\n{e.ToString()}");
                throw;
            }
        }


    }
#nullable disable
}
