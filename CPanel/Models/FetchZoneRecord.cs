using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SER.Utilitties.NetCore.CPanel.Models
{
    public class FetchZoneRecord : BaseZoneRecord
    {
        [JsonPropertyName("cpanel_jsonapi_func")]
        public string CpanelFunc { get; set; } = "fetchzone_records";

        [JsonPropertyName("customonly")]
        public string CustomOnly { get; set; } = "0";
    }
}
