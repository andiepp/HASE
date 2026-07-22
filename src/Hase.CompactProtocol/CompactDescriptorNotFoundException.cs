using Hase.Core.Domain.Descriptors;

namespace Hase.CompactProtocol;

internal sealed class CompactDescriptorNotFoundException : IOException
{
    public CompactDescriptorNotFoundException(
        DescriptorReference descriptorReference)
        : base(
            CreateMessage(
                descriptorReference))
    {
        DescriptorReference =
            descriptorReference;
    }

    public DescriptorReference DescriptorReference
    {
        get;
    }

    private static string CreateMessage(
        DescriptorReference descriptorReference)
    {
        ArgumentNullException.ThrowIfNull(
            descriptorReference);

        return
            $"Descriptor '{descriptorReference.Id.Value}' version "
            + $"{descriptorReference.Version} is not available in the host "
            + "descriptor repository.";
    }
}