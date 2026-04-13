namespace AGenerator.Models;

/// <summary>
/// Промежуточная таблица для связи Act и ProjectDoc
/// </summary>
public class ActProjectDoc
{
    public int Id { get; set; }

    public int ActId { get; set; }
    public Act? Act { get; set; }

    public int ProjectDocId { get; set; }
    public ProjectDoc? ProjectDoc { get; set; }

    public string? Note { get; set; }
}
