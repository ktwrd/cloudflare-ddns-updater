using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudflareDDNS
{
    public class DDNSConfig
    {
        public string PreviousIPAddress = "";
        public string Token = "";
        public string Email = "";
        public DDNSConfigZone[] Zones = Array.Empty<DDNSConfigZone>();
    }
    public class DDNSConfigZone
    {
        public string? Id = null;
        public string Name = "";
        public DDNSConfigRecord[] Records = Array.Empty<DDNSConfigRecord>();
    }
    public class DDNSConfigRecord
    {
        public string? Id = null;
        public string Name = "@";
        public string Type = "A";
        public bool DDNSUpdate = true;
        public bool Proxy = true;
        /// <summary>
        /// Substitute %1 for the new IP Address
        /// </summary>
        public string DDNSUpdateValue = "%1";
    }
}
