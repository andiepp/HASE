namespace Hase.Core.Domain;

public sealed record EndpointId : HaseId
{
    public EndpointId(string value) : base(value)
    {
    }
}
