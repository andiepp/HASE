using System.Windows;
using System.Windows.Controls;
using Hase.Client.Wpf.ViewModels;

namespace Hase.Client.Wpf.Views;

public partial class MediaWindow : Window
{
    public MediaWindow(RuntimeHostMediaViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }

    public ContentControl MediaPresentationSurface => MediaPresentationHost;
}
