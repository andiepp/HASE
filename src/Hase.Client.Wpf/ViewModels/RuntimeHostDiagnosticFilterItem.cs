using Hase.Client.Configuration;

namespace Hase.Client.Wpf.ViewModels;

public sealed record RuntimeHostDiagnosticFilterItem(
    string DisplayName,
    RuntimeHostProfileId? ProfileId)
{
    public override string ToString() => DisplayName;
}
