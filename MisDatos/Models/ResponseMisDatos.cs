using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SER.Utilitties.NetCore.MisDatos.Models
{
    public class ResponseMisDatos
    {
        [JsonPropertyName("statusCode")]
        public int StatusCode { get; set; }

        [JsonPropertyName("statusMessage")]
        public string StatusMessage { get; set; }

        [JsonPropertyName("statusDescription")]
        public string StatusDescription { get; set; }

        //[JsonPropertyName("devErrorDescription")]
        //public object DevErrorDescription { get; set; }
    }
}
