namespace Hase.Python.CredentialProvisioning;

/// <summary>
/// Composes preparation and durable publication without hiding either the
/// explicit finalization decision or the explicit recovery boundary.
/// </summary>
public sealed class PythonCredentialRotationOrchestrator
{
    public async Task<PythonCredentialRotationPublicationResult> BeginAsync(
        PythonCredentialRotationPreparationRequest preparationRequest,
        PythonCredentialRotationPublicationRequest publicationRequest,
        PythonClientCredentialMaterial replacement,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preparationRequest);
        ArgumentNullException.ThrowIfNull(publicationRequest);
        ArgumentNullException.ThrowIfNull(replacement);

        using PythonCredentialRotationCandidates candidates =
            await new PythonCredentialRotationPreparer().PrepareAsync(
                preparationRequest, replacement, utcNow, cancellationToken)
                .ConfigureAwait(false);
        return await new PythonCredentialRotationPublisher().BeginAsync(
            publicationRequest, candidates, cancellationToken)
            .ConfigureAwait(false);
    }

    public PythonCredentialRotationPublicationResult Finalize(
        PythonCredentialRotationPublicationRequest request) =>
        new PythonCredentialRotationPublisher().Finalize(request);

    public PythonCredentialRotationPublicationResult Recover(
        PythonCredentialRotationPublicationRequest request) =>
        new PythonCredentialRotationPublisher().Recover(request);
}
