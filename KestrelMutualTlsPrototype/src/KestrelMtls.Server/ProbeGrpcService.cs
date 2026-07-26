using Grpc.AspNetCore.Server;
using Grpc.Core;
using KestrelMtls.Grpc;

namespace KestrelMtls.Server;

public sealed class ProbeGrpcService : GrpcProbe.GrpcProbeBase
{
    private static int executionCount;

    public override Task<ProbeReply> Probe(
        ProbeRequest request,
        ServerCallContext context)
    {
        var currentExecutionCount = Interlocked.Increment(
            ref executionCount);
        Console.WriteLine(
            $"gRPC probe execution count: {currentExecutionCount}");

        var httpContext = context.GetHttpContext();
        var clientCertificate =
            httpContext.Connection.ClientCertificate
            ?? throw new InvalidOperationException(
                "The TLS connection has no client certificate.");

        return Task.FromResult(
            new ProbeReply
            {
                Authenticated = true,
                ClientSubject = clientCertificate.Subject,
                Protocol = httpContext.Request.Protocol,
            });
    }
}
