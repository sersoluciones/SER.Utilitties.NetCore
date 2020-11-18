using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SER.Utilitties.NetCore.CPanel.Models
{
    public class AddZoneRecord : BaseZoneRecord
    {
        [JsonPropertyName("cpanel_jsonapi_func")]
        public string CpanelFunc { get; set; } = "add_zone_record";

        [JsonPropertyName("ttl")]
        public string Ttl { get; set; } = "86400";
    }

}
