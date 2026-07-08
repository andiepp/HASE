namespace Hase.Core.Domain.Identity;

public sealed record EndpointId : HaseId
{
    public EndpointId(string value) : base(value)
    {
    }
}
