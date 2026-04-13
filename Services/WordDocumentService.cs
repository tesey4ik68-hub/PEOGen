using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using AGenerator.Models;

namespace AGenerator.Services;

/// <summary>
/// Сервис генерации Word-документов актов на основе шаблонов .docx.
/// Использует DocumentFormat.OpenXml — не требует установленного Word.
/// </summary>
public class WordDocumentService
{
    private readonly string _baseOutputPath;
    private readonly string _templatesPath;
    private readonly IRegistryService _registryService;

    public WordDocumentService(string templatesPath, string baseOutputPath, IRegistryService registryService)
    {
        _templatesPath = templatesPath;
        _baseOutputPath = baseOutputPath;
        _registryService = registryService;

        // Гарантируем существование базовой папки Output
        Directory.CreateDirectory(Path.Combine(_baseOutputPath, "Output"));
    }

    /// <summary>
    /// Сгенерировать документ акта из шаблона.
    /// </summary>
    /// <param name="act">Акт для генерации</param>
    /// <returns>Путь к созданному файлу</returns>
    public async Task<string> GenerateActAsync(Act act)
    {
        var templateName = GetTemplateName(act.Type);
        var templatePath = Path.Combine(_templatesPath, templateName);

        if (!File.Exists(templatePath))
            throw new FileNotFoundException($"Шаблон не найден: {templatePath}");

        // Формируем путь: [BasePath]\Output\[Имя_Объекта]\Акты\[Тип]\[YYYY-MM-DD_HH-mm-ss]\
        var objectFolderName = SanitizeFileName(act.ConstructionObject?.Name ?? $"Объект_{act.ConstructionObjectId}");
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");

        var outputDir = Path.Combine(_baseOutputPath, "Output", objectFolderName, "Акты", act.Type, timestamp);
        Directory.CreateDirectory(outputDir);

        var safeActNumber = string.IsNullOrWhiteSpace(act.ActNumber) ? "бн" : SanitizeFileName(act.ActNumber);
        var safeWorkName = SanitizeFileName(act.WorkName);
        if (safeWorkName.Length > 50)
            safeWorkName = safeWorkName[..50];

        var fileName = $"{act.Type}_{safeActNumber}_{safeWorkName}.docx";
        var outputPath = Path.Combine(outputDir, fileName);

        await Task.Run(() =>
        {
            File.Copy(templatePath, outputPath, true);

            // Формируем данные для шаблона.
            // Реестры создаются внутри FormatAppendix с учётом настроек IsEnabled.
            FillWordTemplate(outputPath, act, outputDir);
        });

        return outputPath;
    }

    /// <summary>
    /// Заполнить Word-документ данными из акта.
    /// </summary>
    private void FillWordTemplate(string filePath, Act act, string outputDir)
    {
        using (var doc = WordprocessingDocument.Open(filePath, true))
        {
            var body = doc.MainDocumentPart?.Document?.Body;
            if (body == null)
                return;

            // Собираем закладки из Body (с компарером без учёта регистра)
            var bookmarks = body.Descendants<BookmarkStart>()
                .Where(b => b.Name != null)
                .ToDictionary(b => b.Name!.Value, b => b, StringComparer.OrdinalIgnoreCase);

            // Добавляем закладки из колонтитулов (header/footer)
            foreach (var headerPart in doc.MainDocumentPart.HeaderParts)
            {
                var headerBookmarks = headerPart.RootElement.Descendants<BookmarkStart>()
                    .Where(b => b.Name != null);
                foreach (var bm in headerBookmarks)
                {
                    if (!bookmarks.ContainsKey(bm.Name!.Value))
                        bookmarks[bm.Name!.Value] = bm;
                }
            }

            foreach (var footerPart in doc.MainDocumentPart.FooterParts)
            {
                var footerBookmarks = footerPart.RootElement.Descendants<BookmarkStart>()
                    .Where(b => b.Name != null);
                foreach (var bm in footerBookmarks)
                {
                    if (!bookmarks.ContainsKey(bm.Name!.Value))
                        bookmarks[bm.Name!.Value] = bm;
                }
            }

            // Отладка: выводим все найденные закладки
            System.Diagnostics.Debug.WriteLine($"[WordDocumentService] Найдено закладок: {bookmarks.Count}");
            foreach (var bk in bookmarks.Keys.OrderBy(k => k))
            {
                System.Diagnostics.Debug.WriteLine($"  - {bk}");
            }

            var data = GetActDataDictionary(act, outputDir);

            foreach (var kvp in data)
            {
                if (bookmarks.TryGetValue(kvp.Key, out var bookmark))
                {
                    ReplaceBookmarkText(bookmark, kvp.Value);
                    System.Diagnostics.Debug.WriteLine($"[WordDocumentService] Заменено: {kvp.Key} → \"{kvp.Value}\"");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[WordDocumentService] НЕ НАЙДЕНА закладка: {kvp.Key}");
                }
            }

            // Специфическая очистка для АООК/АРООКС
            if (act.Type is "АООК" or "АРООКС")
            {
                RemoveEmptyTablesForAOOK(body, act);
            }

            // Специфическая очистка для АОУСИТО
            if (act.Type == "АОУСИТО")
            {
                RemoveEmptyBlocksForAOUSITO(body, act);
            }

            doc.MainDocumentPart!.Document.Save();
        }
    }

