using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AGenerator.Services;

public class FileService : IFileService
{
    private readonly string _baseOutputPath;
    private readonly string _templatesPath;

    public FileService()
    {
        // Базовая папка вывода: [корень программы]\Документы
        _baseOutputPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Документы");

        // Папка шаблонов - в директории приложения
        _templatesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates");

        // Создаем базовые папки при инициализации
        Directory.CreateDirectory(_baseOutputPath);
        Directory.CreateDirectory(_templatesPath);
    }

    public string GetBaseOutputPath()
    {
        return _baseOutputPath;
    }

    public string GetObjectFolderPath(int objectId, string objectName)
    {
        // Используем имя объекта для читаемости, но добавляем ID для уникальности
        var safeName = SanitizeFileName(objectName);
        return Path.Combine(_baseOutputPath, $"{objectId}_{safeName}");
    }

    public string GetActsFolderPath(int objectId, string objectName, string actType)
    {
        var objPath = GetObjectFolderPath(objectId, objectName);
        var path = Path.Combine(objPath, "Акты", actType);
        Directory.CreateDirectory(path);
        return path;
    }

    public string GetSchemasFolderPath(int objectId, string objectName)
    {
        var objPath = GetObjectFolderPath(objectId, objectName);
        var path = Path.Combine(objPath, "Схемы");
        Directory.CreateDirectory(path);
        return path;
    }

    public string GetProtocolsFolderPath(int objectId, string objectName)
    {
        var objPath = GetObjectFolderPath(objectId, objectName);
        var path = Path.Combine(objPath, "Протоколы и заключения");
        Directory.CreateDirectory(path);
        return path;
    }

    public string GetMaterialsFolderPath(int objectId, string objectName)
    {
        var objPath = GetObjectFolderPath(objectId, objectName);
        var path = Path.Combine(objPath, "Материалы");
        Directory.CreateDirectory(path);
        return path;
    }

    public void EnsureFoldersExist(int objectId, string objectName)
    {
        try
        {
            var objPath = GetObjectFolderPath(objectId, objectName);
            Directory.CreateDirectory(objPath);
            Directory.CreateDirectory(Path.Combine(objPath, "Акты"));
            Directory.CreateDirectory(Path.Combine(objPath, "Схемы"));
            Directory.CreateDirectory(Path.Combine(objPath, "Протоколы и заключения"));
            Directory.CreateDirectory(Path.Combine(objPath, "Материалы"));
            Directory.CreateDirectory(Path.Combine(objPath, "Приказы"));
            Directory.CreateDirectory(Path.Combine(objPath, "Проекты"));

            // Создаем подпапки для разных типов актов
            var actsPath = Path.Combine(objPath, "Акты");
            foreach (var actType in GetDefaultActTypes())
            {
                Directory.CreateDirectory(Path.Combine(actsPath, actType));
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WARNING] Ошибка создания папок: {ex.Message}");
            // Не бросаем исключение дальше, чтобы не крашить приложение при старте
        }
    }

    public string GetTemplatePath(string templateName)
    {
        var templatePath = Path.Combine(_templatesPath, templateName);
        
        if (!File.Exists(templatePath))
        {
            throw new FileNotFoundException($"Шаблон '{templateName}' не найден в папке {_templatesPath}");
        }
        
        return templatePath;
    }

