using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SER.Utilitties.NetCore.CPanel.Models
{
    public class CPanelResponse
    {
        [JsonPropertyName("cpanelresult")]
        public CPanelResult CPanelResult { get; set; }
    }

    public class CPanelResponseError
    {
        [JsonPropertyName("cpanelresult")]
        public CPanelResultError CPanelResult { get; set; }
    }

    public class CPanelResult
    {
        [JsonPropertyName("data")]
        public List<CResponse> Data { get; set; }
    }

    public class CResponse
    {
        [JsonPropertyName("result")]
        public CPanelStatus Result { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }
        [JsonPropertyName("line")]
        public int Line { get; set; }
        [JsonPropertyName("address")]
        public string Address { get; set; }
    }
}
