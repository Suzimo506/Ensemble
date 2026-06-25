namespace MDEN.UI.Core
{
    /// <summary>
    /// Replaces PopupLib.UI.Windows.Interfaces.IListWindow.
    /// Marker interface used by <see cref="NativeListWindow.OnSelectionChanged"/> event signature.
    /// </summary>
    public interface INativeListWindow { }

    /// <summary>
    /// Replaces PopupLib.UI.Windows.Abstract.BaseWindow.
    /// Marker interface used by <see cref="NativeListWindow.OnInternalShow"/> and
    /// <see cref="NativeListWindow.OnCompletion"/> event signatures.
    /// </summary>
    public interface INativeBaseWindow { }
}
