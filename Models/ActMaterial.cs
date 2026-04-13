namespace AGenerator.Models;

/// <summary>
/// Промежуточная таблица для связи Act и Material
/// </summary>
public class ActMaterial
{
    public int Id { get; set; }
    
    public int ActId { get; set; }
    public Act? Act { get; set; }
    
    public int MaterialId { get; set; }
    public Material? Material { get; set; }
    
    public decimal Quantity { get; set; }
    
    public string? Note { get; set; }
}
