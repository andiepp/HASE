namespace Hase.Core.Domain;

public sealed record InstrumentId : HaseId
{
    public InstrumentId(string value) : base(value)
    {
    }
}
