using Avalonia.Controls;
using SekaiToolsApp.ViewModels;

namespace SekaiToolsApp.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }
}
