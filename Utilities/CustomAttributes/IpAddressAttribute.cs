using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SER.Utilitties.NetCore.Utilities.CustomAttributes
{
    public class IpAddressAttribute : RegularExpressionAttribute
    {
        public IpAddressAttribute()
        : base(@"^([\d]{1,3}\.){3}[\d]{1,3}$")
        { }

        public override bool IsValid(object value)
        {
            if (!base.IsValid(value))
                return false;

            string ipValue = value as string;
            if (IsIpAddressValid(ipValue))
                return true;

            return false;
        }

        private bool IsIpAddressValid(string ipAddress)
        {
            if (string.IsNullOrEmpty(ipAddress))
                return false;

            string[] values = ipAddress.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string token in values)
            {
                if (!byte.TryParse(token, out byte ipByteValue))
                    return false;
            }

            return true;
        }

        public override string FormatErrorMessage(string name)
        {
            return string.Format("The '{0}' field has an invalid format.", name);
        }
    }
}