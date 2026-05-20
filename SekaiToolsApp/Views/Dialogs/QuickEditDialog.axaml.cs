using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using SekaiToolsApp.ViewModels;

namespace SekaiToolsApp.Views.Dialogs;

public partial class QuickEditDialog : Window
{
    public QuickEditDialog()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public QuickEditDialog(QuickEditDialogViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    public QuickEditDialogViewModel ViewModel => (QuickEditDialogViewModel)DataContext!;

    public bool Confirmed { get; private set; }

    private void OnConfirmClicked(object? sender, RoutedEventArgs e)
    {
        Confirmed = true;
        Close(true);
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private void OnTranslatedTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox) return;
        if (e.Key != Key.Enter) return;
        var lineCount = (textBox.Text ?? string.Empty).Split('\n').Length;
        if (lineCount >= 2) e.Handled = true;
    }

    private void OnTranslatedTextBoxTextInput(object? sender, TextInputEventArgs e)
    {
        if (sender is not TextBox textBox) return;
        var text = textBox.Text ?? string.Empty;
        var input = e.Text ?? string.Empty;
        var start = Math.Min(textBox.SelectionStart, textBox.SelectionEnd);
        var end = Math.Max(textBox.SelectionStart, textBox.SelectionEnd);
        var newText = text.Remove(start, end - start).Insert(start, input);
        if (newText.Split('\n').Length > 2) e.Handled = true;
    }
}
