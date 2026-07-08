namespace Hase.Core.Domain;

public sealed record SiteId : HaseId
{
    public SiteId(string value) : base(value)
    {
    }
}
