using CommunityToolkit.Mvvm.ComponentModel;
using SekaiToolsBase.Utils;
using SekaiToolsCore.Process.FrameSet;

namespace SekaiToolsApp.ViewModels;

public partial class QuickEditDialogViewModel : ViewModelBase
{
    public QuickEditDialogViewModel(DialogBaseFrameSet dialogBase)
    {
        ContentOriginal = dialogBase.Data.BodyOriginal;
        ContentTranslated = dialogBase.Data.BodyTranslated;
        if (ContentTranslated.Contains("\\R"))
            ContentTranslated = ContentTranslated.Replace("\n", "")
                .Replace("\\N", "").Replace("\\R", "\n");
        else
            ContentTranslated = ContentTranslated.Replace("\\N", "\n");

        if (ContentTranslated.LineCount() == 3)
            ContentTranslated = ContentTranslated.Replace("\n", "");

        CanReturn = dialogBase.Data.BodyOriginal.LineCount() == 3;
        UseReturn = CanReturn && dialogBase.UseSeparator;
    }

    [ObservableProperty] private string _contentOriginal = string.Empty;
    [ObservableProperty] private string _contentTranslated = string.Empty;

    public bool CanReturn { get; }

    [ObservableProperty] private bool _useReturn;
}
