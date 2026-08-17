using System.ComponentModel;
using System.IO;
using System.Windows;
using Hase.DesktopHost.App.Hosting;
using Hase.DesktopHost.App.Media;
using Hase.DesktopHost.Configuration;

namespace Hase.DesktopHost.App.Views;

public partial class RuntimeHostMediaBindingWindow : Window
{
    private readonly RuntimeHostMediaBindingStartupRequest request;
    private readonly WebView2RuntimeHostMediaBindingBoundary boundary;
    private bool terminal;

    public RuntimeHostMediaBindingWindow(
        RuntimeHostMediaBindingStartupRequest request,
        string assetDirectory)
    {
        this.request = request ?? throw new ArgumentNullException(nameof(request));
        InitializeComponent();
        boundary = new WebView2RuntimeHostMediaBindingBoundary(
            BindingWebView,
            assetDirectory);
        boundary.ValidatedMessage += OnValidatedMessage;
        Loaded += OnLoaded;
        Closing += OnClosing;
        Closed += OnClosed;
    }

    private async void OnLoaded(object sender, RoutedEventArgs eventArgs)
    {
        Loaded -= OnLoaded;
        try
        {
            await boundary.InitializeAsync();
            OutcomeText.Text =
                "Binding mode is ready. Device access occurs only after Discover devices is selected.";
        }
        catch
        {
            terminal = true;
            OutcomeText.Text =
                "Binding mode could not initialize. No candidate was written.";
        }
    }

    private async void OnValidatedMessage(
        RuntimeHostMediaBindingWebMessage message)
    {
        switch (message.Kind)
        {
            case RuntimeHostMediaBindingWebMessageKind.Ready:
                OutcomeText.Text = "Local binding page ready.";
                break;
            case RuntimeHostMediaBindingWebMessageKind.DiscoveryRequested:
                OutcomeText.Text =
                    "Explicit local device discovery is in progress.";
                break;
            case RuntimeHostMediaBindingWebMessageKind.SelectionConfirmed:
                await WriteCandidateAsync(message);
                break;
            case RuntimeHostMediaBindingWebMessageKind.Cancelled:
                terminal = true;
                Close();
                break;
            case RuntimeHostMediaBindingWebMessageKind.Faulted:
                OutcomeText.Text =
                    "Device discovery failed. No candidate was written; retry or close this window.";
                break;
        }
    }

    private async Task WriteCandidateAsync(
        RuntimeHostMediaBindingWebMessage message)
    {
        if (terminal || string.IsNullOrWhiteSpace(message.VideoDeviceId))
        {
            return;
        }
        terminal = true;
        try
        {
            DesktopRuntimeHostMediaBindingCandidate candidate =
                request.CreateCandidate(
                    message.VideoDeviceId,
                    message.AudioDeviceId);
            await DesktopRuntimeHostMediaBindingCandidateFile.WriteNewAsync(
                request.OutputFilePath,
                candidate);
            OutcomeText.Text =
                "The protected media binding candidate was written successfully.";
            MessageBox.Show(
                "The protected media binding candidate was written. It is not active.",
                "HASE Runtime Host Media Binding",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Close();
        }
        catch (IOException)
        {
            terminal = false;
            OutcomeText.Text =
                "The candidate could not be written. Existing files were not overwritten.";
        }
        catch (UnauthorizedAccessException)
        {
            terminal = false;
            OutcomeText.Text =
                "The candidate could not be written because custody validation failed.";
        }
        catch (ArgumentException)
        {
            terminal = false;
            OutcomeText.Text =
                "The selected binding was rejected. No candidate was written.";
        }
    }

    private void OnClosing(object? sender, CancelEventArgs eventArgs)
    {
        boundary.ValidatedMessage -= OnValidatedMessage;
    }

    private async void OnClosed(object? sender, EventArgs eventArgs)
    {
        Closing -= OnClosing;
        Closed -= OnClosed;
        await boundary.DisposeAsync();
        BindingWebView.Dispose();
    }
}
