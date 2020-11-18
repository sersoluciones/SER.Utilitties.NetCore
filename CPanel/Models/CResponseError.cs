using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SER.Utilitties.NetCore.CPanel.Models
{
    public class CResponseError
    {
        [JsonPropertyName("reason")]
        public string Reason { get; set; }

        [JsonPropertyName("result")]
        public string Result { get; set; }
    }
}
