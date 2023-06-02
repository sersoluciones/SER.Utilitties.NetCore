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
        public async Task SendSmsAsync(string number, string? message = null, string templateName = "sending_code")
        {
            var model = new MsgWhatsApp
            {
                To = number,
                Template = new Template
                {
                    Name = templateName,
                    Language = new Language(),
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


        public async Task SendSmsAsync(string number, List<string> messages, string templateName)
        {
            var model = new MsgWhatsApp
            {
                To = number,
                Template = new Template
                {
                    Name = templateName,
                    Language = new Language(),
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


        public async Task SendSmsAsync(string number, List<Component> components, string templateName)
        {
            var model = new MsgWhatsApp
            {
                To = number,
                Template = new Template
                {
                    Name = templateName,
                    Language = new Language(),
                    Components = components.ToArray(),
                }
            };
            await Execute<ResponseWhatsApp>(MakePostRequest(model, endPoint: $"{_phoneNumberId}/messages"));
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
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    return JsonSerializer.Deserialize<T>(response.Content);
                }
                else
                {
                    _logger.LogWarning(string.Format("WhatsApp API StatusCode {0} {1}", response.StatusCode, response.Content));
                    return null;
                }
            }
            catch (Exception e)
            {
                _logger.LogError(string.Format("WhatsApp API {0}\n{1}", response.Content, e.ToString()));
                throw;
            }
        }


    }
#nullable disable
}
