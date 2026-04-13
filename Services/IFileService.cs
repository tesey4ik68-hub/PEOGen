using System;
using System.IO;

namespace AGenerator.Services;

public interface IFileService
{
    /// <summary>
    /// Получить путь к папке объекта
    /// </summary>
    string GetObjectFolderPath(int objectId, string objectName);
    
    /// <summary>
    /// Получить путь к папке актов определенного типа
    /// </summary>
    string GetActsFolderPath(int objectId, string objectName, string actType);
    
    /// <summary>
    /// Получить путь к папке схем
    /// </summary>
    string GetSchemasFolderPath(int objectId, string objectName);
    
    /// <summary>
    /// Получить путь к папке протоколов
    /// </summary>
    string GetProtocolsFolderPath(int objectId, string objectName);
    
    /// <summary>
    /// Получить путь к папке материалов
    /// </summary>
    string GetMaterialsFolderPath(int objectId, string objectName);
    
    /// <summary>
    /// Убедиться, что все папки для объекта существуют
    /// </summary>
    void EnsureFoldersExist(int objectId, string objectName);
    
    /// <summary>
    /// Получить путь к базовой папке вывода
    /// </summary>
    string GetBaseOutputPath();
    
    /// <summary>
    /// Получить путь к шаблону Word
    /// </summary>
    string GetTemplatePath(string templateName);

    // ==================== МЕТОДЫ СОХРАНЕНИЯ ФАЙЛОВ ====================

    /// <summary>
    /// Сохранить файл приказа: [Объект]\Приказы\Приказ на [ФИО в род. падеже] №[номер или б/н][ от дата].[ext]
    /// </summary>
    string SaveOrderFile(int objectId, string objectName, Stream fileStream, string employeeFullName, string orderNumber, DateTime? orderDate, string originalFileName);

    /// <summary>
    /// Сохранить файл сертификата материала: [Объект]\Материалы\[Тип]\[наименование] ([тип док] №[номер][ от дата]).[ext]
    /// </summary>
    string SaveMaterialCert(int objectId, string objectName, string materialType, Stream fileStream,
        string materialName, string documentTypeDisplay, string docNumber, string docDateText, string originalFileName);

    /// <summary>
    /// Сохранить файл схемы: [Объект]\Схемы\[название] №[номер] от [дата].[ext] или просто [название]
    /// </summary>
    string SaveSchemaFile(int objectId, string objectName, Stream fileStream,
        string schemaName, string schemaNumber, DateTime? schemaDate, string originalFileName);

    /// <summary>
    /// Сохранить файл протокола: [Объект]\Протоколы и заключения\[тип] №[номер] от [дата].[ext]
    /// </summary>
    string SaveProtocolFile(int objectId, string objectName, Stream fileStream,
        string documentTypeDisplay, string protocolNumber, DateTime? protocolDate, string originalFileName);

    /// <summary>
    /// Сохранить файл проекта: [Объект]\Проекты\[название раздела] [шифр].[ext]
    /// </summary>
    string SaveProjectFile(int objectId, string objectName, Stream fileStream,
        string projectName, string projectCode, string originalFileName);

    /// <summary>
    /// Открыть файл в ассоциированной программе
    /// </summary>
    void OpenFile(string filePath);

    /// <summary>
    /// Проверить существование файла
    /// </summary>
    bool FileExists(string filePath);

    /// <summary>
    /// Получить только имя файла из полного пути
    /// </summary>
    string GetFileName(string filePath);
}
