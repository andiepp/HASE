using System.Windows;
using Hase.Client.Wpf.Views;
using Prism.DryIoc;
using Prism.Ioc;

namespace Hase.Client.Wpf;

public partial class App
    : PrismApplication
{
    protected override Window CreateShell() =>
        Container.Resolve<MainWindow>();

    protected override void RegisterTypes(
        IContainerRegistry containerRegistry)
    {
    }
}
