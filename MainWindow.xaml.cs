using System.Windows;
using System.Windows.Media.Animation;
using BedrockLauncher.ViewModels;
namespace BedrockLauncher.Views;

public partial class MainWindow : Window
{
    readonly MainViewModel _vm = new();
    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.CurrentPage) && _vm.Settings.Animations)
                PagesHost.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)));
        };
        SizeChanged += (_, _) => _vm.IsCompact = ActualWidth < 980;   // diseño adaptable
        Loaded += async (_, _) => await _vm.InitializeAsync();
    }
}
