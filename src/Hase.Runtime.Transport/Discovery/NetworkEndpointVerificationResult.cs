using Hase.Core.Domain.Identity;
using Hase.Transport.Discovery;

namespace Hase.Runtime.Transport.Discovery;

/// <summary>
/// Represents the outcome of verifying one discovered network
/// endpoint candidate.
/// </summary>
public abstract record NetworkEndpointVerificationResult
{
    protected NetworkEndpointVerificationResult(
        NetworkEndpointCandidate candidate)
    {
        Candidate =
            candidate
            ?? throw new ArgumentNullException(
                nameof(candidate));
    }

    /// <summary>
    /// Gets the candidate that was verified.
    /// </summary>
    public NetworkEndpointCandidate Candidate
    {
        get;
    }
}

/// <summary>
/// Represents a candidate verified through the authoritative HASE
/// Protocol Version 1 discovery exchange.
/// </summary>
public sealed record VerifiedNetworkEndpoint
    : NetworkEndpointVerificationResult
{
    /// <summary>
    /// Initializes a verified network endpoint.
    /// </summary>
    public VerifiedNetworkEndpoint(
        NetworkEndpointCandidate candidate,
        EndpointId endpointId)
        : base(
            candidate)
    {
        EndpointId =
            endpointId
            ?? throw new ArgumentNullException(
                nameof(endpointId));
    }

    /// <summary>
    /// Gets the authoritative endpoint identity returned by the
    /// physical HASE endpoint.
    /// </summary>
    public EndpointId EndpointId
    {
        get;
    }
}

/// <summary>
/// Represents a candidate that could not be verified as a HASE
/// endpoint.
/// </summary>
public sealed record RejectedNetworkEndpointCandidate
    : NetworkEndpointVerificationResult
{
    /// <summary>
    /// Initializes a rejected candidate result.
    /// </summary>
    public RejectedNetworkEndpointCandidate(
        NetworkEndpointCandidate candidate,
        NetworkEndpointVerificationFailure failure,
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
    public NetworkEndpointVerificationFailure Failure
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