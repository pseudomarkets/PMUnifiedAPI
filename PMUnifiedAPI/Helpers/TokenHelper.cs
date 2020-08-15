using JWT;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JWT.Algorithms;
using JWT.Builder;
using JWT.Serializers;
using Microsoft.Extensions.Configuration;

/*
 * Pseudo Markets Unified Web API
 * JWT Token Generator and Validator
 * Author: Shravan Jambukesan <shravan@shravanj.com>
 * (c) 2019 - 2020 Pseudo Markets
 */

namespace PMUnifiedAPI.Helpers
{
    public class TokenHelper
    {
        // REPLACE THIS WITH YOUR OWN SECRET STRING!
        private static string secret = Startup.Configuration.GetValue<string>("PMConfig:TokenSecretKey");

        public static bool ValidateToken(string token)
        {
            bool isValid = false;
            try
            {
                IJsonSerializer serializer = new JsonNetSerializer();
                IDateTimeProvider provider = new UtcDateTimeProvider();
                IJwtValidator validator = new JwtValidator(serializer, provider);
                IBase64UrlEncoder urlEncoder = new JwtBase64UrlEncoder();
                IJwtDecoder decoder = new JwtDecoder(serializer, validator, urlEncoder, new HMACSHA256Algorithm());

                var json = decoder.Decode(token, secret, verify: true);
                if (json != null)
                {
                    isValid = true;
                }
            }
            catch (TokenExpiredException)
            {
                Console.WriteLine("Token has expired");
            }
            catch (SignatureVerificationException)
            {
                Console.WriteLine("Token has invalid signature");
            }

            return isValid;
        }

        public static string GenerateToken(string username)
        {
            var token = new JwtBuilder()
                .WithAlgorithm(new HMACSHA256Algorithm())
                .WithSecret(secret)
                .AddClaim("sub", username + Guid.NewGuid().ToString())
                .Encode();
            return token;
        }
    }
}
