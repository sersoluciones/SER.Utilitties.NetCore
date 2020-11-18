using Microsoft.AspNetCore.Mvc.RazorPages;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SER.Utilitties.NetCore.Utilities.Render.Models
{
    public class TemplateBindingModel : PageModel
    {
        public JToken Data { get; set; }

        public string Lang { get; set; }

        public string[] CSSPaths { get; set; }

        public List<string> Permissions { get; set; }
    }
}
