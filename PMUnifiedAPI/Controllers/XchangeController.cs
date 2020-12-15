﻿using System;
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
        private string NetMqServerConnectionString = string.Empty;
        private string AerospikeServerIp = string.Empty;
        private int AerospikeServerPort = 0;
        private bool XchangeEnabled = false;

        public XchangeController(IOptions<PseudoMarketsConfig> appConfig)
        {
            config = appConfig;
            NetMqServerConnectionString = config.Value.NetMQServer;
            AerospikeServerIp = config.Value.AerospikeServerIP;
            AerospikeServerPort = config.Value.AerospikeServerPort;
            XchangeEnabled = config.Value.XchangeEnabled;
        }

        // GET: api/Xchange/
        [HttpGet]
        public string Get(int id)
        {
            return "PseudoXchange" + "\n" + "Enabled: " + XchangeEnabled;
        }

        [Route("LatestPrice/{symbol}")]
        [HttpGet]
        public async Task<ActionResult> GetLatestPrice(string symbol)
        {
            if (XchangeEnabled)
            {
                AerospikeClient client = new AerospikeClient(AerospikeServerIp, AerospikeServerPort);
                if (client.Connected)
                {
                    Key key = new Key("nsshared", "setXchangeEquities", symbol);
                    double price = -1;
                    Record record = client.Get(null, key);
                    if (record != null)
                    {
                        var priceBin = record.bins.FirstOrDefault();
                        price = (double)priceBin.Value;
                    }
                    else
                    {
                        symbol = "Invalid symbol";
                    }

                    XchangeQuote quote = new XchangeQuote()
                    {
                        Symbol = symbol,
                        Price = price,
                        Timestamp = DateTime.Now
                    };

                    return Ok(quote);

                }
                else
                {
                    return StatusCode(500);
                }
            }
            else
            {
                return StatusCode(403);
            }

        }

        // POST: api/Xchange/OrderEntry
        [Route("OrderEntry")]
        [HttpPost]
        public async Task<ActionResult> Post([FromBody] OrderEntry orderEntry)
        {
            if (XchangeEnabled)
            {
                string token = orderEntry.Token;

                using var client = new RequestSocket(NetMqServerConnectionString);  // connect

                XchangeOrder order = new XchangeOrder()
                {
                    Symbol = orderEntry.Symbol,
                    OrderType = orderEntry.OrderType,
                    Quantity = orderEntry.Quantity,
                    LowerLimitPrice = orderEntry.LowerLimitPrice,
                    UpperLimitPrice = orderEntry.UpperLimitPrice,
                    OrderFillSettings = orderEntry.OrderFillSettings,
                    OrderPricing = orderEntry.OrderPricing,
                    OrderRules = orderEntry.OrderRules,
                    OrderStatus = orderEntry.OrderStatus,
                    AccountId = orderEntry.AccountId
                };

                var serializedOrder = MessagePackSerializer.Serialize(order);

                client.SendFrame(serializedOrder);


                var message = client.ReceiveFrameBytes();

                var deserializedOrderEntryResult = MessagePackSerializer.Deserialize<OrderEntryResult>(message);

                return Ok(deserializedOrderEntryResult);
            }
            else
            {
                return StatusCode(403);
            }

        }

    }

}
