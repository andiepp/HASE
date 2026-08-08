using Hase.Python.CredentialProvisioning.Operator;

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

return await PythonCredentialProvisioningOperator.RunAsync(
    args,
    Console.Out,
    Console.Error,
    new SystemPythonCredentialProvisioningOperations(),
    cancellation.Token);
