using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SER.Utilitties.NetCore.Utilities.Render
{
    public interface IViewRenderService
    {
        Task<string> RenderToStringAsync<T>(string viewName, T model) where T : PageModel;
    }
}
