namespace Hase.Core.Domain.Endpoints;

public sealed record EndpointMetadata
{
    public string? DisplayName { get; init; }

    public string? Description { get; init; }
}