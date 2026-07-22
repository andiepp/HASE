using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Identity;
using Hase.Transport.Discovery;

namespace Hase.Runtime.Transport.Discovery;

/// <summary>
/// Represents the outcome of verifying one USB serial endpoint
/// candidate.
/// </summary>
public abstract record UsbSerialEndpointVerificationResult
{
    protected UsbSerialEndpointVerificationResult(
        UsbSerialEndpointCandidate candidate)
    {
        Candidate =
            candidate
            ?? throw new ArgumentNullException(
                nameof(candidate));
    }

    /// <summary>
    /// Gets the USB serial candidate that was verified.
    /// </summary>
    public UsbSerialEndpointCandidate Candidate
    {
        get;
    }
}

/// <summary>
/// Represents a compatible compact endpoint verified through
/// authoritative Compact Serial Protocol bootstrap and exact descriptor
/// resolution.
/// </summary>
public sealed record VerifiedUsbSerialEndpoint
    : UsbSerialEndpointVerificationResult
{
    /// <summary>
    /// Initializes a verified USB serial endpoint result.
    /// </summary>
    public VerifiedUsbSerialEndpoint(
        UsbSerialEndpointCandidate candidate,
        EndpointId endpointId,
        DescriptorReference descriptorReference,
        EndpointDescriptorDefinition descriptorDefinition)
        : base(
            candidate)
    {
        EndpointId =
            endpointId
            ?? throw new ArgumentNullException(
                nameof(endpointId));

        DescriptorReference =
            descriptorReference
            ?? throw new ArgumentNullException(
                nameof(descriptorReference));

        DescriptorDefinition =
            descriptorDefinition
            ?? throw new ArgumentNullException(
                nameof(descriptorDefinition));
    }

    /// <summary>
    /// Gets the authoritative identity returned by compact bootstrap.
    /// </summary>
    public EndpointId EndpointId
    {
        get;
    }

    /// <summary>
    /// Gets the exact descriptor reference returned by compact bootstrap.
    /// </summary>
    public DescriptorReference DescriptorReference
    {
        get;
    }

    /// <summary>
    /// Gets the complete descriptor definition resolved from the host
    /// repository.
    /// </summary>
    public EndpointDescriptorDefinition DescriptorDefinition
    {
        get;
    }
}

/// <summary>
/// Represents a USB serial candidate that could not be verified as a
/// compatible HASE compact endpoint.
/// </summary>
public sealed record RejectedUsbSerialEndpointCandidate
    : UsbSerialEndpointVerificationResult
{
    /// <summary>
    /// Initializes a rejected USB serial candidate result.
    /// </summary>
    public RejectedUsbSerialEndpointCandidate(
        UsbSerialEndpointCandidate candidate,
        UsbSerialEndpointVerificationFailure failure,
        string detail)
        : base(
            candidate)
    {
        if (!Enum.IsDefined(
            failure))
        {
            throw new ArgumentOutOfRangeException(
                nameof(failure),
                failure,
                "The verification failure classification is invalid.");
        }

        if (string.IsNullOrWhiteSpace(
            detail))
        {
            throw new ArgumentException(
                "The verification failure detail must not be empty.",
                nameof(detail));
        }

        Failure =
            failure;

        Detail =
            detail;
    }

    /// <summary>
    /// Gets the classified verification failure.
    /// </summary>
    public UsbSerialEndpointVerificationFailure Failure
    {
        get;
    }

    /// <summary>
    /// Gets diagnostic information about the rejected candidate.
    /// </summary>
    public string Detail
    {
        get;
    }
}
