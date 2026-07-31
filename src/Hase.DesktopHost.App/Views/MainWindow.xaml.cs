using System.Windows;
using System.Windows.Controls;
using Prism.Commands;

namespace Hase.DesktopHost.App.Views;

public partial class MainWindow : Window
{
    public MainWindow(
        IDesktopDiagnosticsWindowService
            diagnosticsWindowService)
    {
        ArgumentNullException.ThrowIfNull(
            diagnosticsWindowService);

        OpenDiagnosticsCommand =
            new DelegateCommand(
                diagnosticsWindowService.Open);

        InitializeComponent();
        AddOpenDiagnosticsButton();
    }

    public DelegateCommand OpenDiagnosticsCommand
    {
        get;
    }

    private void AddOpenDiagnosticsButton()
    {
        if (Content is not ScrollViewer scrollViewer
            || scrollViewer.Content is not Grid contentGrid
            || contentGrid.Children
                .OfType<StackPanel>()
                .FirstOrDefault(
                    panel =>
                        Grid.GetRow(panel) == 0)
                is not StackPanel header)
        {
            throw new InvalidOperationException(
                "The main-window header could not be resolved.");
        }

        var button =
            new Button
            {
                Content =
                    "Open Diagnostics",
                Margin =
                    new Thickness(
                        0,
                        16,
                        0,
                        0),
                MinWidth =
                    150,
                HorizontalAlignment =
                    HorizontalAlignment.Left
            };

        button.Command =
            OpenDiagnosticsCommand;

        header.Children.Add(
            button);
    }
}