    /// <summary>
    /// Сформировать словарь данных для замены закладок.
    /// Ключи должны точно соответствовать именам закладок в шаблоне .docx.
    /// </summary>
    private Dictionary<string, string> GetActDataDictionary(Act act, string outputDir)
    {
        var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // ==================== ОБЪЕКТ И РЕКВИЗИТЫ ====================
        var obj = act.ConstructionObject;
        if (obj != null)
        {
            data["Наименование_объекта"] = obj.Name;
            data["реквизиты_заказчика"] = obj.CustomerRequisites;
            data["реквизиты_генподрядчика"] = obj.ContractorRequisites;
            data["реквизиты_проектировщика"] = obj.DesignerRequisites;
        }
        else
        {
            data["Наименование_объекта"] = "";
            data["реквизиты_заказчика"] = "";
            data["реквизиты_генподрядчика"] = "";
            data["реквизиты_проектировщика"] = "";
        }

        // ==================== НОМЕР И ДАТА АКТА ====================
        data["Номер_акта"] = act.ActNumber;

        var actDate = act.ActDate;
        data["день_а"] = actDate.Day.ToString("D2");
        data["месяц_а"] = GetMonthName(actDate.Month);
        data["год_а"] = actDate.Year.ToString();

        // ==================== ПОДПИСАНТЫ (ФИО и Должности) ====================

        // СК Заказчика (ТНЗ)
        data["ФИО_ТНЗ"] = act.CustomerRep?.FullName ?? "";
        data["Должность_ТНЗ"] = act.CustomerRep?.Position ?? "";

        // Генподрядчик (Г)
        data["ФИО_Г"] = act.GenContractorRep?.FullName ?? "";
        data["Должность_Г"] = act.GenContractorRep?.Position ?? "";

        // ТК Генподрядчика (ТНГ)
        data["ФИО_ТНГ"] = act.GenContractorSkRep?.FullName ?? "";
        data["Должность_ТНГ"] = act.GenContractorSkRep?.Position ?? "";

        // Проектировщик (Пр)
        data["ФИО_Пр"] = act.DesignerRep?.FullName ?? "";
        data["Должность_Пр"] = act.DesignerRep?.Position ?? "";

        // Подрядчик (Пд)
        data["ФИО_Пд"] = act.ContractorRep?.FullName ?? "";
        data["Должность_Пд"] = act.ContractorRep?.Position ?? "";

        // Иные лица (И1, И2, И3)
        data["ФИО_И1"] = act.OtherPerson1?.FullName ?? "";
        data["Должность_И1"] = act.OtherPerson1?.Position ?? "";

        data["ФИО_И2"] = act.OtherPerson2?.FullName ?? "";
        data["Должность_И2"] = act.OtherPerson2?.Position ?? "";

        data["ФИО_И3"] = act.OtherPerson3?.FullName ?? "";
        data["Должность_И3"] = act.OtherPerson3?.Position ?? "";

        // ==================== РАБОТЫ ====================
        data["Наименование_работ"] = act.WorkName;
        data["Наим_пр_докум"] = act.ProjectDocumentation;

        // Материалы и Схемы (через промежуточные таблицы)
        data["Наименование_материалов"] = FormatMaterials(act, outputDir);
        data["Исполнительные_схемы"] = FormatSchemas(act, outputDir);

        // ==================== ДАТЫ РАБОТ ====================

        // Начало
        if (act.WorkStartDate.HasValue)
        {
            data["день_н"] = act.WorkStartDate.Value.Day.ToString("D2");
            data["месяц_н"] = GetMonthName(act.WorkStartDate.Value.Month);
            data["год_н"] = act.WorkStartDate.Value.Year.ToString();
        }
        else
        {
            data["день_н"] = "";
            data["месяц_н"] = "";
            data["год_н"] = "";
        }

        // Окончание
        if (act.WorkEndDate.HasValue)
        {
            data["день_о"] = act.WorkEndDate.Value.Day.ToString("D2");
            data["месяц_о"] = GetMonthName(act.WorkEndDate.Value.Month);
            data["год_о"] = act.WorkEndDate.Value.Year.ToString();
        }
        else
        {
            data["день_о"] = "";
            data["месяц_о"] = "";
            data["год_о"] = "";
        }

        // ==================== ПРОЧЕЕ ====================
        data["выполнено_с"] = act.StandardReference;
        data["Наименование_последующих_работ"] = act.SubsequentWork;
        data["Доп_сведения"] = act.AdditionalInfo;
        data["экз"] = act.CopiesCount?.ToString() ?? "";

        // ==================== ПРИЛОЖЕНИЯ ====================
        data["Приложения"] = FormatAppendix(act, outputDir);
        data["Приложения_вручную"] = act.Appendix;

        // Наименование исполнителя (берём Company из подрядчика или генподрядчика)
        var executorName = act.ContractorRep?.OrganizationRequisites
            ?? act.GenContractorRep?.OrganizationRequisites
            ?? act.ConstructionObject?.Contractor
            ?? "";
        data["н_исполнителя"] = executorName;
        data["Наименование_исполнителя"] = executorName;

        // Блок АООК
        data["использование_по_назначению"] = act.UsageAsIntended;
        data["процент_нагрузки"] = act.LoadPercentage;
        data["условия_полного_нагружения"] = act.FullLoadConditions;

        return data;
    }

