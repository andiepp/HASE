namespace Hase.Core.Domain.Identity;

public sealed record InstrumentId : HaseId
{
    public InstrumentId(string value) : base(value)
    {
    }
}
