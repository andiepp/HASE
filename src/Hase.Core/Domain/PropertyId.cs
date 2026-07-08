namespace Hase.Core.Domain;

public sealed record PropertyId : HaseId
{
    public PropertyId(string value) : base(value)
    {
    }
}
