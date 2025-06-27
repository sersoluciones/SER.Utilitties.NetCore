using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SER.Utilitties.NetCore.Configuration
{
    public class SERRestOptions
    {
        public bool EnableAudit { get; set; }
        /// <summary>
        /// Gets or sets the concrete type of the <see cref="DbContextType"/> used by the
        /// Graphql auto reflection stores. If this property is not populated,
        /// an exception is thrown at runtime when trying to use the stores.
        /// </summary>
        public Type DbContextType { get; set; }
        public string ConnectionString { get; set; }
        public bool EnableCustomFilter { get; set; }
        public bool DebugMode { get; set; }
        public string NameCustomFilter { get; set; }
    }
}
