using System.Windows;
using System.Windows.Threading;
using Hase.Client.Wpf.ViewModels;

namespace Hase.Client.Wpf.Views;

public partial class DiagnosticsWindow : Window
{
    private readonly ClientDiagnosticsViewModel viewModel;
    private readonly DispatcherTimer refreshTimer;
    private long lastSequence;

    public DiagnosticsWindow(ClientDiagnosticsViewModel viewModel)
    {
        this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        DataContext = viewModel;
        refreshTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(250), DispatcherPriority.Background, Refresh, Dispatcher);
        Loaded += (_, _) => refreshTimer.Start();
        Closed += (_, _) => refreshTimer.Stop();
    }

    private void Refresh(object? sender, EventArgs eventArgs)
    {
        viewModel.Refresh();
        long newest = viewModel.Records.LastOrDefault()?.Sequence ?? 0;
        if (newest != 0 && newest != lastSequence)
        {
            RecordsGrid.ScrollIntoView(viewModel.Records[^1]);
        }
        lastSequence = newest;
    }
}
