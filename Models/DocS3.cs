using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SER.Utilitties.NetCore.Models
{
    public interface DocS3
    {
        public string photos { get; set; }
    }

    public class DocS3Binding
    {
        [Required]
        public JObject docs_json { get; set; }
    }
}
