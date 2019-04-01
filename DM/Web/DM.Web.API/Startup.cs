﻿using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using AutoMapper;
using DM.Services.Core.Configuration;
using DM.Services.DataAccess;
using DM.Services.DataAccess.MongoIntegration;
using DM.Services.SearchEngine;
using DM.Services.SearchEngine.Configuration;
using DM.Web.API.Authentication;
using DM.Web.API.Middleware;
using DM.Web.Core.Binders;
using DM.Web.Core.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.Swagger;

namespace DM.Web.API
{
    /// <summary>
    /// Application
    /// </summary>
    public class Startup
    {
        private IConfigurationRoot Configuration { get; set; }

        private static Assembly[] GetAssemblies()
        {
            var currentAssembly = Assembly.GetExecutingAssembly();
            var referencedAssemblies = currentAssembly.GetReferencedAssemblies().Select(Assembly.Load).ToArray();
            return referencedAssemblies
                .Union(new[] {currentAssembly})
                .Union(referencedAssemblies.SelectMany(a => a.GetReferencedAssemblies().Select(Assembly.Load)))
                .Where(a => a.FullName.StartsWith("DM."))
                .Distinct()
                .ToArray();
        }

        /// <summary>
        /// Configure application services
        /// </summary>
        /// <param name="services">Service collection</param>
        /// <returns>Service provider</returns>
        public IServiceProvider ConfigureServices(IServiceCollection services)
        {
            Configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", false)
                .Build();
            var assemblies = GetAssemblies();

            services
                .AddOptions()
                .Configure<ConnectionStrings>(Configuration.GetSection(nameof(ConnectionStrings)).Bind)
                .Configure<IntegrationSettings>(Configuration.GetSection(nameof(IntegrationSettings)).Bind)
                .Configure<EmailConfiguration>(Configuration.GetSection(nameof(EmailConfiguration)).Bind)
                .Configure<SearchEngineConfiguration>(Configuration.GetSection(nameof(SearchEngineConfiguration)).Bind)
                .AddMemoryCache()
                .AddEntityFrameworkNpgsql()
                .AddDbContext<DmDbContext>(options => options
                    .UseNpgsql(Configuration.GetConnectionString(nameof(DmDbContext))))
                .AddSwaggerGen(c =>
                {
                    c.SwaggerDoc("v1", new Info {Title = "DM.API", Version = "v1"});
                    c.DescribeAllParametersInCamelCase();
                    c.DescribeAllEnumsAsStrings();
                    c.OperationFilter<AuthenticationSwaggerFilter>();
                    var apiAssemblyName = Assembly.GetExecutingAssembly().GetName().Name;
                    c.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, $"{apiAssemblyName}.xml"));
                })
                .AddAutoMapper()
                .AddMvc(config => config.ModelBinderProviders.Insert(0, new ReadableGuidBinderProvider()))
                .SetCompatibilityVersion(CompatibilityVersion.Version_2_2);
            
            var builder = new ContainerBuilder();
            builder.RegisterAssemblyTypes(assemblies)
                .Where(t => t.IsClass)
                .AsSelf()
                .AsImplementedInterfaces()
                .InstancePerLifetimeScope();
            builder.RegisterTypes(typeof(DmMongoClient))
                .AsSelf()
                .AsImplementedInterfaces()
                .InstancePerLifetimeScope();
            builder.RegisterModule<SearchEngineModule>();
            builder.Populate(services);

            var container = builder.Build();
            return new AutofacServiceProvider(container);
        }

        /// <summary>
        /// Configure application
        /// </summary>
        /// <param name="appBuilder"></param>
        public void Configure(IApplicationBuilder appBuilder)
        {
            appBuilder
                .UseMiddleware<CorrelationMiddleware>()
                .UseMiddleware<ErrorHandlingMiddleware>()
                .UseMiddleware<AuthenticationMiddleware>()
                .UseSwagger()
                .UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/swagger/v1/swagger.json", "DM.API V1");
                    c.RoutePrefix = string.Empty;
                    c.DocumentTitle = "DM.API";
                })
                .UseMvc();
        }
    }
}