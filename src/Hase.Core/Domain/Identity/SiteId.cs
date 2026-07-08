namespace Hase.Core.Domain.Identity;

public sealed record SiteId : HaseId
{
    public SiteId(string value) : base(value)
    {
    }
}
