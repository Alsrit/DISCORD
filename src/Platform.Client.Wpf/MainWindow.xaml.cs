using Platform.Client.Wpf.ViewModels;

namespace Platform.Client.Wpf;

public partial class MainWindow : System.Windows.Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        Loaded += async (_, _) => await _viewModel.InitializeAsync();
    }
}
