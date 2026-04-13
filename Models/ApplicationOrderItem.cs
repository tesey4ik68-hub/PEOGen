using CommunityToolkit.Mvvm.ComponentModel;

namespace AGenerator.Models;

/// <summary>
/// Элемент порядка приложений в акте.
/// Каждый элемент представляет тип приложения (материалы, схемы, протоколы и т.д.)
/// и его позицию в итоговом списке приложений.
/// </summary>
public partial class ApplicationOrderItem : ObservableObject
{
    /// <summary>
    /// Уникальный ключ, используемый в коде (напр. "Materials", "Schemas")
    /// </summary>
    public string Key { get; set; } = "";

    /// <summary>
    /// Отображаемое имя в UI
    /// </summary>
    public string DisplayName { get; set; } = "";

    [ObservableProperty]
    private bool _isEnabled;

    [ObservableProperty]
    private int _order;

    public ApplicationOrderItem() { }

    public ApplicationOrderItem(string key, string displayName, bool isEnabled, int order)
    {
        Key = key;
        DisplayName = displayName;
        IsEnabled = isEnabled;
        Order = order;
    }
}
