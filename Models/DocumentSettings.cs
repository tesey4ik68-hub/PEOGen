using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AGenerator.Models;

/// <summary>
/// Модель настроек для генерации актов и реестров
/// </summary>
public partial class DocumentSettings : ObservableObject
{
    /// <summary>
    /// Порядок приложений в акте.
    /// Определяет порядок следования разделов в поле "Приложения".
    /// </summary>
    public List<ApplicationOrderItem> ApplicationOrder { get; set; } = CreateDefaultOrder();

    /// <summary>
    /// Создаёт порядок по умолчанию, соответствующий текущему поведению системы:
    /// 1. Исполнительные схемы
    /// 2. Протоколы испытаний
    /// 3. Материалы (сертификаты)
    /// 4. Ручные приложения
    /// </summary>
    public static List<ApplicationOrderItem> CreateDefaultOrder()
    {
        return new List<ApplicationOrderItem>
        {
            new ApplicationOrderItem("Schemas", "Исполнительные схемы", true, 0),
            new ApplicationOrderItem("Protocols", "Протоколы испытаний", true, 1),
            new ApplicationOrderItem("Materials", "Материалы (сертификаты)", true, 2),
            new ApplicationOrderItem("Manual", "Ручные приложения", true, 3),
        };
    }
}
