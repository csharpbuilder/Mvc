// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace BasicViews
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
            switch (Configuration["Database"])
            {
                case "None":
                    // No database needed
                    break;

                case var database when string.IsNullOrEmpty(database):
                    // Use SQLite when running outside a benchmark test.
                    services
                        .AddEntityFrameworkSqlite()
                        .AddDbContextPool<BasicViewsContext>(options => options.UseSqlite("Data Source=BasicViews.db"));
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
                        replacement: "$1BasicViews;");
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
                        .AddDbContextPool<BasicViewsContext>(options => options.UseNpgsql(connectionString));
                    break;

                default:
                    throw new ArgumentException(
                        $"Application does not support database type {Configuration["Database"]}.");
            }

            services.AddMvc();
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

            app.UseStaticFiles();
            app.UseMvcWithDefaultRoute();
        }

        private static void CreateDatabase(IServiceProvider services)
        {
            using (var serviceScope = services.GetRequiredService<IServiceScopeFactory>().CreateScope())
            {
                using (var dbContext = services.GetRequiredService<BasicViewsContext>())
                {
                    dbContext.Database.EnsureCreated();
                }
            }
        }

        private static void DropDatabase(IServiceProvider services)
        {
            using (var serviceScope = services.GetRequiredService<IServiceScopeFactory>().CreateScope())
            {
                using (var dbContext = services.GetRequiredService<BasicViewsContext>())
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
                .UseIISIntegration()
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseStartup<Startup>();
        }
    }
}