    /// <summary>
    /// Очистка имени файла от недопустимых символов
    /// </summary>
    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName
            .Where(c => !invalidChars.Contains(c))
            .ToArray());
        
        // Заменяем пробелы на подчеркивания для удобства
        return sanitized.Replace(' ', '_');
    }

    /// <summary>
    /// Список типов актов по умолчанию
    /// </summary>
    private static IEnumerable<string> GetDefaultActTypes()
    {
        yield return "АОСР";
        yield return "АООК";
        yield return "АОИО";
        yield return "АВК";
        yield return "АГИ";
        yield return "АИИО";
        yield return "АОГРОКС";
        yield return "АОСР3";
        yield return "АОУСИТО";
        yield return "АПрОб";
        yield return "АРООКС";
        yield return "ОтЭфД";
    }

    // ==================== МЕТОДЫ СОХРАНЕНИЯ ФАЙЛОВ ====================

    public string SaveOrderFile(int objectId, string objectName, Stream fileStream, string employeeFullName, string orderNumber, DateTime? orderDate, string originalFileName)
    {
        var objPath = GetObjectFolderPath(objectId, objectName);
        var dir = Path.Combine(objPath, "Приказы");
        Directory.CreateDirectory(dir);

        // Формируем имя: Приказ на [ФИО в род. падеже] №[номер или б/н][ от дата].[ext]
        var ext = Path.GetExtension(originalFileName);
        var genitiveName = RussianNameDeclension.ToGenitiveCase(employeeFullName);
        var fileName = $"Приказ на {SanitizeFileName(genitiveName)}";

        // Номер приказа: если пустой — "б/н"
        var numPart = string.IsNullOrWhiteSpace(orderNumber) ? "б/н" : SanitizeFileName(orderNumber);
        fileName += $" №{numPart}";

        // Дата приказа: только если есть
        if (orderDate.HasValue)
            fileName += $" от {orderDate.Value:dd.MM.yyyy}";

        fileName += ext;

        var filePath = Path.Combine(dir, SanitizeFileName(fileName));
        filePath = GetUniqueFilePath(filePath);

        using var fs = new FileStream(filePath, FileMode.Create);
        fileStream.CopyTo(fs);
        return filePath;
    }

    public string SaveMaterialCert(int objectId, string objectName, string materialType, Stream fileStream,
        string materialName, string documentTypeDisplay, string docNumber, string docDateText, string originalFileName)
    {
        var objPath = GetObjectFolderPath(objectId, objectName);
        var dir = Path.Combine(objPath, "Материалы", SanitizeFileName(materialType));
        Directory.CreateDirectory(dir);

        // Формируем имя: [наименование] ([тип док] №[номер или б/н][ от дата]).[ext]
        var ext = Path.GetExtension(originalFileName);
        var fileName = SanitizeFileName(materialName);

        // Тип документа в скобках
        var docPart = SanitizeFileName(documentTypeDisplay);
        var numPart = string.IsNullOrWhiteSpace(docNumber) ? "б/н" : SanitizeFileName(docNumber);

        fileName += $" ({docPart} №{numPart}";
        if (!string.IsNullOrWhiteSpace(docDateText))
            fileName += $" от {SanitizeFileName(docDateText)}";
        fileName += ")";

        fileName += ext;

        var filePath = Path.Combine(dir, SanitizeFileName(fileName));
        filePath = GetUniqueFilePath(filePath);

        using var fs = new FileStream(filePath, FileMode.Create);
        fileStream.CopyTo(fs);
        return filePath;
    }

    public string SaveSchemaFile(int objectId, string objectName, Stream fileStream,
        string schemaName, string schemaNumber, DateTime? schemaDate, string originalFileName)
    {
        var objPath = GetObjectFolderPath(objectId, objectName);
        var dir = Path.Combine(objPath, "Схемы");
        Directory.CreateDirectory(dir);

        // Формируем имя: [название] №[номер] от [дата].[ext] или просто [название]
        var ext = Path.GetExtension(originalFileName);
        var fileName = SanitizeFileName(schemaName);
        if (!string.IsNullOrEmpty(schemaNumber))
            fileName += $" №{SanitizeFileName(schemaNumber)}";
        if (schemaDate.HasValue)
            fileName += $" от {schemaDate.Value:dd.MM.yyyy}";
        fileName += ext;

        var filePath = Path.Combine(dir, SanitizeFileName(fileName));
        filePath = GetUniqueFilePath(filePath);

        using var fs = new FileStream(filePath, FileMode.Create);
        fileStream.CopyTo(fs);
        return filePath;
    }

    public string SaveProtocolFile(int objectId, string objectName, Stream fileStream,
        string documentTypeDisplay, string protocolNumber, DateTime? protocolDate, string originalFileName)
    {
        var objPath = GetObjectFolderPath(objectId, objectName);
        var dir = Path.Combine(objPath, "Протоколы и заключения");
        Directory.CreateDirectory(dir);

        // Формируем имя: [тип] №[номер] от [дата].[ext]
        var ext = Path.GetExtension(originalFileName);
        var fileName = SanitizeFileName(documentTypeDisplay);
        if (!string.IsNullOrEmpty(protocolNumber))
            fileName += $" №{SanitizeFileName(protocolNumber)}";
        if (protocolDate.HasValue)
            fileName += $" от {protocolDate.Value:dd.MM.yyyy}";
        fileName += ext;

        var filePath = Path.Combine(dir, SanitizeFileName(fileName));
        filePath = GetUniqueFilePath(filePath);

        using var fs = new FileStream(filePath, FileMode.Create);
        fileStream.CopyTo(fs);
        return filePath;
    }

    public string SaveProjectFile(int objectId, string objectName, Stream fileStream,
        string projectName, string projectCode, string originalFileName)
    {
        var objPath = GetObjectFolderPath(objectId, objectName);
        var dir = Path.Combine(objPath, "Проекты");
        Directory.CreateDirectory(dir);

        // Формируем имя: [название раздела] [шифр].[ext]
        var ext = Path.GetExtension(originalFileName);
        var fileName = SanitizeFileName(projectName);
        if (!string.IsNullOrEmpty(projectCode))
            fileName += $" {SanitizeFileName(projectCode)}";
        fileName += ext;

        var filePath = Path.Combine(dir, SanitizeFileName(fileName));
        filePath = GetUniqueFilePath(filePath);

        using var fs = new FileStream(filePath, FileMode.Create);
        fileStream.CopyTo(fs);
        return filePath;
    }

    public void OpenFile(string filePath)
    {
        if (!FileExists(filePath))
            throw new FileNotFoundException($"Файл не найден: {filePath}");

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = filePath,
            UseShellExecute = true
        });
    }

    public bool FileExists(string filePath)
    {
        return !string.IsNullOrEmpty(filePath) && File.Exists(filePath);
    }

    public string GetFileName(string filePath)
    {
        return string.IsNullOrEmpty(filePath) ? string.Empty : Path.GetFileName(filePath);
    }

    /// <summary>
    /// Создать уникальное имя файла, добавляя (1), (2) и т.д. если файл существует
    /// </summary>
    private string GetUniqueFilePath(string filePath)
    {
        if (!File.Exists(filePath))
            return filePath;

        var dir = Path.GetDirectoryName(filePath);
        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(filePath);
        var ext = Path.GetExtension(filePath);
        int counter = 1;

        string newFilePath;
        do
        {
            newFilePath = Path.Combine(dir!, $"{fileNameWithoutExt} ({counter}){ext}");
            counter++;
        } while (File.Exists(newFilePath));

        return newFilePath;
    }
}
