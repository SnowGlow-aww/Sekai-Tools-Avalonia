using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace SekaiToolsApp.Views.Pages.Components;

public partial class TranslateLineDialogView : UserControl
{
    public TranslateLineDialogView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