    /// <summary>
    /// Форматирование материалов для подстановки в закладку.
    /// Самостоятельно решает, создавать ли реестр, с учётом настроек IsEnabled.
    /// </summary>
    private static string FormatMaterials(Act act, string outputDir)
    {
        if (act.ActMaterials == null || act.ActMaterials.Count == 0)
            return "";

        // Проверяем, включены ли материалы в настройках
        var orderItems = ViewModels.SettingsViewModel.GetApplicationOrder();
        var materialsEnabled = orderItems.FirstOrDefault(x => x.Key == "Materials")?.IsEnabled ?? true;

        if (!materialsEnabled)
            return ""; // Материалы отключены — не добавляем ничего

        // Решаем, создавать ли реестр
        string? materialsRegistryName = null;
        if (act.ActMaterials.Count > Models.Settings.RegistryLimit)
        {
            var registryService = new RegistryService();
            materialsRegistryName = registryService.CreateMaterialsRegistry(act, act.ActMaterials, outputDir);
        }

        // Если создан реестр, возвращаем ссылку на него
        if (!string.IsNullOrEmpty(materialsRegistryName))
        {
            return $"см. Реестр материалов ({materialsRegistryName})";
        }

        // Иначе перечисляем текстом
        return string.Join("; ", act.ActMaterials
            .Where(am => am.Material != null)
            .Select(am => $"{am.Material.Name} №{am.Material.CertificateNumber}"));
    }

