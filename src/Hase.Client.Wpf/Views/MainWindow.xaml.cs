using System.Windows;
using System.Windows.Input;
using Hase.Client.Wpf.ViewModels;

namespace Hase.Client.Wpf.Views;

public partial class MainWindow
    : Window
{
    public MainWindow()
    {
        InitializeComponent();
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
