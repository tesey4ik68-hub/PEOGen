using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OfficeOpenXml;
using AGenerator.Models;

namespace AGenerator.Services;

/// <summary>
/// Сервис генерации Excel-реестров для актов с большим количеством документов.
/// Если количество материалов, схем или протоколов превышает лимит (5 шт.),
/// создаётся отдельный Excel-файл с реестром, а в акт вставляется ссылка на него.
/// </summary>
public interface IRegistryService
{
    /// <summary>
    /// Создаёт реестр материалов, если их больше лимита.
    /// Возвращает имя файла реестра или null, если реестр не нужен.
    /// </summary>
    string? CreateMaterialsRegistry(Act act, List<ActMaterial> actMaterials, string outputDir);

    /// <summary>
    /// Создаёт реестр приложений (схемы + протоколы), если их сумма больше лимита.
    /// </summary>
    string? CreateAttachmentsRegistry(Act act, List<ActSchema> actSchemas, List<Protocol> protocols, string outputDir);
}

public class RegistryService : IRegistryService
{
    static RegistryService()
    {
        // Установка лицензионного контекста для EPPlus (NonCommercial)
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
    }

    public string? CreateMaterialsRegistry(Act act, List<ActMaterial> actMaterials, string outputDir)
    {
        if (actMaterials == null || actMaterials.Count <= Settings.RegistryLimit)
            return null;

        var fileName = $"Реестр_Материалов_{SanitizeFileName(act.ActNumber)}_{DateTime.Now:yyyyMMddHHmmss}.xlsx";
        var filePath = Path.Combine(outputDir, fileName);

        using (var package = new ExcelPackage())
        {
            var worksheet = package.Workbook.Worksheets.Add("Реестр");

            // Заголовки
            worksheet.Cells[1, 1].Value = "№ п/п";
            worksheet.Cells[1, 2].Value = "Наименование материала";
            worksheet.Cells[1, 3].Value = "Ед. изм.";
            worksheet.Cells[1, 4].Value = "Количество";
            worksheet.Cells[1, 5].Value = "Сертификат/Паспорт №";
            worksheet.Cells[1, 6].Value = "Дата сертификата";

            // Оформление заголовков
            using (var headerRange = worksheet.Cells[1, 1, 1, 6])
            {
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                headerRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
            }

            // Данные
            for (int i = 0; i < actMaterials.Count; i++)
            {
                var actMat = actMaterials[i];
                var mat = actMat.Material;
                if (mat == null) continue;

                worksheet.Cells[i + 2, 1].Value = i + 1;
                worksheet.Cells[i + 2, 2].Value = mat.Name;
                worksheet.Cells[i + 2, 3].Value = mat.Unit;
                worksheet.Cells[i + 2, 4].Value = actMat.Quantity > 0 ? actMat.Quantity : mat.Quantity;
                worksheet.Cells[i + 2, 5].Value = mat.CertificateNumber;
                worksheet.Cells[i + 2, 6].Value = mat.CertificateDateText;
            }

            // Автоподбор ширины колонок
            worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();
            
            package.SaveAs(new FileInfo(filePath));
        }

        return fileName;
    }

    public string? CreateAttachmentsRegistry(Act act, List<ActSchema> actSchemas, List<Protocol> protocols, string outputDir)
    {
        var totalDocs = (actSchemas?.Count ?? 0) + (protocols?.Count ?? 0);
        if (totalDocs <= Settings.RegistryLimit)
            return null;

        var fileName = $"Реестр_Приложений_{SanitizeFileName(act.ActNumber)}_{DateTime.Now:yyyyMMddHHmmss}.xlsx";
        var filePath = Path.Combine(outputDir, fileName);

        using (var package = new ExcelPackage())
        {
            var worksheet = package.Workbook.Worksheets.Add("Реестр");

            // Заголовки
            worksheet.Cells[1, 1].Value = "№ п/п";
            worksheet.Cells[1, 2].Value = "Тип документа";
            worksheet.Cells[1, 3].Value = "Номер";
            worksheet.Cells[1, 4].Value = "Дата";
            worksheet.Cells[1, 5].Value = "Наименование";

            // Оформление заголовков
            using (var headerRange = worksheet.Cells[1, 1, 1, 5])
            {
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                headerRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
            }

            int row = 2;
            int index = 1;

            // Схемы
            if (actSchemas != null)
            {
                foreach (var actSchema in actSchemas)
                {
                    var schema = actSchema.Schema;
                    if (schema == null) continue;

                    worksheet.Cells[row, 1].Value = index++;
                    worksheet.Cells[row, 2].Value = "Исполнительная схема";
                    worksheet.Cells[row, 3].Value = schema.Number;
                    worksheet.Cells[row, 4].Value = schema.Date?.ToString("dd.MM.yyyy");
                    worksheet.Cells[row, 5].Value = schema.Name;
                    row++;
                }
            }

            // Протоколы
            if (protocols != null)
            {
                foreach (var protocol in protocols)
                {
                    worksheet.Cells[row, 1].Value = index++;
                    worksheet.Cells[row, 2].Value = "Протокол испытаний";
                    worksheet.Cells[row, 3].Value = protocol.Number;
                    worksheet.Cells[row, 4].Value = protocol.Date.ToString("dd.MM.yyyy");
                    worksheet.Cells[row, 5].Value = protocol.Laboratory;
                    row++;
                }
            }

            // Автоподбор ширины колонок
            worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

            package.SaveAs(new FileInfo(filePath));
        }

        return fileName;
    }

    /// <summary>
    /// Очистить имя файла от недопустимых символов.
    /// </summary>
    private static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return "бн";

        foreach (var c in Path.GetInvalidFileNameChars())
            fileName = fileName.Replace(c, '_');
        return fileName.Trim('_', ' ');
    }
}
