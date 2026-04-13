namespace AGenerator.Models;

/// <summary>
/// Промежуточная таблица для связи Act и Schema
/// </summary>
public class ActSchema
{
    public int Id { get; set; }
    
    public int ActId { get; set; }
    public Act? Act { get; set; }
    
    public int SchemaId { get; set; }
    public Schema? Schema { get; set; }
    
    public string? Note { get; set; }
}
