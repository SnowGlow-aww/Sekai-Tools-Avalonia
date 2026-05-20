using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SekaiToolsPlatform.ViewModels;

/// <summary>
/// 字典型 INPC 基类。属性值落 Dictionary，用 CallerMemberName 自动绑定属性名。
/// 来自 SekaiToolsMauiText.ViewModel.ViewModelBase，无功能改动。
/// </summary>
public class ViewModelBase : INotifyPropertyChanged
{
    private readonly Dictionary<string, object> _properties = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    protected T GetProperty<T>(T defaultValue = default!, [CallerMemberName] string? propertyName = null)
    {
        ArgumentNullException.ThrowIfNull(propertyName);
        if (_properties.TryGetValue(propertyName, out var value))
            return (T)value;

        // 读取未初始化属性时只返回默认值，不在 getter 里触发 PropertyChanged。
        // 否则 Avalonia 编译绑定在首次取值阶段会递归重入。
        return defaultValue;
    }

    protected void SetProperty<T>(T value, [CallerMemberName] string? propertyName = null)
    {
        ArgumentNullException.ThrowIfNull(propertyName);
        if (_properties.TryGetValue(propertyName, out var oldValue) &&
            EqualityComparer<T>.Default.Equals((T)oldValue, value))
            return;
        if (value is null)
            _properties.Remove(propertyName);
        else
            _properties[propertyName] = value;
        OnPropertyChanged(propertyName);
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
