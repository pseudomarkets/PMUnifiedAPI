using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using PMUnifiedAPI.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace PMUnifiedAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AccountController : ControllerBase
    {
        private readonly PseudoMarketsDbContext _context;
        private string baseUrl = "";
        private readonly IOptions<PseudoMarketsConfig> config;

        public AccountController(PseudoMarketsDbContext context, IOptions<PseudoMarketsConfig> appConfig)
        {
            _context = context;
            config = appConfig;
            baseUrl = config.Value.AppBaseUrl;
        }

        // POST: /api/Account/Positions
        [HttpPost]
        [Route("Positions")]
        public async Task<ActionResult> ViewPositions(ViewAccount input)
        {
            var userId = await _context.Tokens.Where(x => x.Token == input.Token).Select(x => x.UserID).FirstOrDefaultAsync();
            var user = await _context.Users.FirstOrDefaultAsync(x => x.Id == userId);
            var account = await _context.Accounts.FirstOrDefaultAsync(x => x.UserID == user.Id);

            var positions = await _context.Positions.Where(x => x.AccountId == account.Id).ToListAsync();
            return Ok(positions);
        }

        // POST: /api/Account/Transactions
        [HttpPost]
        [Route("Transactions")]
        public async Task<ActionResult> ViewTransactions(ViewAccount input)
        {
            var userId = await _context.Tokens.Where(x => x.Token == input.Token).Select(x => x.UserID).FirstOrDefaultAsync();
            var user = await _context.Users.FirstOrDefaultAsync(x => x.Id == userId);
            var account = await _context.Accounts.FirstOrDefaultAsync(x => x.UserID == user.Id);
            var transactions = await _context.Transactions.Where(x => x.AccountId == account.Id).ToListAsync();
            var orders = transactions.Join(_context.Orders, transactions1 => transactions1.TransactionId,
                orders1 => orders1.TransactionID, (transactions1, orders1) => orders1).ToList();

            return Ok(orders);
        }

        // POST: /api/Account/Summary
        [HttpPost]
        [Route("Summary")]
        public async Task<ActionResult> ViewSummary(ViewAccount input)
        {
            var userId = await _context.Tokens.Where(x => x.Token == input.Token).Select(x => x.UserID).FirstOrDefaultAsync();
            var user = await _context.Users.FirstOrDefaultAsync(x => x.Id == userId);
            var account = await _context.Accounts.FirstOrDefaultAsync(x => x.UserID == user.Id);

            var positions = await _context.Positions.Where(x => x.AccountId == account.Id).ToListAsync();
            double totalInvestedValue = 0;
            int numPositions = 0;
            double totalCurrentValue = 0;
            double investmentGainOrLoss = 0;
            double investmentGainOrLossPercentage = 0;
            foreach (Positions p in positions)
            {
                totalInvestedValue += p.Value;
                string symbol = p.Symbol;
                var client = new HttpClient();
                var response = await client.GetAsync(baseUrl + "/api/Quotes/LatestPrice/" + symbol);
                string jsonResponse = await response.Content.ReadAsStringAsync();
                var jsonObj = JsonConvert.DeserializeObject<LatestPriceOutput>(jsonResponse);
                double price = jsonObj.price;
                totalCurrentValue += price * p.Quantity;
                numPositions++;
            }
            investmentGainOrLoss = totalCurrentValue - totalInvestedValue;
            if (investmentGainOrLoss > 0)
            {
                investmentGainOrLossPercentage = (investmentGainOrLoss / totalInvestedValue) * 100;
            }
            else
            {
                investmentGainOrLossPercentage = (-1 * (investmentGainOrLoss / totalInvestedValue)) * 100;
            }

            AccountSummaryOutput output = new AccountSummaryOutput()
            {
                AccountId = account.Id,
                AccountBalance = account.Balance,
                TotalCurrentValue = totalCurrentValue,
                TotalInvestedValue = totalInvestedValue,
                NumberOfPositions = numPositions,
                InvestmentGainOrLoss = investmentGainOrLoss,
                InvestmentGainOrLossPercentage = investmentGainOrLossPercentage
            };

            return Ok(output);
        }
    }

    public class ViewAccount
    {
        public string Token { get; set; }
    }

    public class AccountSummaryOutput
    {
        public int AccountId { get; set; }
        public double AccountBalance { get; set; }
        public double TotalInvestedValue { get; set; }
        public double TotalCurrentValue { get; set; }
        public double InvestmentGainOrLoss { get; set; }
        public double InvestmentGainOrLossPercentage { get; set; }
        public int NumberOfPositions { get; set; }

    }
}