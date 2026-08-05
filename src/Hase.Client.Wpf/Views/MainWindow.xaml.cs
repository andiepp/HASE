using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Hase.Client.Wpf.ViewModels;

namespace Hase.Client.Wpf.Views;

public partial class MainWindow
    : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnModeCommandPreviewMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs eventArgs)
    {
        SetModeCommandInteractionState(
            sender,
            true);
    }

    private void OnModeCommandPreviewMouseLeftButtonUp(
        object sender,
        MouseButtonEventArgs eventArgs)
    {
        ScheduleModeCommandInteractionRelease(
            sender);
    }

    private void OnModeCommandLostMouseCapture(
        object sender,
        MouseEventArgs eventArgs)
    {
        ScheduleModeCommandInteractionRelease(
            sender);
    }

    private void OnModeCommandPreviewKeyDown(
        object sender,
        KeyEventArgs eventArgs)
    {
        if (IsModeCommandActivationKey(
                eventArgs.Key))
        {
            SetModeCommandInteractionState(
                sender,
                true);
        }
    }

    private void OnModeCommandPreviewKeyUp(
        object sender,
        KeyEventArgs eventArgs)
    {
        if (IsModeCommandActivationKey(
                eventArgs.Key))
        {
            ScheduleModeCommandInteractionRelease(
                sender);
        }
    }

    private static bool IsModeCommandActivationKey(
        Key key) =>
        key is Key.Enter or Key.Space;

    private static void ScheduleModeCommandInteractionRelease(
        object sender)
    {
        if (sender is FrameworkElement element)
        {
            element.Dispatcher.BeginInvoke(
                DispatcherPriority.ContextIdle,
                new Action(
                    () =>
                        SetModeCommandInteractionState(
                            element,
                            false)));
        }
    }

    private static void SetModeCommandInteractionState(
        object sender,
        bool isActive)
    {
        if (sender is FrameworkElement
            {
                DataContext:
                    InstrumentInventoryItemViewModel instrument
            })
        {
            instrument.IsInvokingModeCommand =
                isActive;
        }
    }

    private void OnCommandArgumentGotKeyboardFocus(
        object sender,
        KeyboardFocusChangedEventArgs eventArgs)
    {
        if (sender is FrameworkElement
            {
                DataContext:
                    CommandInventoryItemViewModel command
            })
        {
            command.IsEditingArgument =
                true;
        }
    }

    private void OnCommandArgumentLostKeyboardFocus(
        object sender,
        KeyboardFocusChangedEventArgs eventArgs)
    {
        if (sender is FrameworkElement
            {
                DataContext:
                    CommandInventoryItemViewModel command
            })
        {
            command.IsEditingArgument =
                false;
        }
    }

    private void OnPropertyValueGotKeyboardFocus(
        object sender,
        KeyboardFocusChangedEventArgs eventArgs)
    {
        if (sender is FrameworkElement
            {
                DataContext:
                    PropertyInventoryItemViewModel property
            })
        {
            property.IsEditingRequestedValue =
                true;
        }
    }

    private void OnPropertyValueLostKeyboardFocus(
        object sender,
        KeyboardFocusChangedEventArgs eventArgs)
    {
        if (sender is FrameworkElement
            {
                DataContext:
                    PropertyInventoryItemViewModel property
            })
        {
            property.IsEditingRequestedValue =
                false;
        }
    }
}
