using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SER.Utilitties.NetCore.CPanel.Models
{
    public class CPanelResultError
    {
        [JsonPropertyName("data")]
        public CResponseError Data { get; set; }

        [JsonPropertyName("event")]
        public CPanelEvent Event { get; set; }
    }
}
