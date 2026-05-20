using Avalonia.Controls;
using FluentAvalonia.UI.Controls;

namespace SekaiToolsApp.Views.Pages;

/// <summary>
/// M1 过渡期的页面占位控件。具体页面通过 <see cref="Configure"/> 设置标题/说明/迁移提示。
/// </summary>
public partial class PlaceholderPage : UserControl
{
    public PlaceholderPage()
    {
        InitializeComponent();
    }

    public PlaceholderPage(Symbol icon, string title, string description, string migrationHint)
        : this()
    {
        Configure(icon, title, description, migrationHint);
    }

    protected void Configure(Symbol icon, string title, string description, string migrationHint)
    {
        IconView.Symbol = icon;
        TitleView.Text = title;
        DescriptionView.Text = description;
        MigrationHintView.Text = migrationHint;
    }
}
