using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RestSharp;
using SER.Utilitties.NetCore.CPanel.Models;
using SER.Utilitties.NetCore.MisDatos.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SER.Utilitties.NetCore.MisDatos
{
    public class MisDatosAPI
    {
        #region Atributes
        private string _baseUrl;
        private readonly ILogger _logger;
        private readonly RestClient _client;
        private readonly IConfiguration _config;
        private string _auth = string.Empty;
        #endregion

        public MisDatosAPI(ILogger<MisDatosAPI> logger, IConfiguration config)
        {
            _config = config;
            _baseUrl = _config["MisDatos:base_url"];
            _auth = _config["MisDatos:api_key"];
            _client = new RestClient(_baseUrl);
            _logger = logger;
        }

        public async Task<ResponsePersonalData> FetchPersonAsync(PersonData model)
        {
            return await Execute<ResponsePersonalData>(MakePostRequest(model, endPoint: "consultarNombres"));
        }

        public async Task<ResponseCompanyData> FetchCompanyAsync(string nit)
        {
            return await Execute<ResponseCompanyData>(MakePostRequest(new Dictionary<string, string>()
            {
                { "nit", nit }
            }, endPoint: "rues/consultarEmpresaPorNit"));
        }

        private RestRequest MakeGetRequest(dynamic model, string endPoint = "")
        {
            var request = new RestRequest(endPoint, Method.GET);
            request.AddHeader("authorization", _auth);

            if (model.GetType() != typeof(string))
            {
                var jsonString = JsonSerializer.Serialize(model);
                var documentOptions = new JsonDocumentOptions
                {
                    CommentHandling = JsonCommentHandling.Skip
                };
                var document = JsonDocument.Parse(jsonString, documentOptions);

                foreach (JsonProperty property in document.RootElement.EnumerateObject())
                {
                    request.AddParameter(property.Name, property.Value, ParameterType.GetOrPost);
                }
            }
            else if (model is string)
            {
                request.AddParameter(model, model, ParameterType.GetOrPost);
            }
            return request;
        }

        private RestRequest MakePostRequest(dynamic model, string endPoint = "")
        {
            var request = new RestRequest(endPoint, Method.POST);
            request.AddHeader("Authorization", _auth);
            var jsonString = JsonSerializer.Serialize(model);
            var documentOptions = new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip
            };
            var document = JsonDocument.Parse(jsonString, documentOptions);

            foreach (JsonProperty property in document.RootElement.EnumerateObject())
            {
                request.AddParameter(property.Name, property.Value, ParameterType.GetOrPost);
            }

            return request;
        }

        private async Task<T> Execute<T>(RestRequest request) where T : class
        {
            var response = await _client.ExecuteAsync(request);
            try
            {
                return JsonSerializer.Deserialize<T>(response.Content);
            }
            catch (Exception e)
            {
                _logger.LogError(e.ToString());
                throw;
            }
        }
    }
}
