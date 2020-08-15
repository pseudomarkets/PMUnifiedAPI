using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PMUnifiedAPI.Models
{
    public class PseudoMarketsConfig
    {
        public string AppBaseUrl { get; set; }
        public string TokenSecretKey { get; set; }
        public string AppVersion { get; set; }
        public string Environment { get; set; }
        public string ServerId { get; set; }
    }
}
