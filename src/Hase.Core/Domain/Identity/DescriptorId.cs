namespace Hase.Core.Domain.Identity;

/// <summary>
/// Identifies a complete descriptor stored in a HASE descriptor repository.
/// </summary>
public sealed record DescriptorId : HaseId
{
    public DescriptorId(
        string value)
        : base(
            value)
    {
    }
}