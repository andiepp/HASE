using System.Windows;
using System.Windows.Controls;
using Hase.DesktopHost.App.Media;
using Microsoft.Web.WebView2.Wpf;
using Prism.Commands;

namespace Hase.DesktopHost.App.Views;

public partial class MainWindow : Window
{
    private WebView2? mediaCaptureWebView;
    private WebView2? mediaInventoryWebView;

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
        RemoveEmbeddedDiagnosticsPanel();
        AddOpenDiagnosticsButton();
    }

    public DelegateCommand OpenDiagnosticsCommand
    {
        get;
    }

    public WebView2 CreateMediaCaptureWebView(
        string userDataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userDataDirectory);
        if (mediaCaptureWebView is not null)
        {
            throw new InvalidOperationException(
                "The Runtime Host media capture surface already exists.");
        }

        mediaCaptureWebView = new WebView2
        {
            Width = 1,
            Height = 1,
            IsHitTestVisible = false,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            CreationProperties =
                RuntimeHostMediaWebView2Custody.CreateCreationProperties(
                    userDataDirectory)
        };
        ResolveContentGrid().Children.Add(mediaCaptureWebView);
        return mediaCaptureWebView;
    }

    public WebView2 CreateMediaInventoryWebView(string userDataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userDataDirectory);
        if (mediaInventoryWebView is not null)
        {
            throw new InvalidOperationException(
                "The Runtime Host media inventory surface already exists.");
        }

        mediaInventoryWebView = new WebView2
        {
            Width = 1,
            Height = 1,
            IsHitTestVisible = false,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            CreationProperties =
                RuntimeHostMediaWebView2Custody.CreateCreationProperties(
                    userDataDirectory)
        };
        ResolveContentGrid().Children.Add(mediaInventoryWebView);
        return mediaInventoryWebView;
    }

    private void RemoveEmbeddedDiagnosticsPanel()
    {
        Grid contentGrid =
            ResolveContentGrid();

        GroupBox diagnosticsPanel =
            contentGrid.Children
                .OfType<GroupBox>()
                .Single(
                    groupBox =>
                        string.Equals(
                            groupBox.Header as string,
                            "Runtime Diagnostics",
                            StringComparison.Ordinal));

        contentGrid.Children.Remove(
            diagnosticsPanel);
    }

    private void AddOpenDiagnosticsButton()
    {
        Grid contentGrid =
            ResolveContentGrid();

        StackPanel header =
            contentGrid.Children
                .OfType<StackPanel>()
                .First(
                    panel =>
                        Grid.GetRow(panel) == 0);

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
                    HorizontalAlignment.Left,
                Command =
                    OpenDiagnosticsCommand
            };

        header.Children.Add(
            button);
    }

    private Grid ResolveContentGrid()
    {
        if (Content is ScrollViewer scrollViewer
            && scrollViewer.Content is Grid contentGrid)
        {
            return contentGrid;
        }

        throw new InvalidOperationException(
            "The main-window content grid could not be resolved.");
    }
}