    /// <summary>
    /// Форматирование схем для подстановки в закладку.
    /// Самостоятельно решает, создавать ли реестр, с учётом настроек IsEnabled.
    /// </summary>
    private static string FormatSchemas(Act act, string outputDir)
    {
        if (act.ActSchemas == null || act.ActSchemas.Count == 0)
            return "";

        // Проверяем, включены ли схемы в настройках
        var orderItems = ViewModels.SettingsViewModel.GetApplicationOrder();
        var schemasEnabled = orderItems.FirstOrDefault(x => x.Key == "Schemas")?.IsEnabled ?? true;
        var protocolsEnabled = orderItems.FirstOrDefault(x => x.Key == "Protocols")?.IsEnabled ?? true;

        if (!schemasEnabled)
            return ""; // Схемы отключены — не добавляем ничего

        // Решаем, создавать ли реестр приложений (схемы + протоколы, только если оба включены)
        string? attachmentsRegistryName = null;
        var totalEnabledAttachments = 0;
        if (schemasEnabled && act.ActSchemas.Any()) totalEnabledAttachments++;
        if (protocolsEnabled && act.Protocols != null && act.Protocols.Any()) totalEnabledAttachments++;

        // Считаем только включённые элементы для порога реестра
        var totalEnabledCount = 0;
        if (schemasEnabled) totalEnabledCount += act.ActSchemas.Count;
        if (protocolsEnabled) totalEnabledCount += act.Protocols?.Count ?? 0;

        if (totalEnabledCount > Models.Settings.RegistryLimit)
        {
            var registryService = new RegistryService();
            // В реестр попадут только включённые элементы
            var schemasForRegistry = schemasEnabled ? act.ActSchemas : new List<ActSchema>();
            var protocolsForRegistry = protocolsEnabled ? act.Protocols : new List<Protocol>();
            attachmentsRegistryName = registryService.CreateAttachmentsRegistry(act, schemasForRegistry, protocolsForRegistry, outputDir);
        }

        // Если создан реестр приложений, возвращаем ссылку на него
        if (!string.IsNullOrEmpty(attachmentsRegistryName))
        {
            return $"см. Реестр приложений ({attachmentsRegistryName})";
        }

        // Иначе перечисляем текстом
        return string.Join("; ", act.ActSchemas
            .Where(asc => asc.Schema != null)
            .Select(asc => $"{asc.Schema.Number} от {asc.Schema.Date:dd.MM.yyyy}"));
    }

