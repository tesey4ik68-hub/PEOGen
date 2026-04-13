using System;
using System.Collections.Generic;

namespace AGenerator.Models;

/// <summary>
/// Объект строительства (аналог листа "Объекты" в Excel)
/// </summary>
public class ConstructionObject
{
    public int Id { get; set; }
    
    public string Name { get; set; } = string.Empty;
    
    public string Address { get; set; } = string.Empty;
    
    public string Customer { get; set; } = string.Empty;
    
    public string Contractor { get; set; } = string.Empty;
    
    public string ProjectCode { get; set; } = string.Empty;

    // Реквизиты организаций
    public string CustomerRequisites { get; set; } = string.Empty;
    public string ContractorRequisites { get; set; } = string.Empty;
    public string DesignerRequisites { get; set; } = string.Empty;

    public List<Act> Acts { get; set; } = new();
    
    public List<Employee> Employees { get; set; } = new();
    
    public List<Material> Materials { get; set; } = new();
    
    public List<Schema> Schemas { get; set; } = new();
    
    public List<Protocol> Protocols { get; set; } = new();

    public List<ProjectDoc> ProjectDocs { get; set; } = new();
}
