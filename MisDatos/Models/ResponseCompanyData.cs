using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SER.Utilitties.NetCore.MisDatos.Models
{
    public class ResponseCompanyData : ResponseMisDatos
    {
        [JsonPropertyName("data")]
        public CompanyData[] Data { get; set; }
    }

    public class CompanyData
    {
        [JsonPropertyName("categoriaMatricula")]
        public string CategoriaMatricula { get; set; }

        [JsonPropertyName("claseIdentificacion")]
        public string ClaseIdentificacion { get; set; }

        [JsonPropertyName("codigoCamara")]
        public string CodigoCamara { get; set; }

        [JsonPropertyName("codigoEstado")]
        public string CodigoEstado { get; set; }

        [JsonPropertyName("identificacion")]
        public string Identificacion { get; set; }

        [JsonPropertyName("estado")]
        public string Estado { get; set; }

        [JsonPropertyName("numeroMatricula")]
        public string NumeroMatricula { get; set; }

        [JsonPropertyName("nombreCamara")]
        public string NombreCamara { get; set; }

        [JsonPropertyName("organizacionJuridica")]
        public string OrganizacionJuridica { get; set; }

        [JsonPropertyName("razonSocial")]
        public string RazonSocial { get; set; }

        [JsonPropertyName("sigla")]
        public string Sigla { get; set; }

        [JsonPropertyName("tipoEmpresa")]
        public string TipoEmpresa { get; set; }

        [JsonPropertyName("ciiu")]
        public Ciiu Ciiu { get; set; }
    }

    public class Ciiu
    {
        [JsonPropertyName("code")]
        public string Code { get; set; }
        [JsonPropertyName("activity")]
        public string Activity { get; set; }
    }

}
