using System;
using System.IO;
using AGenerator.Models;

namespace AGenerator.Services;

/// <summary>
/// Сервис сохранения и загрузки настроек документов (порядок приложений и т.д.)
/// Настройки хранятся в файле document_settings.json рядом с theme.json
/// </summary>
public class DocumentSettingsService
{
    private readonly string _settingsFilePath;

    public DocumentSettingsService()
    {
        var appDir = AppDomain.CurrentDomain.BaseDirectory;
        _settingsFilePath = Path.Combine(appDir, "document_settings.json");
    }

    /// <summary>
    /// Загрузить настройки документов из файла.
    /// Если файл отсутствует или повреждён — возвращает настройки по умолчанию.
    /// </summary>
    public DocumentSettings LoadSettings()
    {
        if (!File.Exists(_settingsFilePath))
            return new DocumentSettings();

        try
        {
            var json = File.ReadAllText(_settingsFilePath);
            var settings = System.Text.Json.JsonSerializer.Deserialize<DocumentSettings>(json);
            return settings ?? new DocumentSettings();
        }
        catch
        {
            // При повреждённом файле — fallback на дефолт
            return new DocumentSettings();
        }
    }

    /// <summary>
    /// Сохранить настройки документов в файл.
    /// </summary>
    public void SaveSettings(DocumentSettings settings)
    {
        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(settings, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(_settingsFilePath, json);
        }
        catch
        {
            // Игнорируем ошибки сохранения
        }
    }
}
