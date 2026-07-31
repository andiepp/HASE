using Hase.DesktopHost.App.Views;

namespace Hase.DesktopHost.Tests;

public sealed class SingleInstanceDesktopWindowControllerTests
{
    [Fact]
    public void Open_FirstInvocation_ShouldShowAndActivateWindow()
    {
        var window =
            new RecordingWindow();
        var controller =
            new SingleInstanceDesktopWindowController(
                () => window);

        controller.Open();

        Assert.Equal(
            1,
            window.ShowCount);
        Assert.Equal(
            1,
            window.ActivateCount);
        Assert.Equal(
            0,
            window.RestoreCount);
    }

    [Fact]
    public void Open_RepeatedInvocation_ShouldActivateExistingWindow()
    {
        var createdWindows =
            new List<RecordingWindow>();
        var controller =
            new SingleInstanceDesktopWindowController(
                () =>
                {
                    var window =
                        new RecordingWindow();
                    createdWindows.Add(
                        window);
                    return window;
                });

        controller.Open();
        controller.Open();

        RecordingWindow window =
            Assert.Single(
                createdWindows);
        Assert.Equal(
            1,
            window.ShowCount);
        Assert.Equal(
            2,
            window.ActivateCount);
    }

    [Fact]
    public void Open_MinimizedExistingWindow_ShouldRestoreBeforeActivation()
    {
        var window =
            new RecordingWindow
            {
                IsMinimized =
                    true
            };
        var controller =
            new SingleInstanceDesktopWindowController(
                () => window);

        controller.Open();
        window.IsMinimized =
            true;
        controller.Open();

        Assert.Equal(
            1,
            window.RestoreCount);
        Assert.False(
            window.IsMinimized);
        Assert.Equal(
            2,
            window.ActivateCount);
    }

    [Fact]
    public void Open_AfterClose_ShouldCreateFreshWindow()
    {
        var createdWindows =
            new List<RecordingWindow>();
        var controller =
            new SingleInstanceDesktopWindowController(
                () =>
                {
                    var window =
                        new RecordingWindow();
                    createdWindows.Add(
                        window);
                    return window;
                });

        controller.Open();
        createdWindows[0].Close();
        controller.Open();

        Assert.Equal(
            2,
            createdWindows.Count);
        Assert.Equal(
            1,
            createdWindows[1].ShowCount);
        Assert.Equal(
            1,
            createdWindows[1].ActivateCount);
    }

    private sealed class RecordingWindow
        : IDesktopModelessWindow
    {
        public event EventHandler? Closed;

        public bool IsMinimized
        {
            get;
            set;
        }

        public int RestoreCount
        {
            get;
            private set;
        }

        public int ShowCount
        {
            get;
            private set;
        }

        public int ActivateCount
        {
            get;
            private set;
        }

        public void Restore()
        {
            RestoreCount++;
            IsMinimized =
                false;
        }

        public void ShowWindow()
        {
            ShowCount++;
        }

        public void ActivateWindow()
        {
            ActivateCount++;
        }

        public void Close()
        {
            Closed?.Invoke(
                this,
                EventArgs.Empty);
        }
    }
}
