using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
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
    public interface IUnifiedAuthService
    {
        Task<(Users User, Accounts Account, TokenHelper.TokenStatus TokenStatus)> AuthenticateUser(HttpContext context);
    }
}
