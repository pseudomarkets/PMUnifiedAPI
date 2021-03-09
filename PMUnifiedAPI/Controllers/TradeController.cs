using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PMCommonApiModels.RequestModels;
using PMCommonApiModels.ResponseModels;
using PMCommonEntities.Models;
using PMDataSynchronizer;
using PMMarketDataService.DataProvider.Client.Implementation;
using PMUnifiedAPI.AuthenticationService;
using PMUnifiedAPI.Helpers;
using PMUnifiedAPI.Models;
using Serilog;
using PseudoMarketsDbContext = PMUnifiedAPI.Models.PseudoMarketsDbContext;

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
        private readonly string _syncDbConnectionString;
        private readonly bool _dataSyncEnabled;
        private readonly DateTimeHelper _dateTimeHelper;
        private readonly UnifiedAuthService _unifiedAuth;
        private readonly MarketDataServiceClient _marketDataService;

        public TradeController(PseudoMarketsDbContext context, IOptions<PseudoMarketsConfig> appConfig, DateTimeHelper dateTimeHelper, UnifiedAuthService unifiedAuth, MarketDataServiceClient marketDataService)
        {
            _context = context;
            var config = appConfig;
            _syncDbConnectionString = config.Value.DataSyncTargetDb;
            _dataSyncEnabled = config.Value.DataSyncEnabled;
            _dateTimeHelper = dateTimeHelper;
            _unifiedAuth = unifiedAuth;
            _marketDataService = marketDataService;
        }

        // POST: /api/Trade/Execute
        [Route("Execute")]
        [HttpPost]
        public async Task<ActionResult> ExecuteTrade([FromBody] TradeExecInput input)
        {
            var authResult = await _unifiedAuth.AuthenticateUser(Request.HttpContext);

            var tokenStatus = authResult.Item3;

            switch (tokenStatus)
            {
                case TokenHelper.TokenStatus.Valid:
                {
                    try
                    {
                        var transactionId = Guid.NewGuid().ToString();
                        
                        var account = authResult.Item2;

                        var user = authResult.Item1;

                        var latestPrice = await _marketDataService.GetLatestPrice(input.Symbol);
                        double price = latestPrice.price;

                        double value = price * input.Quantity;
                        bool hasSufficientBalance = account.Balance >= value;

                        TradeExecOutput output = new TradeExecOutput();

                        DataSyncManager dataSyncManager = new DataSyncManager(_syncDbConnectionString);

                        // TODO: Extract trade logic into a separate static class
                        if (hasSufficientBalance)
                        {
                            if (input.Type.ToUpper() == "BUY" || input.Type.ToUpper() == "SELL" || input.Type.ToUpper() == "SELLSHORT")
                            {
                                if (price > 0 && input.Quantity > 0)
                                {
                                    if (_dateTimeHelper.IsMarketOpen())
                                    {
                                            Orders order = new Orders
                                            {
                                                Symbol = input.Symbol,
                                                Type = input.Type,
                                                Price = price,
                                                Quantity = input.Quantity,
                                                Date = DateTime.Now,
                                                TransactionID = transactionId,
                                                EnvironmentId = RDSEnums.EnvironmentId.ProductionPrimary,
                                                OriginId = RDSEnums.OriginId.PseudoMarkets,
                                                SecurityTypeId = RDSEnums.SecurityType.RealWorld
                                            };

                                            Transactions transaction = new Transactions
                                            {
                                                AccountId = account.Id,
                                                TransactionId = transactionId,
                                                EnvironmentId = RDSEnums.EnvironmentId.ProductionPrimary,
                                                OriginId = RDSEnums.OriginId.PseudoMarkets
                                            };

                                            _context.Orders.Add(order);
                                            _context.Transactions.Add(transaction);
                                            await _context.SaveChangesAsync();

                                            if (_dataSyncEnabled)
                                            {
                                                Orders replicatedOrder = new Orders
                                                {
                                                    Symbol = order.Symbol,
                                                    Type = order.Type,
                                                    Price = order.Price,
                                                    Quantity = order.Quantity,
                                                    TransactionID = order.TransactionID,
                                                    Date = DateTime.Now,
                                                    EnvironmentId = RDSEnums.EnvironmentId.ProductionSecondary,
                                                    OriginId = RDSEnums.OriginId.PseudoMarkets,
                                                    SecurityTypeId = RDSEnums.SecurityType.RealWorld
                                                };

                                                Transactions replicatedTransaction = new Transactions
                                                {
                                                    TransactionId = order.TransactionID,
                                                    EnvironmentId = RDSEnums.EnvironmentId.ProductionSecondary,
                                                    OriginId = RDSEnums.OriginId.PseudoMarkets
                                                };

                                                await dataSyncManager.SyncOrders(replicatedOrder, user, DataSyncManager.DbSyncMethod.Insert);
                                                await dataSyncManager.SyncTransactions(replicatedTransaction, user, DataSyncManager.DbSyncMethod.Insert);
                                            }

                                            var createdOrder = await _context.Orders.FirstOrDefaultAsync(x => x.TransactionID == transactionId);

                                            if (input.Type.ToUpper() == "BUY")
                                            {
                                                var doesAccountHaveExistingPosition =
                                                    _context.Positions.Any(x => x.AccountId == account.Id && x.Symbol == input.Symbol);
                                                if (doesAccountHaveExistingPosition)
                                                {
                                                    var existingPosition = await _context.Positions
                                                        .Where(x => x.AccountId == account.Id && x.Symbol == input.Symbol).FirstOrDefaultAsync();
                                                    // Long position
                                                    if (existingPosition.Quantity > 0)
                                                    {
                                                        existingPosition.Value += value;
                                                        existingPosition.Quantity += input.Quantity;
                                                        _context.Entry(existingPosition).State = EntityState.Modified;
                                                        account.Balance = account.Balance - value;
                                                        _context.Entry(account).State = EntityState.Modified;
                                                        await _context.SaveChangesAsync();

                                                        if (_dataSyncEnabled)
                                                        {
                                                            await dataSyncManager.SyncPositions(existingPosition, user,
                                                                DataSyncManager.DbSyncMethod.Update);
                                                            await dataSyncManager.SyncAccounts(account, user, DataSyncManager.DbSyncMethod.Update);
                                                        }

                                                    }
                                                    else // Short position
                                                    {
                                                        if (Math.Abs(existingPosition.Quantity) == input.Quantity)
                                                        {
                                                            double gainOrLoss = existingPosition.Value - value;
                                                            account.Balance += gainOrLoss;
                                                            _context.Entry(account).State = EntityState.Modified;
                                                            _context.Entry(existingPosition).State = EntityState.Deleted;
                                                            await _context.SaveChangesAsync();

                                                            if (_dataSyncEnabled)
                                                            {
                                                                await dataSyncManager.SyncAccounts(account, user,
                                                                    DataSyncManager.DbSyncMethod.Update);
                                                                await dataSyncManager.SyncPositions(existingPosition, user,
                                                                    DataSyncManager.DbSyncMethod.Delete);
                                                            }
                                                        }
                                                        else
                                                        {
                                                            existingPosition.Value = existingPosition.Value - value;
                                                            existingPosition.Quantity += input.Quantity;
                                                            account.Balance += existingPosition.Value - value;
                                                            _context.Entry(existingPosition).State = EntityState.Modified;
                                                            _context.Entry(account).State = EntityState.Modified;
                                                            await _context.SaveChangesAsync();

                                                            if (_dataSyncEnabled)
                                                            {

                                                                await dataSyncManager.SyncAccounts(account, user,
                                                                    DataSyncManager.DbSyncMethod.Update);
                                                                await dataSyncManager.SyncPositions(existingPosition, user,
                                                                    DataSyncManager.DbSyncMethod.Update);

                                                            }
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    Positions position = new Positions
                                                    {
                                                        AccountId = account.Id,
                                                        OrderId = createdOrder.Id,
                                                        Value = value,
                                                        Symbol = input.Symbol,
                                                        Quantity = input.Quantity,
                                                        EnvironmentId = RDSEnums.EnvironmentId.ProductionPrimary,
                                                        OriginId = RDSEnums.OriginId.PseudoMarkets,
                                                        SecurityTypeId = RDSEnums.SecurityType.RealWorld
                                                    };
                                                    _context.Positions.Add(position);
                                                    await _context.SaveChangesAsync();

                                                    account.Balance = account.Balance - value;
                                                    _context.Entry(account).State = EntityState.Modified;
                                                    await _context.SaveChangesAsync();

                                                    if (_dataSyncEnabled)
                                                    {
                                                        Positions replicatedPosition = new Positions
                                                        {
                                                            OrderId = createdOrder.Id,
                                                            Value = value,
                                                            Symbol = input.Symbol,
                                                            Quantity = input.Quantity,
                                                            EnvironmentId = RDSEnums.EnvironmentId.ProductionSecondary,
                                                            OriginId = RDSEnums.OriginId.PseudoMarkets,
                                                            SecurityTypeId = RDSEnums.SecurityType.RealWorld
                                                        };

                                                        await dataSyncManager.SyncPositions(replicatedPosition, user,
                                                            DataSyncManager.DbSyncMethod.Insert);
                                                        await dataSyncManager.SyncAccounts(account, user, DataSyncManager.DbSyncMethod.Update);
                                                    }
                                                }
                                            }
                                            else if (input.Type.ToUpper() == "SELL")
                                            {
                                                var doesAccountHaveExistingPosition =
                                                    _context.Positions.Any(x => x.AccountId == account.Id && x.Symbol == input.Symbol);
                                                if (doesAccountHaveExistingPosition)
                                                {
                                                    var existingPosition = await _context.Positions
                                                        .Where(x => x.AccountId == account.Id && x.Symbol == input.Symbol).FirstOrDefaultAsync();
                                                    if (input.Quantity == existingPosition.Quantity)
                                                    {
                                                        account.Balance = account.Balance + value;
                                                        _context.Entry(existingPosition).State = EntityState.Deleted;
                                                        _context.Entry(account).State = EntityState.Modified;
                                                        await _context.SaveChangesAsync();

                                                        if (_dataSyncEnabled)
                                                        {
                                                            await dataSyncManager.SyncAccounts(account, user,
                                                                DataSyncManager.DbSyncMethod.Update);
                                                            await dataSyncManager.SyncPositions(existingPosition, user,
                                                                DataSyncManager.DbSyncMethod.Delete);
                                                        }
                                                    }
                                                    else
                                                    {
                                                        existingPosition.Value -= value;
                                                        existingPosition.Quantity -= input.Quantity;
                                                        _context.Entry(existingPosition).State = EntityState.Modified;
                                                        account.Balance = account.Balance + value;
                                                        _context.Entry(account).State = EntityState.Modified;
                                                        await _context.SaveChangesAsync();

                                                        if (_dataSyncEnabled)
                                                        {
                                                            await dataSyncManager.SyncAccounts(account, user,
                                                                DataSyncManager.DbSyncMethod.Update);
                                                            await dataSyncManager.SyncPositions(existingPosition, user,
                                                                DataSyncManager.DbSyncMethod.Update);
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    output.StatusCode = TradeStatusCodes.ExecutionError;
                                                    output.StatusMessage =
                                                        StatusMessages.InvalidPositionsMessage + input.Symbol;
                                                    return Ok(output);
                                                }
                                            }
                                            else if (input.Type.ToUpper() == "SELLSHORT")
                                            {
                                                var doesAccountHaveExistingPosition =
                                                    _context.Positions.Any(x => x.AccountId == account.Id && x.Symbol == input.Symbol);
                                                if (doesAccountHaveExistingPosition)
                                                {
                                                    var existingPosition = await _context.Positions
                                                        .Where(x => x.AccountId == account.Id && x.Symbol == input.Symbol)
                                                        .FirstOrDefaultAsync();
                                                    if (existingPosition.Quantity > 0)
                                                    {
                                                        _context.Entry(createdOrder).State = EntityState.Deleted;
                                                        await _context.SaveChangesAsync();
                                                        if (_dataSyncEnabled)
                                                        {
                                                            await dataSyncManager.SyncOrders(createdOrder, user, DataSyncManager.DbSyncMethod.Delete);
                                                        }

                                                        output.StatusCode = TradeStatusCodes.ExecutionError;
                                                        output.StatusMessage =
                                                            StatusMessages.InvalidShortPositionMessage;
                                                        return Ok(output);
                                                    }

                                                    existingPosition.Value += value;
                                                    existingPosition.Quantity += input.Quantity * -1;
                                                    _context.Entry(existingPosition).State = EntityState.Modified;
                                                    await _context.SaveChangesAsync();

                                                    if (_dataSyncEnabled)
                                                    {
                                                        await dataSyncManager.SyncAccounts(account, user,
                                                            DataSyncManager.DbSyncMethod.Update);
                                                        await dataSyncManager.SyncPositions(existingPosition, user,
                                                            DataSyncManager.DbSyncMethod.Update);
                                                    }
                                                }
                                                else
                                                {
                                                    Positions position = new Positions
                                                    {
                                                        AccountId = account.Id,
                                                        OrderId = createdOrder.Id,
                                                        Value = value,
                                                        Symbol = input.Symbol,
                                                        Quantity = input.Quantity * -1,
                                                        EnvironmentId = RDSEnums.EnvironmentId.ProductionPrimary,
                                                        OriginId = RDSEnums.OriginId.PseudoMarkets,
                                                        SecurityTypeId = RDSEnums.SecurityType.RealWorld
                                                    };

                                                    _context.Positions.Add(position);
                                                    account.Balance = account.Balance - value;
                                                    _context.Entry(account).State = EntityState.Modified;
                                                    await _context.SaveChangesAsync();

                                                    if (_dataSyncEnabled)
                                                    {
                                                        await dataSyncManager.SyncPositions(position, user,
                                                            DataSyncManager.DbSyncMethod.Insert);
                                                        await dataSyncManager.SyncAccounts(account, user, DataSyncManager.DbSyncMethod.Update);
                                                    }
                                                }
                                            }

                                            output.StatusCode = TradeStatusCodes.ExecutionOk;
                                            output.StatusMessage = StatusMessages.SuccessMessage;
                                            output.Order = createdOrder;
                                            return Ok(output);
                                    }

                                    CreateQueuedOrder(input, user.Id);
                                    output.StatusMessage =
                                        "Market is closed, order has been queued to be filled on next market open";
                                    output.StatusCode = TradeStatusCodes.ExecutionQueued;
                                    return Ok(output);
                                }

                                output.StatusCode = TradeStatusCodes.ExecutionError;
                                output.StatusMessage = StatusMessages.InvalidSymbolOrQuantityMessage;
                                return Ok(output);
                            }

                            output.StatusCode = TradeStatusCodes.ExecutionError;
                            output.StatusMessage = StatusMessages.InvalidOrderTypeMessage;
                            return Ok(output);
                        }

                        output.StatusCode = TradeStatusCodes.ExecutionError;
                        output.StatusMessage = StatusMessages.InsufficientBalanceMessage;
                        return Ok(output);
                    }
                    catch (Exception e)
                    {
                        TradeExecOutput status = new TradeExecOutput
                        {
                            Order = null,
                            StatusCode = TradeStatusCodes.ExecutionError,
                            StatusMessage = StatusMessages.FailureMessage
                        };
                        Log.Fatal(e, $"{nameof(ExecuteTrade)}");
                        return Ok(status);
                    }
                }
                case TokenHelper.TokenStatus.Expired:
                {
                    TradeExecOutput status = new TradeExecOutput
                    {
                        Order = null,
                        StatusCode = TradeStatusCodes.ExecutionError,
                        StatusMessage = StatusMessages.ExpiredTokenMessage
                    };

                    return Ok(status);
                }
                default:
                {
                    TradeExecOutput status = new TradeExecOutput
                    {
                        Order = null,
                        StatusCode = TradeStatusCodes.ExecutionError,
                        StatusMessage = StatusMessages.InvalidTokenMessage
                    };

                    return Ok(status);
                }
            }
        }

        private void CreateQueuedOrder(TradeExecInput input, int userId)
        {
            try
            {
                QueuedOrders order = new QueuedOrders
                {
                    OrderDate = DateTime.Today,
                    OrderType = input.Type,
                    Quantity = input.Quantity,
                    Symbol = input.Symbol,
                    UserId = userId,
                    IsOpenOrder = true,
                    EnvironmentId = RDSEnums.EnvironmentId.ProductionPrimary
                };

                _context.QueuedOrders.Add(order);

                _context.SaveChanges();
            }
            catch (Exception e)
            {
                Log.Fatal(e, $"{nameof(CreateQueuedOrder)}");
            }
        }

    }
}