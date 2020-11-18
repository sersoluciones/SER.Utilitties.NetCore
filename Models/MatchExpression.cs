using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SER.Utilitties.NetCore.Models
{
    public class MatchExpression
    {
        public List<Regex> Regexes { get; set; }

        public Action<Match, object> Action { get; set; }
    }
}
