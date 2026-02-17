using Microsoft.Extensions.DependencyInjection;
using Pefi.Bank.Functions.Projections;

namespace Pefi.Bank.Functions.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddProjections(this IServiceCollection services)
    {
        var assembly = typeof(IProjectionHandler).Assembly;

        // Auto-discover and register all IProjectionHandler implementations
        foreach (var type in assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.IsAssignableTo(typeof(IProjectionHandler))))
            services.AddSingleton(typeof(IProjectionHandler), type);

        // Auto-discover and register all ISagaExecutor implementations
        foreach (var type in assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.IsAssignableTo(typeof(ISagaExecutor))))
            services.AddSingleton(typeof(ISagaExecutor), type);


        return services;
    }

        public static IServiceCollection AddSagas(this IServiceCollection services)
    {
        var assembly = typeof(IProjectionHandler).Assembly;


        // Auto-discover and register all ISagaExecutor implementations
        foreach (var type in assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.IsAssignableTo(typeof(ISagaExecutor))))
            services.AddSingleton(typeof(ISagaExecutor), type);


        return services;
    }
}
