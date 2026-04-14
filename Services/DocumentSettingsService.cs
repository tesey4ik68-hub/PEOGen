using System;
using System.IO;
using System.Text.Json;
using AGenerator.Models;

namespace AGenerator.Services;

/// <summary>
/// Сервис сохранения и загрузки настроек документов (порядок приложений и т.д.)
/// Настройки хранятся в файле document_settings.json рядом с theme.json
/// </summary>
public class DocumentSettingsService
{
    private readonly string _settingsFilePath;
    private readonly string _maskSettingsFilePath;

    public DocumentSettingsService()
    {
        var appDir = AppDomain.CurrentDomain.BaseDirectory;
        _settingsFilePath = Path.Combine(appDir, "document_settings.json");
        _maskSettingsFilePath = Path.Combine(appDir, "act_number_mask_settings.json");
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
            var settings = JsonSerializer.Deserialize<DocumentSettings>(json);
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
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
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

    /// <summary>
    /// Загрузить настройки маски номера акта из файла.
    /// Если файл отсутствует или повреждён — возвращает настройки по умолчанию.
    /// </summary>
    public ActNumberMaskSettings LoadActNumberMaskSettings()
    {
        if (!File.Exists(_maskSettingsFilePath))
            return ActNumberMaskSettings.CreateDefault();

        try
        {
            var json = File.ReadAllText(_maskSettingsFilePath);
            var settings = JsonSerializer.Deserialize<ActNumberMaskSettings>(json);
            return settings ?? ActNumberMaskSettings.CreateDefault();
        }
        catch
        {
            return ActNumberMaskSettings.CreateDefault();
        }
    }

    /// <summary>
    /// Сохранить настройки маски номера акта в файл.
    /// </summary>
    public void SaveActNumberMaskSettings(ActNumberMaskSettings settings)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(_maskSettingsFilePath, json);
        }
        catch
        {
            // Игнорируем ошибки сохранения
        }
    }
}
