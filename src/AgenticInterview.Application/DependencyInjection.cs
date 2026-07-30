using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace AgenticInterview.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddAutoMapper(cfg => cfg.AddMaps(Assembly.GetExecutingAssembly()));
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
        
        // Removed AddMediatR from here to put it in Program.cs
        
        return services;
    }
}