    /// <summary>
    /// Форматирование поля "Приложения" с учётом реестров и настраиваемого порядка.
    /// Объединяет схемы, протоколы, материалы и ручные приложения в одну строку.
    /// Порядок определяется из настроек (DocumentSettings), fallback на дефолт.
    /// Реестры создаются здесь же, с учётом IsEnabled каждого типа.
    /// </summary>
    private static string FormatAppendix(Act act, string outputDir)
    {
        // Получаем порядок из настроек
        var orderItems = ViewModels.SettingsViewModel.GetApplicationOrder();

        // Сначала определяем, какие типы включены
        var schemasEnabled = orderItems.FirstOrDefault(x => x.Key == "Schemas")?.IsEnabled ?? true;
        var protocolsEnabled = orderItems.FirstOrDefault(x => x.Key == "Protocols")?.IsEnabled ?? true;
        var materialsEnabled = orderItems.FirstOrDefault(x => x.Key == "Materials")?.IsEnabled ?? true;
        var manualEnabled = orderItems.FirstOrDefault(x => x.Key == "Manual")?.IsEnabled ?? true;

        // Считаем количество включённых элементов для решения о реестре приложений
        var totalEnabledAttachments = 0;
        if (schemasEnabled) totalEnabledAttachments += act.ActSchemas?.Count ?? 0;
        if (protocolsEnabled) totalEnabledAttachments += act.Protocols?.Count ?? 0;

        // Создаём реестр приложений только если включённые элементы превышают порог
        string? attachmentsRegistryName = null;
        if (totalEnabledAttachments > Models.Settings.RegistryLimit)
        {
            var registryService = new RegistryService();
            var schemasForRegistry = schemasEnabled ? (act.ActSchemas ?? new List<ActSchema>()) : new List<ActSchema>();
            var protocolsForRegistry = protocolsEnabled ? (act.Protocols ?? new List<Protocol>()) : new List<Protocol>();
            attachmentsRegistryName = registryService.CreateAttachmentsRegistry(act, schemasForRegistry, protocolsForRegistry, outputDir);
        }

        // Реестр материалов
        string? materialsRegistryName = null;
        if (materialsEnabled && act.ActMaterials != null && act.ActMaterials.Count > Models.Settings.RegistryLimit)
        {
            var registryService = new RegistryService();
            materialsRegistryName = registryService.CreateMaterialsRegistry(act, act.ActMaterials, outputDir);
        }

        var appsList = new List<string>();

        // Проходим по элементам в заданном порядке
        foreach (var item in orderItems)
        {
            // Пропускаем отключённые элементы
            if (!item.IsEnabled) continue;

            switch (item.Key)
            {
                case "Schemas":
                    if (act.ActSchemas != null && act.ActSchemas.Any())
                    {
                        if (!string.IsNullOrEmpty(attachmentsRegistryName))
                        {
                            appsList.Add($"Исполнительные схемы - см. Реестр приложений ({attachmentsRegistryName})");
                        }
                        else
                        {
                            var schemasStr = string.Join("; ", act.ActSchemas
                                .Where(asc => asc.Schema != null)
                                .Select(asc => $"{asc.Schema.Number} от {asc.Schema.Date:dd.MM.yyyy}"));
                            appsList.Add($"Исполнительные схемы: {schemasStr}");
                        }
                    }
                    break;

                case "Protocols":
                    if (act.Protocols != null && act.Protocols.Any())
                    {
                        // Протоколы включены в общий реестр приложений, не дублируем
                        if (string.IsNullOrEmpty(attachmentsRegistryName))
                        {
                            var protocolsStr = string.Join("; ", act.Protocols
                                .Select(p => $"{p.Number} от {p.Date:dd.MM.yyyy} — {p.Laboratory}"));
                            appsList.Add($"Протоколы испытаний: {protocolsStr}");
                        }
                    }
                    break;

                case "Materials":
                    if (act.ActMaterials != null && act.ActMaterials.Any())
                    {
                        if (!string.IsNullOrEmpty(materialsRegistryName))
                        {
                            appsList.Add($"Сертификаты на материалы - см. Реестр материалов ({materialsRegistryName})");
                        }
                        else
                        {
                            var matsStr = string.Join("; ", act.ActMaterials
                                .Take(Models.Settings.RegistryLimit)
                                .Where(am => am.Material != null)
                                .Select(am => $"{am.Material.Name} №{am.Material.CertificateNumber}"));

                            if (act.ActMaterials.Count > Models.Settings.RegistryLimit)
                                matsStr += " и др.";

                            appsList.Add($"Материалы: {matsStr}");
                        }
                    }
                    break;

                case "Manual":
                    if (!string.IsNullOrWhiteSpace(act.Appendix))
                    {
                        appsList.Add(act.Appendix);
                    }
                    break;
            }
        }

        return string.Join("\n", appsList);
    }

    /// <summary>
    /// Получить название месяца прописью в родительном падеже.
    /// </summary>
    private static string GetMonthName(int month)
    {
        var months = new[]
        {
            "", "января", "февраля", "марта", "апреля", "мая", "июня",
            "июля", "августа", "сентября", "октября", "ноября", "декабря"
        };
        return months[month];
    }

