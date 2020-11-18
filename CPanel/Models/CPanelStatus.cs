using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SER.Utilitties.NetCore.CPanel.Models
{
    public class CPanelStatus
    {
        [JsonPropertyName("statusmsg")]
        public string StatusMsg { get; set; }
        [JsonPropertyName("status")]
        public int Status { get; set; }
        [JsonPropertyName("newserial")]
        public int NewSerial { get; set; }
    }
}
