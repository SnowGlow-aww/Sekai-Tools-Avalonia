namespace SekaiToolsPlatform.ViewModels;

/// <summary>
/// 翻译工作台中一行 (对话 / 旁白 / 标记) 的抽象基类。
/// SourceLine 系列字段由平台模式注入；TranslationLineMetadata 保存当前准备上传的元数据，本地模式留空。
/// </summary>
public abstract class LineModel : ViewModelBase
{
    public long? SourceLineId { get; set; }
    public int? SourceLineNo { get; set; }
    public string? SourceLineType { get; set; }
    public string? SourceLineMetadata { get; set; }
    public string? TranslationLineMetadata { get; set; }
    public bool ShowReferenceDiff
    {
        get => GetProperty(false);
        set
        {
            SetProperty(value);
            OnShowReferenceDiffChanged();
        }
    }

    protected virtual void OnShowReferenceDiffChanged()
    {
    }

    public abstract string Result { get; }
}
