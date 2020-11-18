using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SER.Utilitties.NetCore.CPanel.Models
{
    public sealed class SealedResponseCPanel
    {
        public CPanelResponse Success;
        public CPanelResponseError Error;
    }
}
