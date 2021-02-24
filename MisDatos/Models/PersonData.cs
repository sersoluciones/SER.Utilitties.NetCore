using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SER.Utilitties.NetCore.MisDatos.Models
{
    public class PersonData
    {
        [JsonPropertyName("documentType")]
        public string DocumentType { get; set; }
        [JsonPropertyName("documentNumber")]
        public string DocumentNumber { get; set; }
    }
}
