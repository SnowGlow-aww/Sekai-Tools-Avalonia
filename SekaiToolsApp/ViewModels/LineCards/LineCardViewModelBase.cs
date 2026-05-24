using CommunityToolkit.Mvvm.ComponentModel;
using SekaiToolsCore.Process.FrameSet;

namespace SekaiToolsApp.ViewModels.LineCards;

/// <summary>
/// 行卡片 VM 抽象基类。三类卡片在主页右侧 ItemsControl 内统一渲染，按插入顺序排列。
/// </summary>
public abstract partial class LineCardViewModelBase : ViewModelBase
{
    /// <summary>由父 VM 根据筛选开关写入；行卡片视图直接绑定 IsVisible。</summary>
    [ObservableProperty] private bool _visible = true;

    /// <summary>取出底层 frame set，便于导出阶段重新组装。</summary>
    public abstract BaseFrameSet FrameSet { get; }
}
