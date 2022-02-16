using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SER.Utilitties.NetCore.Verifik.Models
{
    public class LoginVerifik
    {
        [JsonPropertyName("password")]
        public string Password { get; set; }
        [JsonPropertyName("phone")]
        public string Phone { get; set; }
    }
}
