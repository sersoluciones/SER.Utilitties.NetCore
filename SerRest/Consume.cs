using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace SER.Utilitties.NetCore.SerRest
{
    public class Consume
    {
        public async Task<string> GetNitRUES(string NitNumber)
        {
            using var client = new HttpClient();
            try
            {
                var formContent = new FormUrlEncodedContent(new[]
                {
                        new KeyValuePair<string, string>("txtNIT", NitNumber),
                    });

                var response = await client.PostAsync("https://www.rues.org.co/RM/ConsultaNIT_json", formContent);
                response.EnsureSuccessStatusCode();
                return response.Content.ReadAsStringAsync().Result;

            }
            catch (HttpRequestException e)
            {
                Console.WriteLine(e.ToString());
                return null;
            }
        }

    }
}