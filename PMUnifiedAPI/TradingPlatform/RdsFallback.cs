using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PMCommonApiModels.RequestModels;
using PMCommonApiModels.ResponseModels;
using PMCommonEntities.Models;
using PMDataSynchronizer;
using PMMarketDataService.DataProvider.Client.Implementation;
using PMUnifiedAPI.Helpers;
using PMUnifiedAPI.Models;
using Serilog;
using PseudoMarketsDbContext = PMUnifiedAPI.Models.PseudoMarketsDbContext;

namespace PMUnifiedAPI.TradingPlatform
{
    public static class RdsFallback
    {
        public static async Task<TradeExecOutput> ProcessTradingRequestUsingRdsFallback(Users user, Accounts account,
            TradeExecInput input, PseudoMarketsDbContext context, MarketDataServiceClient marketDataService,
            DateTimeHelper dateTimeHelper)
        {
            try
            {
                var transactionId = Guid.NewGuid().ToString();

                var latestPrice = await marketDataService.GetLatestPrice(input.Symbol);
                double price = latestPrice.price;

                double value = price * input.Quantity;
                bool hasSufficientBalance = account.Balance >= value;

                TradeExecOutput output = new TradeExecOutput();

                // TODO: Extract trade logic into a separate static class
                if (hasSufficientBalance)
                {
                    if (input.Type.ToUpper() == "BUY" || input.Type.ToUpper() == "SELL" ||
                        input.Type.ToUpper() == "SELLSHORT")
                    {
                        if (price > 0 && input.Quantity > 0)
                        {
                            if (dateTimeHelper.IsMarketOpen())
                            {
                                Orders order = new Orders
                                {
                                    Symbol = input.Symbol,
                                    Type = input.Type,
                                    Price = price,
                                    Quantity = input.Quantity,
                                    Date = DateTime.Now,
                                    TransactionID = transactionId,
                                    EnvironmentId = RDSEnums.EnvironmentId.RdsFallback,
                                    OriginId = RDSEnums.OriginId.PseudoMarkets,
                                    SecurityTypeId = RDSEnums.SecurityType.RealWorld
                                };

                                Transactions transaction = new Transactions
                                {
                                    AccountId = account.Id,
                                    TransactionId = transactionId,
                                    EnvironmentId = RDSEnums.EnvironmentId.RdsFallback,
                                    OriginId = RDSEnums.OriginId.PseudoMarkets
                                };

                                context.Orders.Add(order);
                                context.Transactions.Add(transaction);
                                await context.SaveChangesAsync();
                                var createdOrder =
                                    await context.Orders.FirstOrDefaultAsync(x => x.TransactionID == transactionId);

                                if (input.Type.ToUpper() == "BUY")
                                {
                                    var doesAccountHaveExistingPosition =
                                        context.Positions.Any(x =>
                                            x.AccountId == account.Id && x.Symbol == input.Symbol);
                                    if (doesAccountHaveExistingPosition)
                                    {
                                        var existingPosition = await context.Positions
                                            .Where(x => x.AccountId == account.Id && x.Symbol == input.Symbol)
                                            .FirstOrDefaultAsync();
                                        // Long position
                                        if (existingPosition.Quantity > 0)
                                        {
                                            existingPosition.Value += value;
                                            existingPosition.Quantity += input.Quantity;
                                            context.Entry(existingPosition).State = EntityState.Modified;
                                            account.Balance = account.Balance - value;
                                            context.Entry(account).State = EntityState.Modified;
                                            await context.SaveChangesAsync();

                                        }
                                        else // Short position
                                        {
                                            if (Math.Abs(existingPosition.Quantity) == input.Quantity)
                                            {
                                                double gainOrLoss = existingPosition.Value - value;
                                                account.Balance += gainOrLoss;
                                                context.Entry(account).State = EntityState.Modified;
                                                context.Entry(existingPosition).State = EntityState.Deleted;
                                                await context.SaveChangesAsync();
                                            }
                                            else
                                            {
                                                existingPosition.Value = existingPosition.Value - value;
                                                existingPosition.Quantity += input.Quantity;
                                                account.Balance += existingPosition.Value - value;
                                                context.Entry(existingPosition).State = EntityState.Modified;
                                                context.Entry(account).State = EntityState.Modified;
                                                await context.SaveChangesAsync();
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
                                            EnvironmentId = RDSEnums.EnvironmentId.RdsFallback,
                                            OriginId = RDSEnums.OriginId.PseudoMarkets,
                                            SecurityTypeId = RDSEnums.SecurityType.RealWorld
                                        };
                                        context.Positions.Add(position);
                                        await context.SaveChangesAsync();

                                        account.Balance = account.Balance - value;
                                        context.Entry(account).State = EntityState.Modified;
                                        await context.SaveChangesAsync();
                                    }
                                }
                                else if (input.Type.ToUpper() == "SELL")
                                {
                                    var doesAccountHaveExistingPosition =
                                        context.Positions.Any(x =>
                                            x.AccountId == account.Id && x.Symbol == input.Symbol);
                                    if (doesAccountHaveExistingPosition)
                                    {
                                        var existingPosition = await context.Positions
                                            .Where(x => x.AccountId == account.Id && x.Symbol == input.Symbol)
                                            .FirstOrDefaultAsync();
                                        if (input.Quantity == existingPosition.Quantity)
                                        {
                                            account.Balance = account.Balance + value;
                                            context.Entry(existingPosition).State = EntityState.Deleted;
                                            context.Entry(account).State = EntityState.Modified;
                                            await context.SaveChangesAsync();
                                        }
                                        else
                                        {
                                            existingPosition.Value -= value;
                                            existingPosition.Quantity -= input.Quantity;
                                            context.Entry(existingPosition).State = EntityState.Modified;
                                            account.Balance = account.Balance + value;
                                            context.Entry(account).State = EntityState.Modified;
                                            await context.SaveChangesAsync();
                                        }
                                    }
                                    else
                                    {
                                        output.StatusCode = TradeStatusCodes.ExecutionError;
                                        output.StatusMessage =
                                            StatusMessages.InvalidPositionsMessage + input.Symbol;
                                        return output;
                                    }
                                }
                                else if (input.Type.ToUpper() == "SELLSHORT")
                                {
                                    var doesAccountHaveExistingPosition =
                                        context.Positions.Any(x =>
                                            x.AccountId == account.Id && x.Symbol == input.Symbol);
                                    if (doesAccountHaveExistingPosition)
                                    {
                                        var existingPosition = await context.Positions
                                            .Where(x => x.AccountId == account.Id && x.Symbol == input.Symbol)
                                            .FirstOrDefaultAsync();
                                        if (existingPosition.Quantity > 0)
                                        {
                                            context.Entry(createdOrder).State = EntityState.Deleted;
                                            await context.SaveChangesAsync();

                                            output.StatusCode = TradeStatusCodes.ExecutionError;
                                            output.StatusMessage =
                                                StatusMessages.InvalidShortPositionMessage;
                                            return output;
                                        }

                                        existingPosition.Value += value;
                                        existingPosition.Quantity += input.Quantity * -1;
                                        context.Entry(existingPosition).State = EntityState.Modified;
                                        await context.SaveChangesAsync();
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
                                            EnvironmentId = RDSEnums.EnvironmentId.RdsFallback,
                                            OriginId = RDSEnums.OriginId.PseudoMarkets,
                                            SecurityTypeId = RDSEnums.SecurityType.RealWorld
                                        };

                                        context.Positions.Add(position);
                                        account.Balance = account.Balance - value;
                                        context.Entry(account).State = EntityState.Modified;
                                        await context.SaveChangesAsync();
                                    }
                                }

                                output.StatusCode = TradeStatusCodes.ExecutionOk;
                                output.StatusMessage = StatusMessages.SuccessMessage;
                                output.Order = createdOrder;
                                return output;
                            }

                            CreateQueuedOrder(input, user.Id, context);
                            output.StatusMessage =
                                "Market is closed, order has been queued to be filled on next market open";
                            output.StatusCode = TradeStatusCodes.ExecutionQueued;
                            return output;
                        }

                        output.StatusCode = TradeStatusCodes.ExecutionError;
                        output.StatusMessage = StatusMessages.InvalidSymbolOrQuantityMessage;
                        return (output);
                    }

                    output.StatusCode = TradeStatusCodes.ExecutionError;
                    output.StatusMessage = StatusMessages.InvalidOrderTypeMessage;
                    return (output);
                }

                output.StatusCode = TradeStatusCodes.ExecutionError;
                output.StatusMessage = StatusMessages.InsufficientBalanceMessage;
                return (output);
            }
            catch (Exception e)
            {
                TradeExecOutput status = new TradeExecOutput
                {
                    Order = null,
                    StatusCode = TradeStatusCodes.ExecutionError,
                    StatusMessage = StatusMessages.FailureMessage
                };
                Log.Fatal(e, $"{nameof(ProcessTradingRequestUsingRdsFallback)}");
                return (status);
            }
        }

        private static void CreateQueuedOrder(TradeExecInput input, int userId, PseudoMarketsDbContext _context)
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
                    EnvironmentId = RDSEnums.EnvironmentId.RdsFallback
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
