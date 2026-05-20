namespace SekaiToolsMauiText.ViewModel;

public abstract class LineModel : ViewModelBase
{
    public long? SourceLineId { get; set; }
    public int? SourceLineNo { get; set; }
    public string? SourceLineType { get; set; }

    public abstract string Result { get; }
}
