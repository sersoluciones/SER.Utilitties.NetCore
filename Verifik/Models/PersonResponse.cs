using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SER.Utilitties.NetCore.Verifik.Models
{
    public class PersonDataViewModel
    {
        [Required]
        [JsonPropertyName("documentType")]
        public string DocumentType { get; set; }
        [Required]
        [JsonPropertyName("documentNumber")]
        public string DocumentNumber { get; set; }
    }

    public class PersonData
    {
        [JsonPropertyName("documentType")]
        public string DocumentType { get; set; }

        [JsonPropertyName("documentNumber")]
        public string DocumentNumber { get; set; }

        [JsonPropertyName("fullName")]
        public string FullName { get; set; }

        [JsonPropertyName("firstName")]
        public string FirstName { get; set; }

        [JsonPropertyName("lastName")]
        public string LastName { get; set; }
    }

}
