using System;

namespace AGenerator.Models;

/// <summary>
/// Роль организации в контексте объекта строительства
/// </summary>
public enum OrganizationRole
{
    Customer,         // Заказчик
    GenContractor,    // Генподрядчик
    Designer,         // Проектировщик
    SubContractor,    // Субподрядчик
    Other             // Иное лицо
}

/// <summary>
/// Справочник организаций — принадлежит конкретному объекту строительства
/// </summary>
public class Organization
{
    public int Id { get; set; }

    /// <summary>
    /// ID объекта строительства, которому принадлежит организация
    /// </summary>
    public int ConstructionObjectId { get; set; }

    /// <summary>
    /// Навигационное свойство на объект
    /// </summary>
    public ConstructionObject? ConstructionObject { get; set; }

    /// <summary>
    /// Роль организации (одна на запись)
    /// </summary>
    public OrganizationRole Role { get; set; }

    /// <summary>
    /// Полное наименование организации
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Краткое наименование организации (опционально)
    /// </summary>
    public string? ShortName { get; set; }

    /// <summary>
    /// Реквизиты организации в формате для Word документа
    /// </summary>
    public string Requisites { get; set; } = string.Empty;

    /// <summary>
    /// Признак активности организации.
    /// Для ролей Customer/GenContractor/Designer означает "текущая организация для шапки АОСР".
    /// Активной может быть только одна организация на роль в рамках объекта.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Дата создания записи
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// Отображаемое наименование для комбобоксов
    /// </summary>
    public string DisplayName => string.IsNullOrWhiteSpace(ShortName) ? Name : ShortName!;

    /// <summary>
    /// Полные реквизиты для документа: Наименование + Реквизиты
    /// Без лишней точки, если одна из частей пустая.
    /// </summary>
    public string FullRequisites
    {
        get
        {
            var hasName = !string.IsNullOrWhiteSpace(Name);
            var hasReq = !string.IsNullOrWhiteSpace(Requisites);

            if (hasName && hasReq)
                return $"{Name.Trim()}. {Requisites.Trim()}";
            if (hasName)
                return Name.Trim();
            if (hasReq)
                return Requisites.Trim();

            return string.Empty;
        }
    }

    public override string ToString()
    {
        return Name;
    }
}
