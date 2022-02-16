using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RestSharp;
using SER.Utilitties.NetCore.Utilities;
using SER.Utilitties.NetCore.Verifik.Models;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SER.Utilitties.NetCore.Verifik
{
    public class VerifikAPI
    {
        #region Atributes
        private static readonly string _baseUrl = "https://api.verifik.co/v2/";
        private readonly ILogger _logger;
        private RestClient _client;
        private readonly IConfiguration _config;
        private readonly string LOGIN_ENDPOINT = "auth/login";
        private string _phone = string.Empty;
        private string _password = string.Empty;
        private string _accessToken = string.Empty;

        public string AccessToken { get => _accessToken; set => _accessToken = value; }
        #endregion

        public VerifikAPI(ILogger<VerifikAPI> logger, IConfiguration config)
        {
            _config = config;
            _phone = _config["Verifik:phone"];
            _password = _config["Verifik:password"];
            _client = new RestClient(_baseUrl);
            _logger = logger;
        }

        /// <summary>
        /// https://docs.verifik.co/consulta-persona/consultar-nombre-ciudadano-o-extranjero
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public async Task<BaseResponse<PersonData>> FetchPersonAsync(PersonDataViewModel model)
        {
            return await Execute<BaseResponse<PersonData>>(await MakeGetRequest(model, endPoint: "co/consultarNombres"));
        }

        /// <summary>
        /// fetch info de una empresa
        /// https://docs.verifik.co/rues/por-nit
        /// </summary>
        /// <param name="nit"></param>
        /// <returns></returns>
        public async Task<BaseResponse<CompanyData>> FetchCompanyAsync(string nit)
        {
            return await Execute<BaseResponse<CompanyData>>(await MakeGetRequest(new Dictionary<string, string>()
            {
                { "documentNumber", nit }
            }, endPoint: "co/rues/consultarEmpresaPorNit"));
        }

        private async Task<RestRequest> MakeGetRequest(dynamic model, string endPoint = "")
        {
            var request = new RestRequest(endPoint, Method.GET);
            await VerifyTokenAsync();
            request.AddHeader("authorization", string.Format("Bearer {0}", _accessToken));

            var jsonString = JsonSerializer.Serialize(model);
            var documentOptions = new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip
            };
            var document = JsonDocument.Parse(jsonString, documentOptions);

            foreach (JsonProperty property in document.RootElement.EnumerateObject())
            {
                request.AddParameter(property.Name, property.Value, ParameterType.QueryString);
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
            var jsonString = JsonSerializer.Serialize(model);            
            Console.WriteLine($" ---------------- BODY {jsonString} -----------------");
            request.AddJsonBody(jsonString);

            return request;
        }

        private async Task<T> Execute<T>(RestRequest request) where T : class
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
                    _logger.LogWarning(string.Format("VERIFIK API StatusCode {0} {1}", response.StatusCode, response.Content));
                    return JsonSerializer.Deserialize<T>(response.Content);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(string.Format("VERIFIK API {0}\n{1}", response.Content, e.ToString()));
                throw;
            }
        }

        #region login token

        private async Task VerifyTokenAsync()
        {
            if (string.IsNullOrEmpty(_accessToken))
            {
                await GetAccessToken();
                return;
            }
            var handler = new JwtSecurityTokenHandler();
            var token = handler.ReadJwtToken(_accessToken);
            var exp = token.IssuedAt.AddHours(24);
            if (DateTime.Now.ToEpochTime() > exp.ToEpochTime())
            {
                await GetAccessToken();
            }
        }

        private async Task GetAccessToken()
        {
            var getToken = await Execute<LoginResponseVerifik>(MakePostRequest(new LoginVerifik()
            {
                Password = _password,
                Phone = _phone,
            }, endPoint: LOGIN_ENDPOINT));
            _accessToken = string.Empty;
            if (!string.IsNullOrEmpty(getToken.AccessToken))
            {
                _accessToken = getToken.AccessToken;
            }
        }

        #endregion


    }

}
