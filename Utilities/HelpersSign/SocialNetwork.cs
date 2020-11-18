using Google.Apis.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SER.Utilitties.NetCore.Utilities.HelpersSign
{
    public static class SocialNetwork
    {
        #region validate JWT open id library
        public static async Task<string> ValidateJWTwithOpenId(string token, string audience)
        {
            IdentityModelEventSource.ShowPII = true;
            JwtSecurityToken jwt = new JwtSecurityToken(token);
            var issuer = jwt.Payload.Iss;

            var configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>($"{issuer}/.well-known/openid-configuration",
                new OpenIdConnectConfigurationRetriever());
            OpenIdConnectConfiguration openIdConfig = await configurationManager.GetConfigurationAsync(CancellationToken.None);

            TokenValidationParameters validationParameters =
                new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateLifetime = true,
                    IssuerSigningKeys = openIdConfig.SigningKeys
                };

            JwtSecurityTokenHandler handler = new JwtSecurityTokenHandler();
            try
            {
                var claimsPrincipal = handler.ValidateToken(token, validationParameters, out SecurityToken validatedToken);
                //JwtSecurityToken jwt = new JwtSecurityToken(token);
                var validPayload = jwt.Payload;
                var providerKey = Guid.Parse(validPayload.FirstOrDefault(c => c.Key == "oid").Value.ToString())
                    .ToString("N").TrimStart(new char[] { '0' });
                return providerKey;
            }
            catch (SecurityTokenValidationException ex)
            {
                // Validation failed
                Console.WriteLine(ex.ToString());
            }
            return null;
        }
        #endregion

        public static async Task<TUser> ValidateMSToken<TUser>(UserManager<TUser> userManager, string token, string audience,
            string companyId = null, bool getUser = false)
            where TUser : class
        {
            try
            {
                var providerKey = await ValidateJWTwithOpenId(token, audience);
                if (providerKey != null)
                {
                    JwtSecurityToken jwt = new JwtSecurityToken(token);
                    var validPayload = jwt.Payload;
                    var username = validPayload.FirstOrDefault(x => x.Key == UtilConstants.Username).Value.ToString();
                    var name = validPayload.FirstOrDefault(c => c.Key == UtilConstants.Name).Value.ToString();
                    return await GetUser(userManager, providerKey, "Microsoft", username, name, companyId: companyId, getUser: getUser);
                }
                else
                {
                    throw new InvalidTokenException(nameof(InvalidJwtException));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                throw new InvalidTokenException(nameof(InvalidJwtException));
            }

        }


        public static async Task<TUser> ValidateGoogleToken<TUser>(UserManager<TUser> userManager, string token, string audience,
           string companyId = null, bool getUser = false)
            where TUser : class
        {
            var validation = new GoogleJsonWebSignature.ValidationSettings()
            {
                Audience = new string[] { audience }
            };

            try
            {
                var validPayload = await GoogleJsonWebSignature.ValidateAsync(token, validation);
                if (validPayload != null)
                {
                    var email = validPayload.Email;
                    string providerKey = validPayload.Subject;
                    return await GetUser(userManager, providerKey, "Google", email, validPayload.GivenName, lastName: validPayload.FamilyName,
                        photo: validPayload.Picture, getUser: getUser);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                throw new InvalidTokenException(nameof(InvalidJwtException));
            }
            return null;
        }

        public static async Task<TUser> ValidateFacebookToken<TUser>(UserManager<TUser> userManager, string token,
            string companyId = null, bool getUser = false)
            where TUser : class
        {
            var jObjFB = await GetProfileFromFB(token);
            if (jObjFB == null || !jObjFB.ContainsKey("email"))
            {
                throw new InvalidTokenException(nameof(InvalidJwtException));
            }
            var email = jObjFB.GetValue("email").ToString();
            string providerKey = jObjFB.GetValue("id").ToString();
            var user = await GetUser(userManager, providerKey, "Facebook", email, jObjFB.GetValue("first_name").ToString(),
                lastName: jObjFB.GetValue("last_name").ToString(), photo: ((JObject)jObjFB["picture"]["data"]).GetValue("url").ToString(),
                getUser: getUser);
            return user;
        }

        #region request token facebook
        public static async Task<JObject> GetProfileFromFB(string access_token)
        {
            var url = $"https://graph.facebook.com/v5.0/me?fields=id,name,last_name,first_name,email,picture&access_token={access_token}";
            try
            {
                using (var httpClient = new HttpClient())
                {
                    HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, url);
                    HttpResponseMessage httpResponseMessage = await httpClient.SendAsync(req);
                    httpResponseMessage.EnsureSuccessStatusCode();
                    HttpContent httpContent = httpResponseMessage.Content;
                    var responseString = JObject.Parse(await httpContent.ReadAsStringAsync());
                    return responseString;
                };
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                return null;
            }
        }

        #endregion

        #region get user

        public static async Task<TUser> GetUser<TUser>(UserManager<TUser> userManager,
            string providerKey, string loginProvider, string email, string name, string lastName = null,
            string photo = null, bool getUser = false, string companyId = null)
            where TUser : class
        {
            var user = await userManager.FindByLoginAsync(loginProvider, providerKey);
            if (user == null)
            {
                if (!string.IsNullOrEmpty(companyId))
                {
                    var userName = string.Format("{0}_{1}", companyId, email);
                    user = await userManager.FindByNameAsync(userName);
                }
                else
                    user = await userManager.FindByNameAsync(email);

                if (user == null)
                {
                    if (getUser)
                    {
                        user = (TUser)Activator.CreateInstance(typeof(TUser));
                        IDictionary<string, object> expandoUser = new ExpandoObject();
                        expandoUser.Add("UserName", email);
                        expandoUser.Add("Email", email);
                        expandoUser.Add("Name", name);
                        expandoUser.Add("LastName", lastName ?? null);
                        expandoUser.Add("EmailConfirmed", true);
                        expandoUser.Add("IsActive", true);
                        expandoUser.Add("ProviderKey", providerKey);
                        foreach (var key in expandoUser.Keys)
                        {
                            var propertyInfo = typeof(TUser).GetProperty(key);
                            propertyInfo.SetValue(user, expandoUser[key]);
                        }
                        return user;
                    }
                    return null;
                }
                if (user != null)
                {
                    await userManager.AddLoginAsync(user, new UserLoginInfo(
                                loginProvider, providerKey, loginProvider));
                }

            }
            return user;
        }


        #endregion


    }
}