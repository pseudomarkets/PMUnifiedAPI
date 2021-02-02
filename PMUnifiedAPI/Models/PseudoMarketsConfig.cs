using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/*
 * Pseudo Markets Unified Web API
 * Service configuration model
 * Author: Shravan Jambukesan <shravan@shravanj.com>
 * (c) 2019 - 2020 Pseudo Markets
 */

namespace PMUnifiedAPI.Models
{
    public class PseudoMarketsConfig
    {
        public string AppBaseUrl { get; set; }
        public string TokenSecretKey { get; set; }
        public string AppVersion { get; set; }
        public string Environment { get; set; }
        public string ServerId { get; set; }
        public bool DataSyncEnabled { get; set; }
        public string DataSyncTargetDb { get; set; }
        public string NetMQServer { get; set; }
        public string AerospikeServerIP { get; set; }
        public int AerospikeServerPort { get; set; } 
        public bool XchangeEnabled { get; set; }
        public string TokenIssuer { get; set; }
        public string PerformanceReportingApiUrl { get; set; }
        public string InternalServiceUsername { get; set; }
        public string InternalServicePassword { get; set; }
        public string MarketDataServiceUrl { get; set; }
    }
}
