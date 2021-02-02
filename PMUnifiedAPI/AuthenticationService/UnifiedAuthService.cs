using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.EntityFrameworkCore;
using PMUnifiedAPI.Helpers;
using PMUnifiedAPI.Models;

/*
 * Pseudo Markets Unified Web API
 * Unified Authentication Service Provider
 * Author: Shravan Jambukesan <shravan@shravanj.com>
 * (c) 2019 - 2021 Pseudo Markets
 */

namespace PMUnifiedAPI.AuthenticationService
{
    public class UnifiedAuthService : IUnifiedAuthService
    {
        private readonly PseudoMarketsDbContext _context;
        public UnifiedAuthService(PseudoMarketsDbContext context)
        {
            this._context = context;
        }

        public async Task<Tuple<Users, Accounts, TokenHelper.TokenStatus>> AuthenticateUser(HttpContext context)
        {
            var authHeader = context.Request.Headers["UnifiedAuth"].ToString();

            var tokenStatus = TokenHelper.ValidateToken(authHeader);

            if (tokenStatus == TokenHelper.TokenStatus.Valid)
            {
                var account = await GetAccountFromToken(authHeader);

                var user = await GetUserFromToken(authHeader);

                return new Tuple<Users, Accounts, TokenHelper.TokenStatus>(user, account, tokenStatus);
            }
            else
            {
                return new Tuple<Users, Accounts, TokenHelper.TokenStatus>(null, null, TokenHelper.TokenStatus.Unknown);
            }

        }

        private async Task<Accounts> GetAccountFromToken(string token)
        {
            var account = await _context.Tokens.Where(x => x.Token == token).Join(_context.Accounts,
                    tokens => tokens.UserID, accounts => accounts.UserID, (tokens, accounts) => accounts)
                .FirstOrDefaultAsync();

            return account;
        }

        private async Task<Users> GetUserFromToken(string token)
        {
            var user = await _context.Tokens.Where(x => x.Token == token)
                .Join(_context.Users, tokens => tokens.UserID, users => users.Id, (tokens, users) => users)
                .FirstOrDefaultAsync();

            return user;
        }
    }
}
