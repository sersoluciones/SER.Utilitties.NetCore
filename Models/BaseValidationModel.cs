using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SER.Utilitties.NetCore.Models
{
    public class BaseValidationModel
    {
        [Required]
        public string Model { get; set; }
        [Required]
        public string Field { get; set; }
        [Required]
        public string Value { get; set; }
        public string Id { get; set; }
    }

    public class UserValidationModel : BaseValidationModel
    {
        public new string Model { get; set; } = "User";
    }
}
