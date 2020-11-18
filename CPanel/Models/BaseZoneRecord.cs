using SER.Utilitties.NetCore.Utilities.CustomAttributes;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SER.Utilitties.NetCore.CPanel.Models
{
    public class BaseZoneRecord // : IValidatableObject
    {
        [JsonPropertyName("cpanel_jsonapi_module")]
        public string CpanelModule { get; set; } = "ZoneEdit";
        [JsonPropertyName("domain")]
        public string Domain { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; } = "A";

        [JsonPropertyName("address")]
        [IpAddress]
        public string Address { get; set; }

        [JsonPropertyName("name")]
        [Required]
        public string Name { get; set; }

        //public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        //{
        //    const string regexPattern = @"^([\d]{1,3}\.){3}[\d]{1,3}$";
        //    var regex = new Regex(regexPattern);

        //    if (!regex.IsMatch(Address))
        //        yield return new ValidationResult("IP address format is invalid", new[] { "Address" });
        //}
    }
}
