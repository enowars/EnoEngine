namespace EnoChecker
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;
    using EnoCore;
    using EnoCore.Checker;
    using EnoCore.Logging;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;

    public class Startup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            // TODO gracefully abort
            var (checkerType, checkerInitializerType) = LoadCheckerFromAssembly(Program.Path!);

            if (checkerInitializerType.InvokeMember(
                string.Empty,
                BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance | BindingFlags.CreateInstance,
                null,
                null,
                null) is not ICheckerInitializer initializerInstance)
            {
                throw new Exception("Could not create ICheckerInitializer");
            }

            services.AddScoped(typeof(IChecker), checkerType);
            services.AddScoped(typeof(ICheckerInitializer), checkerInitializerType);
            services.AddControllers()
                .AddJsonOptions(jsonOptions =>
                {
                    jsonOptions.JsonSerializerOptions.AllowTrailingCommas = true;
                    jsonOptions.JsonSerializerOptions.ReadCommentHandling = JsonCommentHandling.Skip;
                    jsonOptions.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                    jsonOptions.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                });
            services.AddLogging(loggingBuilder =>
            {
                if (Environment.GetEnvironmentVariable("USE_ELK") != null)
                {
                    loggingBuilder.ClearProviders();
                    loggingBuilder.AddProvider(new EnoLogMessageConsoleLoggerProvider($"{initializerInstance.ServiceName}Checker"));
                }
            });
            initializerInstance.Initialize(services);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseStaticFiles();
            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapControllers();
                });
            });
        }

        private static (Type Checker, Type CheckerBuilder) LoadCheckerFromAssembly(string path)
        {
            // TODO do in separate domain https://www.codeproject.com/Articles/30612/Load-a-User-DLL-implementing-an-AppIn-interface
            Assembly checkerAssembly = Assembly.LoadFrom(path);
            Type? checkerType = null;
            Type? checkerBuilderType = null;

            foreach (var type in checkerAssembly.GetExportedTypes())
            {
                if (type.GetInterface(nameof(IChecker)) != null)
                {
                    if (checkerType is null)
                    {
                        checkerType = type;
                    }
                    else
                    {
                        throw new Exception($"Multiple {nameof(IChecker)} implementations in assembly.");
                    }
                }

                if (type.GetInterface(nameof(ICheckerInitializer)) != null)
                {
                    if (checkerBuilderType is null)
                    {
                        checkerBuilderType = type;
                    }
                    else
                    {
                        throw new Exception($"Multiple {nameof(ICheckerInitializer)} implementations in assembly.");
                    }
                }
            }

            if (checkerType is null)
            {
                throw new Exception($"No {nameof(IChecker)} implementation in assembly.");
            }

            if (checkerBuilderType is null)
            {
                throw new Exception($"No {nameof(ICheckerInitializer)} implementation in assembly.");
            }

            return (checkerType, checkerBuilderType);
        }
    }
}
