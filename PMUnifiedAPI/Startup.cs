using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PMUnifiedAPI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using PMMarketDataService.DataProvider.Client.Implementation;
using PMUnifiedAPI.AuthenticationService;
using PMUnifiedAPI.Helpers;
using PMUnifiedAPI.Interfaces;
using PMUnifiedAPI.Swagger;

namespace PMUnifiedAPI
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public static IConfiguration Configuration { get; set; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDbContext<PseudoMarketsDbContext>(options => options.UseSqlServer(Configuration.GetConnectionString("PMDB")));
            services.Configure<PseudoMarketsConfig>(Configuration.GetSection("PMConfig"));
            // Inject DateTimeHelper for market open check and Unified Auth Service for shared authentication mechanism
            services.AddScoped<DateTimeHelper>();
            services.AddScoped<UnifiedAuthService>();
            // Inject Market Data Service Provider
            services.AddSingleton<MarketDataServiceClient>(new MarketDataServiceClient(new HttpClient(),
                Configuration.GetValue<string>("PMConfig:InternalServiceUsername"),
                Configuration.GetValue<string>("PMConfig:InternalServicePassword"),
                Configuration.GetValue<string>("PMConfig:MarketDataServiceUrl")));
            services.AddControllers();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Version = "v1",
                    Title = "Pseudo Markets Unified API",
                    Description = "Unified API for trading, quotes, account management, and portfolio performance",
                    Contact = new OpenApiContact
                    {
                        Name = "Shravan Jambukesan",
                        Email = "shravan@shravanj.com",
                        Url = new Uri("https://github.com/ShravanJ")
                    },
                    License = new OpenApiLicense
                    {
                        Name = "MIT License",
                        Url = new Uri("https://github.com/pseudomarkets/PMUnifiedAPI/blob/master/LICENSE.txt")
                    }
                });

                c.OperationFilter<RequiredHeaderParameter>();
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseSwagger();

            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Pseudo Markets Unified API");
            });

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
