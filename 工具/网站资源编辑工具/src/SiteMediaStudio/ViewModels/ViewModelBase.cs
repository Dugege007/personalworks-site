using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace PersonalWorks.SiteMediaStudio;

/// <summary>
/// 可绑定对象的公共实现。
/// </summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// 若值变化则写入并通知绑定。
    /// </summary>
    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }

    /// <summary>
    /// 通知指定属性已变化。
    /// </summary>
    protected void Raise(string name)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

/// <summary>
/// 无参数命令。
/// </summary>
public sealed class RelayCommand : ICommand
{
    private readonly Action mExecute;
    private readonly Func<bool>? mCanExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        mExecute = execute;
        mCanExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    /// <summary>
    /// 当前是否可执行。
    /// </summary>
    public bool CanExecute(object? parameter)
    {
        return mCanExecute == null || mCanExecute();
    }

    /// <summary>
    /// 执行命令体。
    /// </summary>
    public void Execute(object? parameter)
    {
        mExecute();
    }

    /// <summary>
    /// 通知绑定按钮刷新可执行状态。
    /// </summary>
    public void RaiseCanExecute()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
