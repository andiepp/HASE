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
        if (TryGetInstrument(
                sender) is { } instrument)
        {
            instrument.IsInvokingModeCommand =
                isActive;
        }
    }

    private void OnInputCommandPreviewMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs eventArgs)
    {
        SetInputCommandInteractionState(
            sender,
            true);
    }

    private void OnInputCommandPreviewMouseLeftButtonUp(
        object sender,
        MouseButtonEventArgs eventArgs)
    {
        ScheduleInputCommandInteractionRelease(
            sender);
    }

    private void OnInputCommandLostMouseCapture(
        object sender,
        MouseEventArgs eventArgs)
    {
        ScheduleInputCommandInteractionRelease(
            sender);
    }

    private void OnInputCommandPreviewKeyDown(
        object sender,
        KeyEventArgs eventArgs)
    {
        if (IsModeCommandActivationKey(
                eventArgs.Key))
        {
            SetInputCommandInteractionState(
                sender,
                true);
        }
    }

    private void OnInputCommandPreviewKeyUp(
        object sender,
        KeyEventArgs eventArgs)
    {
        if (IsModeCommandActivationKey(
                eventArgs.Key))
        {
            ScheduleInputCommandInteractionRelease(
                sender);
        }
    }

    private static void ScheduleInputCommandInteractionRelease(
        object sender)
    {
        if (sender is FrameworkElement element)
        {
            element.Dispatcher.BeginInvoke(
                DispatcherPriority.ContextIdle,
                new Action(
                    () =>
                        SetInputCommandInteractionState(
                            element,
                            false)));
        }
    }

    private static void SetInputCommandInteractionState(
        object sender,
        bool isActive)
    {
        if (TryGetInstrument(
                sender) is { } instrument)
        {
            instrument.IsInvokingInputCommand =
                isActive;
        }
    }

    private static InstrumentInventoryItemViewModel? TryGetInstrument(
        object sender) =>
        sender is FrameworkElement element
            ? element.Tag as InstrumentInventoryItemViewModel
                ?? element.DataContext as InstrumentInventoryItemViewModel
            : null;

    private void OnShortCircuitCommandPreviewMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs eventArgs)
    {
        SetShortCircuitCommandInteractionState(
            sender,
            true);
    }

    private void OnShortCircuitCommandPreviewMouseLeftButtonUp(
        object sender,
        MouseButtonEventArgs eventArgs)
    {
        ScheduleShortCircuitCommandInteractionRelease(
            sender);
    }

    private void OnShortCircuitCommandLostMouseCapture(
        object sender,
        MouseEventArgs eventArgs)
    {
        ScheduleShortCircuitCommandInteractionRelease(
            sender);
    }

    private void OnShortCircuitCommandPreviewKeyDown(
        object sender,
        KeyEventArgs eventArgs)
    {
        if (IsModeCommandActivationKey(
                eventArgs.Key))
        {
            SetShortCircuitCommandInteractionState(
                sender,
                true);
        }
    }

    private void OnShortCircuitCommandPreviewKeyUp(
        object sender,
        KeyEventArgs eventArgs)
    {
        if (IsModeCommandActivationKey(
                eventArgs.Key))
        {
            ScheduleShortCircuitCommandInteractionRelease(
                sender);
        }
    }

    private static void ScheduleShortCircuitCommandInteractionRelease(
        object sender)
    {
        if (sender is FrameworkElement element)
        {
            element.Dispatcher.BeginInvoke(
                DispatcherPriority.ContextIdle,
                new Action(
                    () =>
                        SetShortCircuitCommandInteractionState(
                            element,
                            false)));
        }
    }

    private static void SetShortCircuitCommandInteractionState(
        object sender,
        bool isActive)
    {
        if (TryGetInstrument(
                sender) is { } instrument)
        {
            instrument.IsInvokingShortCircuitCommand =
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
