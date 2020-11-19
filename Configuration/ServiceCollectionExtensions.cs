using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SER.Utilitties.NetCore.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SER.Utilitties.NetCore.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SER.Utilitties.NetCore.SerRest;
using SER.Utilitties.NetCore.Managers;
using SER.Utilitties.NetCore.Utilities;
using SER.AmazonS3;

namespace SER.Utilitties.NetCore.Configuration
{
    public static class ServiceCollectionExtensions
    {
        public static void AddSerUtilites<TContext>(this IServiceCollection services)
          where TContext : DbContext
        {
            services.AddScoped<AuditManager>();          
            services.AddScoped<AmazonFileService>();

            services.AddSingleton<PostgresQLService>();
            services.AddSingleton<XlsxHelpers>();
            services.AddSingleton<ExcelService>();
            services.AddSingleton<Consume>();

            AddScopedModelsDynamic<TContext>(services);
        }

        public static void AddScopedModelsDynamic<TContext>(this IServiceCollection services)
            where TContext : DbContext
        {
            var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(x => x.GetName().Name == typeof(TContext).Assembly.GetName().Name);
            var assemblyName = assembly.GetName().Name;
            foreach (var type in assembly.GetTypes().Where(x => !x.IsAbstract && x.IsClass
                && x.Namespace == $"{assemblyName}.Models" && x.BaseType != typeof(Object) && x.GetProperties().Any(y => y.Name == "Id"
                    || y.Name == "id")))
            {
                var interfaceType = typeof(IRepository<>).MakeGenericType(new Type[] { type });
                var inherateType = typeof(GenericModelFactory<,>).MakeGenericType(new Type[] { type, typeof(TContext) });
                ServiceLifetime serviceLifetime = ServiceLifetime.Scoped;
                //Console.WriteLine($"Dependencia IRepository registrada type {type.Name}");
                services.TryAdd(new ServiceDescriptor(interfaceType, inherateType, serviceLifetime));
            }
        }
    }
}
