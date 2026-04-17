using System;

namespace AGenerator.Models;

/// <summary>
/// Тип документа протокола/заключения
/// </summary>
public enum ProtocolDocType
{
    ConcreteDensity,      // Акт испытания образцов бетона
    CompactionDegree     // Заключение о степени уплотнения
}

/// <summary>
/// Протокол испытаний (аналог листа "Протоколы" в Excel)
/// </summary>
public class Protocol
{
    public int Id { get; set; }

    public int ConstructionObjectId { get; set; }
    public ConstructionObject? ConstructionObject { get; set; }

    public int? ActId { get; set; }
    public Act? Act { get; set; }

    public string Number { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty; // Наименование испытания

    // Тип документа
    public ProtocolDocType DocumentType { get; set; }

    public DateTime Date { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string DateText
    {
        get => Date.ToString("dd.MM.yyyy");
        set { /* Только для биндинга */ }
    }

    public string Type { get; set; } = string.Empty; // Тип протокола/испытаний

    public string Laboratory { get; set; } = string.Empty; // Лаборатория

    public string Result { get; set; } = string.Empty; // Результат испытаний

    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Имя файла протокола — для отображения в UI
    /// </summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string FileName => string.IsNullOrEmpty(FilePath)
        ? string.Empty
        : System.IO.Path.GetFileName(FilePath);

    /// <summary>
    /// Отображаемое имя типа документа
    /// </summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string DocumentTypeDisplay => DocumentType switch
    {
        ProtocolDocType.ConcreteDensity => "Акт испытания образцов бетона",
        ProtocolDocType.CompactionDegree => "Заключение о степени уплотнения",
        _ => DocumentType.ToString()
    };

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
