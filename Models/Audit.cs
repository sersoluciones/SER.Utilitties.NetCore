using SER.Utilitties.NetCore.Managers;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SER.Utilitties.NetCore.Models
{
    public class Audit
    {
        public int id { get; set; }
        public DateTime date { get; set; }

        public AudiState action { get; set; }

        [StringLength(20)]
        public string objeto { get; set; }

        [StringLength(20)]
        public string username { get; set; }

        [StringLength(20)]
        public string role { get; set; }

        [StringLength(2000)]
        public string json_browser { get; set; }

        [StringLength(2000)]
        public string json_request { get; set; }

        [Column(TypeName = "jsonb")]
        public string data { get; set; }

        [StringLength(60)]
        public string user_id { get; set; }
    }

    public class AuditBinding
    {
        public AudiState action { get; set; }
        public string objeto { get; set; }
        public Dictionary<string, string> extras { get; set; }
    }
}
