using System.Windows;
using System.Windows.Data;
namespace Hase.Client.Wpf.RfLab.Tests;

/// <summary>
/// The ported numeric control owns its own data context, which silently
/// defeats any binding written against it. These tests pin that constraint
/// and the binding shape the panel must therefore use.
/// </summary>
public sealed class NCMultiDigitBindingTests
{
    [Fact]
    public void TheControl_ShouldOwnItsDataContext()
    {
        // This is the constraint the panel has to work around. If it ever
        // stops being true, the explicit binding sources in the panel can be
        // simplified back to plain ones.
        object? dataContext = OnStaThread(() =>
        {
            var control = new NCMultiDigit();
            return control.DataContext;
        });

        Assert.IsType<NCMultiDigit>(dataContext);
    }

    [Fact]
    public void APlainBinding_ShouldFailToReachTheViewModel()
    {
        // The defect: the binding resolves against the control, which has no
        // such property, so the control keeps its default enabled state and
        // never follows the view model.
        bool enabled = OnStaThread(() =>
        {
            var window = CreateWindow(clockPresent: false, out NCMultiDigit control);
            control.SetBinding(
                UIElement.IsEnabledProperty,
                new Binding("IsClockGeneratorPresent"));
            window.Show();
            window.Close();
            return control.IsEnabled;
        });

        Assert.True(enabled);
    }

    [Fact]
    public void AnExplicitlySourcedBinding_ShouldFollowTheViewModel()
    {
        // The fix: naming the source reaches past the control's own data
        // context, so the control follows the panel.
        (bool whenAbsent, bool whenPresent) = OnStaThread(() =>
        {
            var window = CreateWindow(clockPresent: false, out NCMultiDigit control);
            control.SetBinding(
                UIElement.IsEnabledProperty,
                new Binding("DataContext.IsClockGeneratorPresent")
                {
                    RelativeSource = new RelativeSource(
                        RelativeSourceMode.FindAncestor)
                    {
                        AncestorType = typeof(Window)
                    }
                });
            window.Show();

            bool absent = control.IsEnabled;
            ((ClockPresenceStub)window.DataContext).IsClockGeneratorPresent = true;
            bool present = control.IsEnabled;

            window.Close();
            return (absent, present);
        });

        Assert.False(whenAbsent);
        Assert.True(whenPresent);
    }

    private static Window CreateWindow(bool clockPresent, out NCMultiDigit control)
    {
        control = new NCMultiDigit();
        var window = new Window
        {
            DataContext = new ClockPresenceStub
            {
                IsClockGeneratorPresent = clockPresent
            },
            Content = control,
            Width = 100,
            Height = 100,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
            ShowActivated = false,
            Left = -10000,
            Top = -10000
        };

        return window;
    }

    private static T OnStaThread<T>(Func<T> action)
    {
        T result = default!;
        Exception? failure = null;

        var thread = new Thread(() =>
        {
            try
            {
                result = action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            throw new InvalidOperationException(
                "The user interface thread failed.",
                failure);
        }

        return result;
    }

    /// <summary>
    /// Stands in for the panel, carrying only the member the clock controls
    /// bind to.
    /// </summary>
    private sealed class ClockPresenceStub
        : System.ComponentModel.INotifyPropertyChanged
    {
        private bool isClockGeneratorPresent;

        public event System.ComponentModel.PropertyChangedEventHandler?
            PropertyChanged;

        public bool IsClockGeneratorPresent
        {
            get => isClockGeneratorPresent;
            set
            {
                if (isClockGeneratorPresent == value)
                {
                    return;
                }

                isClockGeneratorPresent = value;
                PropertyChanged?.Invoke(
                    this,
                    new System.ComponentModel.PropertyChangedEventArgs(
                        nameof(IsClockGeneratorPresent)));
            }
        }
    }
}
