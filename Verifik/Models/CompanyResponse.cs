using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SER.Utilitties.NetCore.Verifik.Models
{

    public class CompanyData
    {
        [JsonPropertyName("page")]
        public long Page { get; set; }

        [JsonPropertyName("codigo_error")]
        public string CodigoError { get; set; }

        [JsonPropertyName("mensaje_error")]
        public string MensajeError { get; set; }

        [JsonPropertyName("records")]
        public long Records { get; set; }

        [JsonPropertyName("rows")]
        public Row[] Rows { get; set; }
    }

    public partial class Row
    {
        [JsonPropertyName("identificacion")]
        public string Identificacion { get; set; }

        [JsonPropertyName("razon_social")]
        public string RazonSocial { get; set; }

        [JsonPropertyName("sigla")]
        public string Sigla { get; set; }

        [JsonPropertyName("categoria_matricula")]
        public string CategoriaMatricula { get; set; }

        [JsonPropertyName("municipio")]
        public string Municipio { get; set; }

        [JsonPropertyName("enlace")]
        public string Enlace { get; set; }

        [JsonPropertyName("estadoRM")]
        public string EstadoRm { get; set; }
    }
}
