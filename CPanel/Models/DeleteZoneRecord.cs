using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SER.Utilitties.NetCore.CPanel.Models
{
    public class DeleteZoneRecord : BaseZoneRecord
    {
        [JsonPropertyName("cpanel_jsonapi_func")]
        public string CpanelFunc { get; set; } = "remove_zone_record";

        [JsonPropertyName("line")]
        public int? Line { get; set; }
    }
}
