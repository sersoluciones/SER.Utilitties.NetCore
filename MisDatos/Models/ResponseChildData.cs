using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SER.Utilitties.NetCore.MisDatos.Models
{
    public class ResponseChildData
    {
        [JsonPropertyName("ti")]
        public string Ti { get; set; }

        [JsonPropertyName("primerApellido")]
        public string PrimerApellido { get; set; }

        [JsonPropertyName("segundoApellido")]
        public string SegundoApellido { get; set; }

        [JsonPropertyName("primerNombre")]
        public string PrimerNombre { get; set; }

        [JsonPropertyName("segundoNombre")]
        public string SegundoNombre { get; set; }

        [JsonPropertyName("sexo")]
        public string Sexo { get; set; }

        [JsonPropertyName("serialRegistoCivil")]
        public string SerialRegistoCivil { get; set; }

        [JsonPropertyName("fechaDeRegistro")]
        public string FechaDeRegistro { get; set; }

        [JsonPropertyName("ciudadDeNacimiento")]
        public string CiudadDeNacimiento { get; set; }

        [JsonPropertyName("tipoRegistroCivil")]
        public string TipoRegistroCivil { get; set; }
    }
}
