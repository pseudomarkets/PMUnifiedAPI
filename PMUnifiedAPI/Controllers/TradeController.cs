using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
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
        private string baseUrl = "https://app.pseudomarkets.live";

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
            var response = await client.GetAsync(baseUrl + "/api/Quotes/SmartQuote/" + input.Symbol);
            string jsonResponse = await response.Content.ReadAsStringAsync();
            var jsonObj = JsonConvert.DeserializeObject<LatestPriceOutput>(jsonResponse);
            double price = jsonObj.price;
            double value = price * input.Quantity;

            if (input.Type == "BUY" || input.Type == "SELL" || input.Type == "SELLSHORT")
            {
                if (price > 0 && input.Quantity > 0)
                {
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
                        var doesAccountHaveExistingPosition =
                            _context.Positions.Any(x => x.AccountId == account.Id && x.Symbol == input.Symbol);
                        if (doesAccountHaveExistingPosition == true)
                        {
                            var existingPosition = await _context.Positions
                                .Where(x => x.AccountId == account.Id && x.Symbol == input.Symbol).FirstOrDefaultAsync();
                            existingPosition.Value += value;
                            existingPosition.Quantity += input.Quantity;
                            _context.Entry(existingPosition).State = EntityState.Modified;
                            account.Balance = account.Balance - value;
                            _context.Entry(account).State = EntityState.Modified;
                            await _context.SaveChangesAsync();
                        }
                        else
                        {
                            Positions position = new Positions()
                            {
                                AccountId = account.Id,
                                OrderId = createdOrder.Id,
                                Value = value,
                                Symbol = input.Symbol,
                                Quantity = input.Quantity
                            };
                            _context.Positions.Add(position);
                            await _context.SaveChangesAsync();

                            account.Balance = account.Balance - value;
                            _context.Entry(account).State = EntityState.Modified;
                            await _context.SaveChangesAsync();
                        }
                    }
                    else if (input.Type == "SELL")
                    {
                        var doesAccountHaveExistingPosition =
                            _context.Positions.Any(x => x.AccountId == account.Id && x.Symbol == input.Symbol);
                        if (doesAccountHaveExistingPosition == true)
                        {
                            var existingPosition = await _context.Positions
                                .Where(x => x.AccountId == account.Id && x.Symbol == input.Symbol).FirstOrDefaultAsync();
                            if (input.Quantity == existingPosition.Quantity)
                            {
                                account.Balance = account.Balance + value;
                                _context.Entry(existingPosition).State = EntityState.Deleted;
                                _context.Entry(account).State = EntityState.Modified;
                                await _context.SaveChangesAsync();
                            }
                            else
                            {
                                existingPosition.Value -= value;
                                existingPosition.Quantity -= input.Quantity;
                                _context.Entry(existingPosition).State = EntityState.Modified;
                                account.Balance = account.Balance + value;
                                _context.Entry(account).State = EntityState.Modified;
                                await _context.SaveChangesAsync();
                            }
                        }
                        else
                        {
                            return Ok("No positions to sell for symbol: " + input.Symbol);
                        }
                    }
                    else
                    {
                        // TO DO: LOGIC FOR SHORT SELLING
                        return Ok("Short selling is currently unavailable");
                    }

                    return Ok(createdOrder);
                }
                else
                {
                    return Ok("Invalid symbol or quantity");
                }
            }
            else
            {
                return Ok("Invalid Order Type");
            }

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