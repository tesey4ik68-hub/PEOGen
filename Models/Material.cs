using System;
using System.Collections.Generic;

namespace AGenerator.Models;

/// <summary>
/// Тип документа материала
/// </summary>
public enum MaterialDocType
{
    DeclarationOfConformity,      // Декларация о соответствии
    QualityDocument,              // Документ о качестве
    RefusalLetter,                // Отказное письмо
    Passport,                     // Паспорт
    QualityPassport,              // Паспорт качества
    SanitaryEpidemiologicalConclusion, // Санитарно-эпидемиологическое заключение
    Certificate,                  // Свидетельство
    StateRegistrationCertificate, // Свидетельство о гос.регистрации
    CertificateOfConformity,      // Сертификат соответствия
    TechnicalPassport             // Технический паспорт
}

/// <summary>
/// Материал (аналог листа "Материалы" в Excel)
/// </summary>
public class Material
{
    public int Id { get; set; }

    public int ConstructionObjectId { get; set; }
    public ConstructionObject? ConstructionObject { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string Unit { get; set; } = string.Empty; // Единица измерения

    public decimal Quantity { get; set; }

    public string GostNumber { get; set; } = string.Empty; // Номер ГОСТ

    // Тип документа
    public MaterialDocType DocumentType { get; set; }

    public string CertificateNumber { get; set; } = string.Empty; // Номер документа

    public string CertificateDateText { get; set; } = string.Empty; // Дата документа (текст: "дд.мм.гггг", "мм.гггг", "гггг")

    public string Manufacturer { get; set; } = string.Empty;

    public string Supplier { get; set; } = string.Empty;

    public DateTime? DeliveryDate { get; set; }

    // Файл сертификата
    public string CertificateFilePath { get; set; } = string.Empty;

    /// <summary>
    /// Имя файла сертификата — для отображения в UI
    /// </summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string CertificateFileName => string.IsNullOrEmpty(CertificateFilePath)
        ? string.Empty
        : System.IO.Path.GetFileName(CertificateFilePath);

    /// <summary>
    /// Отображаемое имя типа документа
    /// </summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string DocumentTypeDisplay => DocumentType switch
    {
        MaterialDocType.DeclarationOfConformity => "Декларация о соответствии",
        MaterialDocType.QualityDocument => "Документ о качестве",
        MaterialDocType.RefusalLetter => "Отказное письмо",
        MaterialDocType.Passport => "Паспорт",
        MaterialDocType.QualityPassport => "Паспорт качества",
        MaterialDocType.SanitaryEpidemiologicalConclusion => "Сан.-эпид. заключение",
        MaterialDocType.Certificate => "Свидетельство",
        MaterialDocType.StateRegistrationCertificate => "Свидетельство о гос.регистрации",
        MaterialDocType.CertificateOfConformity => "Сертификат соответствия",
        MaterialDocType.TechnicalPassport => "Технический паспорт",
        _ => DocumentType.ToString()
    };

    public List<ActMaterial> ActMaterials { get; set; } = new();
}
