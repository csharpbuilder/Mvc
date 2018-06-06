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
            // Provide a connection string that is unique to this application.
            var connectionString = Regex.Replace(
                input: Configuration["ConnectionString"] ?? string.Empty,
                pattern: "(Database=)[^;]*;",
                replacement: "$1BasicViews;");

            var databaseType = Configuration["Database"];
            switch (databaseType)
            {
                case "None":
                    // No database needed e.g. only testing GET actions.
                    break;

                case var database when string.IsNullOrEmpty(database):
                    // Use SQLite when running outside a benchmark test.
                    services
                        .AddEntityFrameworkSqlite()
                        .AddDbContextPool<BasicViewsContext>(options => options.UseSqlite("Data Source=BasicViews.db"));
                    break;

                case "PostgreSql":
                    if (string.IsNullOrEmpty(connectionString))
                    {
                        throw new ArgumentException("Connection string must be specified for {databaseType}.");
                    }

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

                case "SqlServer":
                    if (string.IsNullOrEmpty(connectionString))
                    {
                        throw new ArgumentException("Connection string must be specified for {databaseType}.");
                    }

                    services
                        .AddEntityFrameworkSqlServer()
                        .AddDbContextPool<BasicViewsContext>(options => options.UseSqlServer(connectionString));
                    break;

                default:
                    throw new ArgumentException(
                        $"Application does not support database type {databaseType}.");
            }

            services.AddMvc();
        }

        public void Configure(IApplicationBuilder app, IApplicationLifetime lifetime)
        {
            if (!string.Equals("None", Configuration["Database"], StringComparison.Ordinal))
            {
                var services = app.ApplicationServices;
                CreateDatabase(services);
                lifetime.ApplicationStopping.Register(() => DropDatabase(services));
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
