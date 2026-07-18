using Hase.Runtime.Transport.Discovery;
using Hase.Transport.Discovery;

namespace Hase.ProtocolExplorer.Scenarios;

/// <summary>
/// Browses for HASE network endpoints and verifies each candidate
/// through Protocol Version 1.
/// </summary>
internal sealed class NetworkDiscoveryScenario
    : IParameterizedScenario
{
    private static readonly TimeSpan VerificationTimeout =
        TimeSpan.FromSeconds(
            3);

    public string Name =>
        "network-discovery";

    public void Execute(
        IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(
            arguments);

        if (arguments.Count != 0)
        {
            throw new ArgumentException(
                "Network discovery does not accept arguments.",
                nameof(arguments));
        }

        ExecuteAsync()
            .GetAwaiter()
            .GetResult();
    }

    private static async Task ExecuteAsync()
    {
        WriteHeader();

        INetworkEndpointBrowser browser =
            new MdnsNetworkEndpointBrowser();

        INetworkEndpointCandidateVerifier verifier =
            new TcpProtocolNetworkEndpointCandidateVerifier();

        INetworkEndpointDiscoveryService discoveryService =
            new NetworkEndpointDiscoveryService(
                browser,
                verifier);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        ConsoleCancelEventHandler cancelHandler =
            (
                sender,
                eventArgs) =>
            {
                eventArgs.Cancel =
                    true;

                cancellationTokenSource.Cancel();
            };

        Console.CancelKeyPress +=
            cancelHandler;

        try
        {
            await foreach (
                NetworkEndpointVerificationResult result
                in discoveryService.DiscoverAsync(
                    VerificationTimeout,
                    cancellationTokenSource.Token))
            {
                WriteResult(
                    result);
            }
        }
        catch (OperationCanceledException)
            when (cancellationTokenSource
                .IsCancellationRequested)
        {
            Console.WriteLine();
            Console.WriteLine(
                "Network discovery stopped.");
        }
        finally
        {
            Console.CancelKeyPress -=
                cancelHandler;
        }
    }

    private static void WriteHeader()
    {
        const string title =
            "Network Discovery";

        Console.WriteLine(
            title);

        Console.WriteLine(
            new string(
                '=',
                title.Length));

        Console.WriteLine();

        Console.WriteLine(
            "Browse for _hase._tcp.local services and verify each "
            + "candidate through HASE Protocol Version 1.");

        Console.WriteLine();

        Console.WriteLine(
            "Service type         : _hase._tcp.local");

        Console.WriteLine(
            "IP version           : IPv4");

        Console.WriteLine(
            $"Verification timeout : "
            + $"{VerificationTimeout.TotalSeconds:0} seconds");

        Console.WriteLine();

        Console.WriteLine(
            "mDNS advertisements provide candidate reachability only.");

        Console.WriteLine(
            "Endpoint identity is accepted only from DiscoverResponse.");

        Console.WriteLine(
            "Discovered endpoints are not attached to the runtime.");

        Console.WriteLine();

        Console.WriteLine(
            "Press Ctrl+C to stop.");

        Console.WriteLine();

        Console.WriteLine(
            "Starting network discovery.");

        Console.WriteLine();
    }

    private static void WriteResult(
        NetworkEndpointVerificationResult result)
    {
        ArgumentNullException.ThrowIfNull(
            result);

        NetworkEndpointCandidate candidate =
            result.Candidate;

        Console.WriteLine(
            $"[{DateTimeOffset.UtcNow:O}]");

        Console.WriteLine(
            $"  Service  : {candidate.ServiceInstanceName}");

        Console.WriteLine(
            $"  Candidate: {candidate.Address}:{candidate.Port}");

        switch (result)
        {
            case VerifiedNetworkEndpoint verifiedEndpoint:
                Console.WriteLine(
                    "  Result   : Verified");

                Console.WriteLine(
                    $"  Endpoint : "
                    + $"{verifiedEndpoint.EndpointId.Value}");

                break;

            case RejectedNetworkEndpointCandidate rejectedCandidate:
                Console.WriteLine(
                    "  Result   : Rejected");

                Console.WriteLine(
                    $"  Failure  : "
                    + $"{rejectedCandidate.Failure}");

                Console.WriteLine(
                    $"  Detail   : "
                    + $"{rejectedCandidate.Detail}");

                break;

            default:
                throw new InvalidOperationException(
                    $"Unsupported discovery result type "
                    + $"'{result.GetType().FullName}'.");
        }

        Console.WriteLine();
    }
}
