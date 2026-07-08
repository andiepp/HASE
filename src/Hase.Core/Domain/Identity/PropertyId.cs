namespace Hase.Core.Domain.Identity;

public sealed record PropertyId : HaseId
{
    public PropertyId(string value) : base(value)
    {
    }
}
