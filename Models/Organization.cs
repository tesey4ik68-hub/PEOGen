using System;

namespace AGenerator.Models;

/// <summary>
/// Справочник организаций
/// </summary>
public class Organization
{
    public int Id { get; set; }

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
    /// Признак активности организации
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
}
