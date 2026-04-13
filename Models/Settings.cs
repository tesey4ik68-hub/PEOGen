namespace AGenerator.Models;

/// <summary>
/// Глобальные настройки генерации документов.
/// </summary>
public static class Settings
{
    /// <summary>
    /// Лимит документов (материалы, схемы, протоколы), 
    /// после которого создаётся Excel-реестр вместо текстового перечисления.
    /// По умолчанию: 5 (из VBA-логики).
    /// </summary>
    public const int RegistryLimit = 5;
}
