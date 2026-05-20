using SekaiToolsMauiText.View.Translate;

namespace SekaiToolsMauiText;

public partial class AppShell : Shell
{
    public AppShell(TranslatePage translatePage)
    {
        InitializeComponent();
        Items.Add(new ShellContent
        {
            Title = "翻译",
            Route = nameof(TranslatePage),
            ContentTemplate = new DataTemplate(() => translatePage)
        });
    }
}
