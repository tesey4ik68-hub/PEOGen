using System;
using System.Collections.Generic;

namespace AGenerator.Models;

/// <summary>
/// Схема/Чертеж (аналог листа "Схемы" в Excel)
/// </summary>
public class Schema
{
    public int Id { get; set; }
    
    public int ConstructionObjectId { get; set; }
    public ConstructionObject? ConstructionObject { get; set; }
    
    public string Name { get; set; } = string.Empty;
    
    public string Number { get; set; } = string.Empty;
    
    public string Stage { get; set; } = string.Empty; // Стадия проектирования
    
    public DateTime? Date { get; set; }
    
    public string Author { get; set; } = string.Empty;
    
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Имя файла схемы — для отображения в UI
    /// </summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string FileName => string.IsNullOrEmpty(FilePath)
        ? string.Empty
        : System.IO.Path.GetFileName(FilePath);

    public List<ActSchema> ActSchemas { get; set; } = new();
}
