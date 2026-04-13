using System;
using System.Collections.Generic;

namespace AGenerator.Models;

/// <summary>
/// Проектная документация (аналог листа "Проекты" в Excel)
/// </summary>
public class ProjectDoc
{
    public int Id { get; set; }

    public int ConstructionObjectId { get; set; }
    public ConstructionObject? ConstructionObject { get; set; }

    public string Code { get; set; } = string.Empty; // Шифр раздела (напр. РД шифр ...)

    public string Name { get; set; } = string.Empty; // Название раздела

    public string Sheets { get; set; } = string.Empty; // Номера листов (напр. л.4, 5, 7)

    public string Organization { get; set; } = string.Empty; // Проектная организация

    public string GIP { get; set; } = string.Empty; // ГИП (Главный инженер проекта)

    // Файл проекта
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Имя файла проекта — для отображения в UI
    /// </summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string FileName => string.IsNullOrEmpty(FilePath)
        ? string.Empty
        : System.IO.Path.GetFileName(FilePath);

    public List<ActProjectDoc> ActProjectDocs { get; set; } = new();
}
