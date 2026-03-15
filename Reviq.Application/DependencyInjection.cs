using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Reviq.Application.Common;

namespace Reviq.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediator(o =>
        {
            o.ServiceLifetime = ServiceLifetime.Scoped;
            o.PipelineBehaviors = [typeof(ValidationBehavior<,>)];
        });
        services.AddValidatorsFromAssemblyContaining<IApplicationMarker>();
        return services;
    }
}