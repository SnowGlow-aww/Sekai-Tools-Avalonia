using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using SekaiToolsApp.ViewModels;
using SekaiToolsApp.ViewModels.LineCards;
using SekaiToolsApp.Views.Dialogs;

namespace SekaiToolsApp.Views.Pages.Components;

/// <summary>
/// 对话行卡片。仅承载 XAML 加载与“双击进入快捷编辑”的入口（M1.C 接入）。
/// </summary>
public partial class DialogLineCard : UserControl
{
    public DialogLineCard()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private async void OnContentDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not DialogLineCardViewModel viewModel) return;
        if (TopLevel.GetTopLevel(this) is not Window owner) return;

        var dialog = new QuickEditDialog(new QuickEditDialogViewModel(viewModel.Set));
        var result = await dialog.ShowDialog<bool>(owner);
        if (!result) return;

        viewModel.ApplyQuickEdit(dialog.ViewModel.ContentTranslated, dialog.ViewModel.UseReturn);
    }
}
