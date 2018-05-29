// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using BasicApi.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Serialization;
using Npgsql;

namespace BasicApi
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            var rsa = new RSACryptoServiceProvider(2048);
            var key = new RsaSecurityKey(rsa.ExportParameters(true));

            services.AddSingleton(new SigningCredentials(
                key,
                SecurityAlgorithms.RsaSha256Signature));

            services.AddAuthentication().AddJwtBearer(options =>
            {
                options.TokenValidationParameters.IssuerSigningKey = key;
                options.TokenValidationParameters.ValidAudience = "Myself";
                options.TokenValidationParameters.ValidIssuer = "BasicApi";
            });

            switch (Configuration["Database"])
            {
                case "None":
                    // No database needed
                    break;

                case var database when string.IsNullOrEmpty(database):
                    // Use SQLite when running outside a benchmark test.
                    services
                        .AddEntityFrameworkSqlite()
                        .AddDbContextPool<BasicApiContext>(options => options.UseSqlite("Data Source=BasicApi.db"));
                    break;

                case "PostgreSql":
                    var connectionString = Configuration["ConnectionString"];
                    if (string.IsNullOrEmpty(connectionString))
                    {
                        throw new ArgumentException("Connection string must be specified for Npgsql.");
                    }

                    // Make connection string unique to this application
                    connectionString = Regex.Replace(
                        input: connectionString,
                        pattern: "(Database=)[^;]*;",
                        replacement: "$1BasicApi;");
                    var settings = new NpgsqlConnectionStringBuilder(connectionString);
                    if (!settings.NoResetOnClose)
                    {
                        throw new ArgumentException("No Reset On Close=true must be specified for Npgsql.");
                    }
                    if (settings.Enlist)
                    {
                        throw new ArgumentException("Enlist=false must be specified for Npgsql.");
                    }

                    services
                        .AddEntityFrameworkNpgsql()
                        .AddDbContextPool<BasicApiContext>(options => options.UseNpgsql(connectionString));
                    break;

                default:
                    throw new ArgumentException(
                        $"Application does not support database type {Configuration["Database"]}.");
            }

            services.AddAuthorization(options =>
            {
                options.AddPolicy(
                    "pet-store-reader",
                    builder => builder
                        .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                        .RequireAuthenticatedUser()
                        .RequireClaim("scope", "pet-store-reader"));

                options.AddPolicy(
                    "pet-store-writer",
                    builder => builder
                        .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                        .RequireAuthenticatedUser()
                        .RequireClaim("scope", "pet-store-writer"));
            });

            services
                .AddMvcCore()
                .AddAuthorization()
                .AddJsonFormatters(json => json.ContractResolver = new CamelCasePropertyNamesContractResolver())
                .AddDataAnnotations();

            services.AddSingleton(new PetRepository());
        }

        public void Configure(IApplicationBuilder app, IApplicationLifetime lifetime)
        {
            var services = app.ApplicationServices;
            switch (Configuration["Database"])
            {
                case var database when string.IsNullOrEmpty(database):
                case "PostgreSql":
                    CreateDatabase(services);
                    lifetime.ApplicationStopping.Register(() => DropDatabase(services));
                    break;
            }

            app.Use(next => async context =>
            {
                try
                {
                    await next(context);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    throw;
                }
            });

            app.UseAuthentication();
            app.UseMvc();
        }

        private static void CreateDatabase(IServiceProvider services)
        {
            using (var serviceScope = services.GetRequiredService<IServiceScopeFactory>().CreateScope())
            {
                using (var dbContext = services.GetRequiredService<BasicApiContext>())
                {
                    // Contrary to general documentation, creates and seeds tables even if PostgreSQL database exists.
                    dbContext.Database.EnsureCreated();
                }
            }
        }

        private static void DropDatabase(IServiceProvider services)
        {
            using (var serviceScope = services.GetRequiredService<IServiceScopeFactory>().CreateScope())
            {
                using (var dbContext = services.GetRequiredService<BasicApiContext>())
                {
                    dbContext.Database.EnsureDeleted();
                }
            }
        }

        public static void Main(string[] args)
        {
            var host = CreateWebHostBuilder(args)
                .Build();

            host.Run();
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .AddCommandLine(args)
                .Build();

            return new WebHostBuilder()
                .UseKestrel()
                .UseUrls("http://+:5000")
                .UseConfiguration(configuration)
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseStartup<Startup>();
        }
    }
}
