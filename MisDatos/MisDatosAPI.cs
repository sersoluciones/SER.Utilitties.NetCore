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
        private RestClient _client;
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

        /// <summary>
        /// https://documenter.getpostman.com/view/3987067/SWE56Jz1?version=latest#035aa9a8-c506-4d21-b53c-644b427deb6a
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public async Task<ResponsePersonalData> FetchPersonAsync(PersonData model)
        {
            return await Execute<ResponsePersonalData>(MakePostRequest(model, endPoint: "consultarNombres"));
        }

        /// <summary>
        /// https://documenter.getpostman.com/view/3987067/TVzUEwnN#b924e28c-353e-432e-8b01-be7b48242753
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public async Task<ResponseChildData> FetchChildAsync(PersonData model)
        {
            return await Execute<ResponseChildData>(MakePostRequest(model, endPoint: "registraduria/civilRegisty", baseUrl: "https://mdapi-microservices.dinamicadigital.cloud/api/" ));
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

        private RestRequest MakePostRequest(dynamic model, string endPoint = "", string baseUrl = "")
        {
            if (!string.IsNullOrEmpty(baseUrl))
            {
                _client = new RestClient(baseUrl);
            }
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
                _logger.LogError(string.Format("MIS DATOS API {0}\n{1}", response.Content, e.ToString()));
                throw;
            }
        }
    }
}
