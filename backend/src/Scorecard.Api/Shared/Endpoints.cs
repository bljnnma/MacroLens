using System.Reflection;

namespace Scorecard.Api.Shared;

/// <summary>
/// Implemented by every vertical slice. Endpoints are discovered by assembly
/// scan, so adding a feature never means touching Program.cs — which is what
/// keeps the slicing honest as the surface grows.
/// </summary>
public interface IEndpoint
{
    void Map(IEndpointRouteBuilder app);
}

public static class EndpointExtensions
{
    public static IEndpointRouteBuilder MapFeatureEndpoints(this IEndpointRouteBuilder app)
    {
        var endpoints = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(IEndpoint).IsAssignableFrom(t))
            .Select(Activator.CreateInstance)
            .Cast<IEndpoint>();

        foreach (var endpoint in endpoints) endpoint.Map(app);
        return app;
    }
}
