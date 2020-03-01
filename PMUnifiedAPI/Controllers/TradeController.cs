using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using PMUnifiedAPI.Models;

/*
 * Pseudo Markets Unified Web API
 * Trading API
 * Author: Shravan Jambukesan <shravan@shravanj.com>
 * (c) 2019 - 2020 Pseudo Markets
 */

namespace PMUnifiedAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TradeController : ControllerBase
    {
        private readonly PseudoMarketsDbContext _context;
        private string baseUrl = "https://localhost:44323";

        public TradeController(PseudoMarketsDbContext context)
        {
            _context = context;
        }

        // POST: /api/Trade/Execute
        [Route("Execute")]
        [HttpPost]
        public async Task<ActionResult> ExecuteTrade([FromBody] TradeExecInput input)
        {
            var transactionId = Guid.NewGuid().ToString();
            var userId = await _context.Tokens.Where(x => x.Token == input.Token).Select(x => x.UserID).FirstOrDefaultAsync();
            var user = await _context.Users.FirstOrDefaultAsync(x => x.Id == userId);
            var account = await _context.Accounts.FirstOrDefaultAsync(x => x.UserID == user.Id);

            var client = new HttpClient();
            var response = await client.GetAsync(baseUrl + "/api/Quotes/LatestPrice/" + input.Symbol);
            string jsonResponse = await response.Content.ReadAsStringAsync();
            var jsonObj = JsonConvert.DeserializeObject<LatestPriceOutput>(jsonResponse);
            double price = jsonObj.price;
            double value = price * input.Quantity;

            Orders order = new Orders()
            {
                Symbol = input.Symbol,
                Type = input.Type,
                Price = price,
                Quantity = input.Quantity,
                Date = DateTime.Now,
                TransactionID = transactionId
            };
            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            var createdOrder = await _context.Orders.FirstOrDefaultAsync(x => x.TransactionID == transactionId);

            if (input.Type == "BUY")
            {
                Positions position = new Positions()
                {
                    AccountId = account.Id,
                    OrderId = createdOrder.Id,
                    Value = value,
                    Symbol = input.Symbol
                };
                _context.Positions.Add(position);
                await _context.SaveChangesAsync();

                account.Balance = account.Balance - value;
                _context.Entry(account).State = EntityState.Modified;
                await _context.SaveChangesAsync();

            }
            else
            {
                // TO DO: Logic for SELLS
            }

            return Ok(createdOrder);
        }

    }

    public class TradeExecInput
    {
        public string Token { get; set; }
        public string Symbol { get; set; }
        public int Quantity { get; set; }
        public string Type { get; set; }
    }

    public class LatestPriceOutput
    {
        public string symbol { get; set; }
        public double price { get; set; }
        public DateTime timestamp { get; set; }
        public string source { get; set; }
    }
}