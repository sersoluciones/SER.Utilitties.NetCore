using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SER.Utilitties.NetCore
{
    public class PagedResultBase
    {
        public int current_page { get; set; }
        public int page_count { get; set; }
        public int page_size { get; set; }
        public int row_count { get; set; }

        public int first_row_on_page
        {

            get { return (current_page - 1) * page_size + 1; }
        }

        public int last_row_on_page
        {
            get { return Math.Min(current_page * page_size, row_count); }
        }

        public bool has_previous_page
        {
            get
            {
                return (current_page > 1);
            }
        }

        public bool has_next_page
        {
            get
            {
                return (current_page < page_count);
            }
        }
    }

    public class PagedResult<T> : PagedResultBase where T : class
    {
        public List<T> results { get; set; }
    }
}
