using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RestSharp;
using SER.Utilitties.NetCore.CPanel.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SER.Utilitties.NetCore.Services
{
    public class CPanelRest
    {
        #region Atributes
        private string _baseUrl;
        private string _domain;
        private string _address;
        private readonly ILogger _logger;
        private readonly RestClient _client;
        private readonly IConfiguration _config;
        private string _auth = string.Empty;
        #endregion

        public CPanelRest(ILogger<CPanelRest> logger, IConfiguration config)
        {
            _baseUrl = _config["CPanelAPI:base_url"];
            _domain = _config["CPanelAPI:domain"];
            _address = _config["CPanelAPI:address"];
            _auth = $"cpanel {_config["CPanelAPI:username"]}:{_config["CPanelAPI:p_key"]}";
            _client = new RestClient(_baseUrl);
            _logger = logger;
            _config = config;
        }

        public async Task<SealedResponseCPanel> AddZone(AddZoneRecord model)
        {
            var sealesRes = await FetchZone(model);

            if (sealesRes.Success != null)
            {
                if (sealesRes.Success.CPanelResult.Data.Count > 0)
                    Console.WriteLine(string.Format("name: {0}", sealesRes.Success.CPanelResult.Data.FirstOrDefault()?.Name));
                else
                    sealesRes = await Execute(MakeGetRequest(model));
            }
            return sealesRes;
        }

        public async Task<SealedResponseCPanel> DeleteZone(DeleteZoneRecord model)
        {
            var sealesRes = await FetchZone(model);
            if (sealesRes.Success != null)
            {
                if (sealesRes.Success.CPanelResult.Data.Count > 0)
                {
                    model.Line = sealesRes.Success.CPanelResult.Data.FirstOrDefault()?.Line;
                    sealesRes = await Execute(MakeGetRequest(model));
                }
            }
            return sealesRes;
        }

        private async Task<SealedResponseCPanel> FetchZone(AddZoneRecord model)
        {
            var fetchModel = new FetchZoneRecord();
            foreach (var propertyInfo in typeof(BaseZoneRecord).GetProperties())
            {
                var currentValue = propertyInfo.GetValue(model);
                if (propertyInfo.Name != "Ttl")
                {
                    propertyInfo.SetValue(fetchModel, currentValue, null);
                }
            }
            var domainToSend = string.IsNullOrEmpty(model.Domain) ? _domain : model.Domain;
            fetchModel.Name = $"{model.Name}.{domainToSend}.";
            return await Execute(MakeGetRequest(fetchModel));
        }

        private async Task<SealedResponseCPanel> FetchZone(DeleteZoneRecord model)
        {
            var fetchModel = new FetchZoneRecord();
            var domainToSend = string.IsNullOrEmpty(model.Domain) ? _domain : model.Domain;
            fetchModel.Domain = domainToSend;
            fetchModel.Address = string.IsNullOrEmpty(model.Address) ? _address : model.Address;
            fetchModel.Name = $"{model.Name}.{domainToSend}.";
            return await Execute(MakeGetRequest(fetchModel));
        }

        private RestRequest MakeGetRequest(dynamic model, string endPoint = "json-api/cpanel")
        {
            var jsonString = JsonSerializer.Serialize(model);
            var documentOptions = new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip
            };
            var document = JsonDocument.Parse(jsonString, documentOptions);
            Console.WriteLine(jsonString);
            var request = new RestRequest(endPoint, Method.GET)
            {
                RequestFormat = DataFormat.Json
            };
            request.AddHeader("authorization", _auth);

            foreach (JsonProperty property in document.RootElement.EnumerateObject())
            {
                request.AddParameter(property.Name, property.Value, ParameterType.QueryString);
            }
            //var list = string.Join(", ", request.Parameters);
            //Console.WriteLine($"Params Query {list}");
            return request;
        }

        private async Task<SealedResponseCPanel> Execute(RestRequest request)
        {
            var response = await _client.ExecuteAsync(request);
            try
            {
                var res = JsonSerializer.Deserialize<CPanelResponse>(response.Content);
                var sealedResponse = new SealedResponseCPanel
                {
                    Success = res
                };
                return sealedResponse;
            }
            catch (Exception e)
            {
                _logger.LogError(e.ToString());
                var res = JsonSerializer.Deserialize<CPanelResponseError>(response.Content);
                Console.WriteLine(string.Format("Result: {0}", res.CPanelResult.Data.Result));
                var sealedResponse = new SealedResponseCPanel
                {
                    Error = res
                };
                return sealedResponse;
            }
        }
    }
}
