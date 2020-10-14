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
using PMCommonApiModels.RequestModels;
using PMCommonApiModels.ResponseModels;
using TwelveDataSharp;

namespace PMUnifiedAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AccountController : ControllerBase
    {
        private readonly PseudoMarketsDbContext _context;
        private string baseUrl = "";
        private readonly IOptions<PseudoMarketsConfig> config;
        private string twelveDataApiKey = string.Empty;

        public AccountController(PseudoMarketsDbContext context, IOptions<PseudoMarketsConfig> appConfig)
        {
            _context = context;
            config = appConfig;
            baseUrl = config.Value.AppBaseUrl;
            twelveDataApiKey = _context.ApiKeys.Where(x => x.ProviderName == "TwelveData").Select(x => x.ApiKey)
                .FirstOrDefault();
        }

        // POST: /api/Account/Positions
        [HttpPost]
        [Route("Positions")]
        public async Task<ActionResult> ViewPositions(ViewAccount input)
        {

            var account = await _context.Tokens.Where(x => x.Token == input.Token).Join(_context.Accounts,
                tokens => tokens.UserID, accounts => accounts.UserID, (tokens, accounts) => accounts).FirstOrDefaultAsync();

            var positions = await _context.Positions.Where(x => x.AccountId == account.Id).ToListAsync();
            return Ok(positions);
        }

        // POST: /api/Account/Transactions
        [HttpPost]
        [Route("Transactions")]
        public async Task<ActionResult> ViewTransactions(ViewAccount input)
        {

            var account = await _context.Tokens.Where(x => x.Token == input.Token).Join(_context.Accounts,
                tokens => tokens.UserID, accounts => accounts.UserID, (tokens, accounts) => accounts).FirstOrDefaultAsync();

            var transactions = await _context.Transactions.Where(x => x.AccountId == account.Id).ToListAsync();

            var orders = transactions.Join(_context.Orders, transactions1 => transactions1.TransactionId,
                orders1 => orders1.TransactionID, (transactions1, orders1) => orders1).ToList();

            return Ok(orders);
        }

        // POST: /api/Balance
        [HttpPost]
        [Route("Balance")]
        public async Task<ActionResult> ViewBalance(ViewAccount input)
        {
            var account = await _context.Tokens.Where(x => x.Token == input.Token).Join(_context.Accounts,
                    tokens => tokens.UserID, accounts => accounts.UserID, (tokens, accounts) => accounts)
                .FirstOrDefaultAsync();

            AccountBalanceOutput output = new AccountBalanceOutput()
            {
                AccountId = account.Id,
                AccountBalance = account.Balance
            };

            return Ok(output);
        }

        // POST: /api/Account/Summary
        [HttpPost]
        [Route("Summary")]
        public async Task<ActionResult> ViewSummary(ViewAccount input)
        {
            var account = await _context.Tokens.Where(x => x.Token == input.Token).Join(_context.Accounts,
                    tokens => tokens.UserID, accounts => accounts.UserID, (tokens, accounts) => accounts)
                .FirstOrDefaultAsync();

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
                TwelveDataClient twelveDataClient = new TwelveDataClient(twelveDataApiKey);
                var latestPrice = await twelveDataClient.GetRealTimePriceAsync(symbol);
                totalCurrentValue += latestPrice.Price * p.Quantity;
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
}