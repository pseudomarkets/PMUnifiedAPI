using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aerospike.Client;
using MessagePack;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NetMQ;
using NetMQ.Sockets;
using PMCommonEntities.Models.PseudoXchange.OrderEntryModels;
using PMUnifiedAPI.Models;

/*
 * Pseudo Markets Unified Web API
 * PseudoXchange API
 * Author: Shravan Jambukesan <shravan@shravanj.com>
 * (c) 2019 - 2020 Pseudo Markets
 */

namespace PMUnifiedAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class XchangeController : ControllerBase
    {
        private readonly IOptions<PseudoMarketsConfig> config;
        private string _netMqServerConnectionString = string.Empty;
        private string _aerospikeServerIp = string.Empty;
        private int _aerospikeServerPort = 0;
        private readonly bool _xchangeEnabled = false;

        public XchangeController(IOptions<PseudoMarketsConfig> appConfig)
        {
            config = appConfig;
            _netMqServerConnectionString = config.Value.NetMQServer;
            _aerospikeServerIp = config.Value.AerospikeServerIP;
            _aerospikeServerPort = config.Value.AerospikeServerPort;
            _xchangeEnabled = config.Value.XchangeEnabled;
        }

        // GET: api/Xchange/
        [HttpGet]
        public string Get(int id)
        {
            return "PseudoXchange" + "\n" + "Enabled: " + _xchangeEnabled;
        }

        [Route("LatestPrice/{symbol}")]
        [HttpGet]
        public async Task<ActionResult> GetLatestPrice(string symbol)
        {
            // TODO: Consume PseudoXchange.Client once ready
            return StatusCode(403);
        }

        // POST: api/Xchange/OrderEntry
        [Route("OrderEntry")]
        [HttpPost]
        public async Task<ActionResult> Post(OrderEntry orderEntry)
        {
            // TODO: Consume PseudoXchange.Client once ready
            return StatusCode(403);
        }

    }

}