    /// <summary>
    /// Заменить всё содержимое закладки новым текстом.
    /// Сохраняет форматирование (шрифт, размер, начертание) из первого Run закладки.
    /// </summary>
    private static void ReplaceBookmarkText(BookmarkStart bookmark, string text)
    {
        var parent = bookmark.Parent;
        if (parent == null) return;

        // Находим соответствующий BookmarkEnd по ID
        var bookmarkEnd = parent.Descendants<BookmarkEnd>()
            .FirstOrDefault(be => be.Id != null && be.Id.Value == bookmark.Id?.Value);

        if (bookmarkEnd == null)
        {
            // Fallback: если BookmarkEnd не найден, просто вставляем после BookmarkStart
            var fallbackRun = new Run(new Text(text));
            bookmark.InsertAfterSelf(fallbackRun);
            return;
        }

        // Собираем все узлы между BookmarkStart и BookmarkEnd
        var nodesToDelete = new List<OpenXmlElement>();
        RunProperties? originalProps = null;
        var currentNode = bookmark.NextSibling();

        while (currentNode != null && currentNode != bookmarkEnd)
        {
            nodesToDelete.Add(currentNode);

            // Запоминаем форматирование первого найденного Run
            if (originalProps == null && currentNode is Run run)
            {
                originalProps = run.RunProperties?.CloneNode(true) as RunProperties;
            }

            currentNode = currentNode.NextSibling();
        }

        // Удаляем найденные узлы (с конца к началу, чтобы не сбить ссылки)
        for (var i = nodesToDelete.Count - 1; i >= 0; i--)
        {
            nodesToDelete[i].Remove();
        }

        // Создаём новый Run с тем же форматированием
        var newRun = new Run(new Text(text));
        if (originalProps != null)
        {
            newRun.RunProperties = originalProps;
        }

        bookmark.InsertAfterSelf(newRun);
    }

    /// <summary>
    /// Удалить пустые таблицы для АООК/АРООКС.
    /// </summary>
    private static void RemoveEmptyTablesForAOOK(Body body, Act act)
    {
        if (string.IsNullOrWhiteSpace(act.SubsequentWork))
            RemoveTableByBookmark(body, "Последующие_работы");

        if (string.IsNullOrWhiteSpace(act.LoadPercentage))
            RemoveTableByBookmark(body, "Нагрузка");

        if (string.IsNullOrWhiteSpace(act.FullLoadConditions))
            RemoveTableByBookmark(body, "Условия_нагружения");

        if (string.IsNullOrWhiteSpace(act.UsageAsIntended))
            RemoveTableByBookmark(body, "По_назначению");
    }

    /// <summary>
    /// Удалить пустые блоки для АОУСИТО.
    /// </summary>
    private static void RemoveEmptyBlocksForAOUSITO(Body body, Act act)
    {
        if (string.IsNullOrWhiteSpace(act.UsageAsIntended))
            RemoveTableByBookmark(body, "По_назначению");

        if (string.IsNullOrWhiteSpace(act.LoadPercentage))
            RemoveTableByBookmark(body, "Нагрузка");
    }

    /// <summary>
    /// Найти таблицу, содержащую закладку, и удалить её.
    /// </summary>
    private static void RemoveTableByBookmark(Body body, string bookmarkName)
    {
        var bookmark = body.Descendants<BookmarkStart>()
            .FirstOrDefault(b => b.Name != null && string.Equals(b.Name.Value, bookmarkName, StringComparison.OrdinalIgnoreCase));

        if (bookmark != null)
        {
            var table = bookmark.Ancestors<Table>().FirstOrDefault();
            table?.Remove();
        }
    }

    /// <summary>
    /// Получить имя файла шаблона по типу акта.
    /// </summary>
    private static string GetTemplateName(string actType)
    {
        return actType switch
        {
            "АОСР" => "АОСР.docx",
            "АООК" => "АООК.docx",
            "АОУСИТО" => "АОУСИТО.docx",
            "АВК" => "АВК.docx",
            "АГИ" => "АГИ.docx",
            "АИИО" => "АИИО.docx",
            "АОГРОКС" => "АОГРОКС.docx",
            "АОСР3" => "АОСР3.docx",
            "АПрОб" => "АПрОб.docx",
            "АРООКС" => "АРООКС.docx",
            "ОтЭфД" => "ОтЭфД.docx",
            "АОСР_старый" => "АОСР_старый.docx",
            "АООК_старый" => "АООК_старый.docx",
            _ => "АОСР.docx"
        };
    }

    /// <summary>
    /// Очистить имя файла от недопустимых символов.
    /// </summary>
    private static string SanitizeFileName(string fileName)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            fileName = fileName.Replace(c, '_');
        return fileName.Trim('_', ' ');
    }
}
