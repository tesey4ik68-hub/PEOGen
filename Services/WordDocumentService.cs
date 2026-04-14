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

            // Специфическая очистка для АОСР ВРЕМЕННО ОТКЛЮЧЕНА.
            // Причина: в текущем шаблоне верхний блок с объектом и реквизитами
            // находится в общей таблице, и удаление ancestor table по одной пустой
            // закладке сносит всю первую страницу/первый крупный блок документа.
            //
            // После стабилизации шаблона здесь нужно будет реализовать более точечную
            // очистку по строкам/абзацам, а не удаление всей таблицы.
            //
            // if (act.Type == "АОСР")
            // {
            //     RemoveEmptyBlocksForAOSR(body, act);
            // }

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
    /// Для типа АОСР используется отдельная AOSR-логика.
    /// </summary>
    private Dictionary<string, string> GetActDataDictionary(Act act, string outputDir)
    {
        // Для АОСР — отдельная логика подготовки данных
        if (act.Type == "АОСР")
        {
            return BuildAOSRData(act, outputDir);
        }

        // Для всех остальных типов — общая логика
        return BuildGeneralActData(act, outputDir);
    }

    /// <summary>
    /// Общая логика сбора данных для всех типов актов, кроме АОСР.
    /// Повторяет предыдущую реализацию GetActDataDictionary.
    /// </summary>
    private Dictionary<string, string> BuildGeneralActData(Act act, string outputDir)
    {
        var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // ==================== ОБЪЕКТ И РЕКВИЗИТЫ ====================
        var obj = act.ConstructionObject;
        if (obj != null)
        {
            data["Наименование_объекта"] = obj.Name;
        }
        else
        {
            data["Наименование_объекта"] = "";
        }

        // Реквизиты — приоритет: организация акта → fallback на объект
        data["реквизиты_заказчика"] = GetOrganizationRequisites(
            act.CustomerOrganization, obj?.Customer, obj?.CustomerRequisites);

        data["реквизиты_генподрядчика"] = GetOrganizationRequisites(
            act.GenContractorOrganization, obj?.Contractor, obj?.ContractorRequisites);

        data["реквизиты_проектировщика"] = GetOrganizationRequisites(
            act.DesignerOrganization, null, obj?.DesignerRequisites);

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

        // Наименование исполнителя — приоритет: организация акта → fallback на сотрудника → объект
        var executorName = act.ContractorOrganization?.Name
            ?? act.GenContractorOrganization?.Name
            ?? act.ContractorRep?.OrganizationName
            ?? act.GenContractorRep?.OrganizationName
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
    /// Отдельная логика сбора данных для АОСР.
    /// Заполняет закладки по VBA-подобной логике:
    /// - объект + адрес одной строкой,
    /// - реквизиты: [Имя организации]. [Реквизиты],
    /// - Наим_пр_докум — агрегированно из привязанной проектной документации,
    /// - выполнено_с — комбинированно,
    /// - материалы/схемы/приложения — по AOSR-логике с учётом реестров,
    /// - Наименование_исполнителя — именно имя организации, не реквизиты.
    /// </summary>
    private Dictionary<string, string> BuildAOSRData(Act act, string outputDir)
    {
        var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var obj = act.ConstructionObject;

        // ==================== ОБЪЕКТ И РЕКВИЗИТЫ ====================

        // Наименование_объекта: [Название объекта] + " " + [Адрес объекта]
        if (obj != null)
        {
            data["Наименование_объекта"] = CombineObjectAddress(obj.Name, obj.Address);
        }
        else
        {
            data["Наименование_объекта"] = "";
        }

        // Реквизиты — приоритет: организация акта → fallback на объект
        data["реквизиты_заказчика"] = GetOrganizationRequisites(
            act.CustomerOrganization, obj?.Customer, obj?.CustomerRequisites);

        data["реквизиты_генподрядчика"] = GetOrganizationRequisites(
            act.GenContractorOrganization, obj?.Contractor, obj?.ContractorRequisites);

        data["реквизиты_проектировщика"] = GetOrganizationRequisites(
            act.DesignerOrganization, null, obj?.DesignerRequisites);

        // ==================== НОМЕР И ДАТА АКТА ====================
        data["Номер_акта"] = act.ActNumber;

        var actDate = act.ActDate;
        data["день_а"] = actDate.Day.ToString("D2");
        data["месяц_а"] = GetMonthName(actDate.Month);
        data["год_а"] = actDate.Year.ToString();

        // ==================== ПОДПИСАНТЫ (ФИО и Должности) ====================
        data["ФИО_ТНЗ"] = act.CustomerRep?.FullName ?? "";
        data["Должность_ТНЗ"] = act.CustomerRep?.Position ?? "";

        data["ФИО_Г"] = act.GenContractorRep?.FullName ?? "";
        data["Должность_Г"] = act.GenContractorRep?.Position ?? "";

        data["ФИО_ТНГ"] = act.GenContractorSkRep?.FullName ?? "";
        data["Должность_ТНГ"] = act.GenContractorSkRep?.Position ?? "";

        data["ФИО_Пр"] = act.DesignerRep?.FullName ?? "";
        data["Должность_Пр"] = act.DesignerRep?.Position ?? "";

        data["ФИО_Пд"] = act.ContractorRep?.FullName ?? "";
        data["Должность_Пд"] = act.ContractorRep?.Position ?? "";

        data["ФИО_И1"] = act.OtherPerson1?.FullName ?? "";
        data["Должность_И1"] = act.OtherPerson1?.Position ?? "";

        data["ФИО_И2"] = act.OtherPerson2?.FullName ?? "";
        data["Должность_И2"] = act.OtherPerson2?.Position ?? "";

        data["ФИО_И3"] = act.OtherPerson3?.FullName ?? "";
        data["Должность_И3"] = act.OtherPerson3?.Position ?? "";

        // ==================== РАБОТЫ ====================

        // Наименование_работ — текущая пользовательская логика (НЕ откатываем)
        data["Наименование_работ"] = act.AutoWorkName;

        // Наим_пр_докум — агрегированно из привязанной проектной документации
        data["Наим_пр_докум"] = BuildAOSRProjectDoc(act);

        // ==================== МАТЕРИАЛЫ (AOSR-specific) ====================
        data["Наименование_материалов"] = BuildAOSRMaterials(act, outputDir);

        // ==================== ИСПОЛНИТЕЛЬНЫЕ СХЕМЫ (схема + протоколы) ====================
        data["Исполнительные_схемы"] = BuildAOSRSchemasAndProtocols(act, outputDir);

        // ==================== ДАТЫ РАБОТ ====================
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

        // ==================== ВЫПОЛНЕНО В СООТВЕТСТВИИ С ====================
        data["выполнено_с"] = BuildAOSRStandardReference(act);

        // ==================== ПРОЧЕЕ ====================
        data["Наименование_последующих_работ"] = act.SubsequentWork;
        data["Доп_сведения"] = act.AdditionalInfo;
        data["экз"] = act.CopiesCount?.ToString() ?? "";

        // ==================== ПРИЛОЖЕНИЯ (AOSR-specific) ====================
        data["Приложения"] = BuildAOSRAppendix(act, outputDir);
        data["Приложения_вручную"] = act.Appendix;

        // ==================== НАИМЕНОВАНИЕ ИСПОЛНИТЕЛЯ ====================
        // Подставляем именно наименование организации, не реквизиты
        data["н_исполнителя"] = GetAOSRExecutorName(act);
        data["Наименование_исполнителя"] = GetAOSRExecutorName(act);

        // Блок АООК (для АОСР обычно пуст, но заполняем на случай наличия закладок)
        data["использование_по_назначению"] = act.UsageAsIntended;
        data["процент_нагрузки"] = act.LoadPercentage;
        data["условия_полного_нагружения"] = act.FullLoadConditions;

        return data;
    }

    // ==================== AOSR HELPER METHODS ====================

    /// <summary>
    /// Комбинировать название объекта и адрес одной строкой.
    /// Если адрес пустой — не добавлять лишние пробелы.
    /// </summary>
    private static string CombineObjectAddress(string name, string address)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";
        if (string.IsNullOrWhiteSpace(address)) return name.Trim();
        return $"{name.Trim()} {address.Trim()}";
    }

    /// <summary>
    /// Комбинировать наименование организации и реквизиты.
    /// Формат: [Наименование организации]. [Реквизиты]
    /// Если реквизиты пустые — без точки. Если наименование пустое — только реквизиты.
    /// </summary>
    private static string CombineOrganizationRequisites(string orgName, string requisites)
    {
        var hasName = !string.IsNullOrWhiteSpace(orgName);
        var hasReq = !string.IsNullOrWhiteSpace(requisites);

        if (hasName && hasReq)
            return $"{orgName.Trim()}. {requisites.Trim()}";
        if (hasName)
            return orgName.Trim();
        if (hasReq)
            return requisites.Trim();
        return "";
    }

    /// <summary>
    /// Получить реквизиты организации с fallback на объект.
    /// Приоритет: Organization из акта → fallback на имя и реквизиты из объекта.
    /// </summary>
    private static string GetOrganizationRequisites(
        Organization? actOrg,
        string? objectOrgName,
        string? objectOrgRequisites)
    {
        // Если есть организация из акта — используем её
        if (actOrg != null)
            return actOrg.FullRequisites;

        // Fallback на объект
        return CombineOrganizationRequisites(objectOrgName ?? "", objectOrgRequisites ?? "");
    }

    /// <summary>
    /// Сбор Наим_пр_докум для АОСР — агрегированно из привязанной проектной документации акта.
    /// Аналог VBA qВсеДанныеВАкт[п2] — собирает строку из ActProjectDocs.
    /// </summary>
    private static string BuildAOSRProjectDoc(Act act)
    {
        if (act.ActProjectDocs == null || act.ActProjectDocs.Count == 0)
        {
            // Fallback на одно поле, если привязанной документации нет
            return act.ProjectDocumentation ?? "";
        }

        var parts = act.ActProjectDocs
            .Where(apd => apd.ProjectDoc != null)
            .Select(apd => $"{apd.ProjectDoc.Code} — {apd.ProjectDoc.Name}")
            .ToList();

        return string.Join("; ", parts);
    }

    /// <summary>
    /// Сбор "выполнено_с" для АОСР — комбинированное значение:
    /// [ручное поле] + [агрегированная проектная/нормативная часть].
    /// Не дублирует одинаковые значения.
    /// </summary>
    private static string BuildAOSRStandardReference(Act act)
    {
        var parts = new List<string>();

        // Ручное поле "Выполнено в соответствии с"
        if (!string.IsNullOrWhiteSpace(act.StandardReference))
            parts.Add(act.StandardReference.Trim());

        // Если есть привязанная проектная документация — добавляем её
        if (act.ActProjectDocs != null && act.ActProjectDocs.Count > 0)
        {
            var docRefs = act.ActProjectDocs
                .Where(apd => apd.ProjectDoc != null)
                .Select(apd => $"{apd.ProjectDoc.Code} — {apd.ProjectDoc.Name}")
                .ToList();

            foreach (var docRef in docRefs)
            {
                // Не дублируем, если уже есть в parts
                if (!parts.Any(p => p.Contains(docRef, StringComparison.OrdinalIgnoreCase)))
                    parts.Add(docRef);
            }
        }

        return string.Join("; ", parts);
    }

    /// <summary>
    /// Сбор материалов для АОСР — AOSR-specific builder.
    /// - если материалов нет → пусто
    /// - если материалов <= лимита → перечисление текстом
    /// - если материалов > лимита и реестр → текст ссылки на реестр
    /// </summary>
    private static string BuildAOSRMaterials(Act act, string outputDir)
    {
        if (act.ActMaterials == null || act.ActMaterials.Count == 0)
            return "";

        var orderItems = ViewModels.SettingsViewModel.GetApplicationOrder();
        var materialsEnabled = orderItems.FirstOrDefault(x => x.Key == "Materials")?.IsEnabled ?? true;

        if (!materialsEnabled)
            return "";

        string? materialsRegistryName = null;
        if (act.ActMaterials.Count > Models.Settings.RegistryLimit)
        {
            var registryService = new RegistryService();
            materialsRegistryName = registryService.CreateMaterialsRegistry(act, act.ActMaterials, outputDir);
        }

        if (!string.IsNullOrEmpty(materialsRegistryName))
        {
            return $"см. Реестр материалов ({materialsRegistryName})";
        }

        return string.Join("; ", act.ActMaterials
            .Where(am => am.Material != null)
            .Select(am => $"{am.Material.Name} №{am.Material.CertificateNumber}"));
    }

    /// <summary>
    /// Сбор исполнительных схем для АОСР — учитывает схемы + протоколы.
    /// Если реестр приложений нужен — подставляет ссылку на реестр.
    /// </summary>
    private static string BuildAOSRSchemasAndProtocols(Act act, string outputDir)
    {
        var orderItems = ViewModels.SettingsViewModel.GetApplicationOrder();
        var schemasEnabled = orderItems.FirstOrDefault(x => x.Key == "Schemas")?.IsEnabled ?? true;
        var protocolsEnabled = orderItems.FirstOrDefault(x => x.Key == "Protocols")?.IsEnabled ?? true;

        if (!schemasEnabled && !protocolsEnabled)
            return "";

        var hasSchemas = schemasEnabled && act.ActSchemas != null && act.ActSchemas.Any();
        var hasProtocols = protocolsEnabled && act.Protocols != null && act.Protocols.Any();

        if (!hasSchemas && !hasProtocols)
            return "";

        // Считаем общий объём для решения о реестре
        var totalCount = 0;
        if (hasSchemas) totalCount += act.ActSchemas.Count;
        if (hasProtocols) totalCount += act.Protocols.Count;

        // Если превышен порог — создаём общий реестр
        if (totalCount > Models.Settings.RegistryLimit)
        {
            var registryService = new RegistryService();
            var schemasForRegistry = hasSchemas ? act.ActSchemas : new List<ActSchema>();
            var protocolsForRegistry = hasProtocols ? act.Protocols : new List<Protocol>();
            var registryName = registryService.CreateAttachmentsRegistry(act, schemasForRegistry, protocolsForRegistry, outputDir);
            return $"см. Реестр приложений ({registryName})";
        }

        // Иначе перечисляем текстом: схемы + протоколы
        var parts = new List<string>();

        if (hasSchemas)
        {
            var schemasStr = string.Join("; ", act.ActSchemas
                .Where(asc => asc.Schema != null)
                .Select(asc => $"{asc.Schema.Number} от {asc.Schema.Date:dd.MM.yyyy}"));
            parts.Add(schemasStr);
        }

        if (hasProtocols)
        {
            var protocolsStr = string.Join("; ", act.Protocols
                .Select(p => $"{p.Number} от {p.Date:dd.MM.yyyy} — {p.Laboratory}"));
            parts.Add(protocolsStr);
        }

        return string.Join("; ", parts);
    }

    /// <summary>
    /// Сбор приложений для АОСР — AOSR-ориентированная сборка.
    /// Учитывает порядок из настроек, ручные приложения, материалы, схемы, протоколы, реестры.
    /// Формирует нумерацию "Приложение №1 ...", "Приложение №2 ..."
    /// </summary>
    private static string BuildAOSRAppendix(Act act, string outputDir)
    {
        var orderItems = ViewModels.SettingsViewModel.GetApplicationOrder();

        var schemasEnabled = orderItems.FirstOrDefault(x => x.Key == "Schemas")?.IsEnabled ?? true;
        var protocolsEnabled = orderItems.FirstOrDefault(x => x.Key == "Protocols")?.IsEnabled ?? true;
        var materialsEnabled = orderItems.FirstOrDefault(x => x.Key == "Materials")?.IsEnabled ?? true;
        var manualEnabled = orderItems.FirstOrDefault(x => x.Key == "Manual")?.IsEnabled ?? true;

        // Считаем включённые элементы для решения о реестре
        var totalAttachments = 0;
        if (schemasEnabled) totalAttachments += act.ActSchemas?.Count ?? 0;
        if (protocolsEnabled) totalAttachments += act.Protocols?.Count ?? 0;

        string? attachmentsRegistryName = null;
        if (totalAttachments > Models.Settings.RegistryLimit)
        {
            var registryService = new RegistryService();
            var schemasForRegistry = schemasEnabled ? (act.ActSchemas ?? new List<ActSchema>()) : new List<ActSchema>();
            var protocolsForRegistry = protocolsEnabled ? (act.Protocols ?? new List<Protocol>()) : new List<Protocol>();
            attachmentsRegistryName = registryService.CreateAttachmentsRegistry(act, schemasForRegistry, protocolsForRegistry, outputDir);
        }

        string? materialsRegistryName = null;
        if (materialsEnabled && act.ActMaterials != null && act.ActMaterials.Count > Models.Settings.RegistryLimit)
        {
            var registryService = new RegistryService();
            materialsRegistryName = registryService.CreateMaterialsRegistry(act, act.ActMaterials, outputDir);
        }

        // Собираем список приложений с нумерацией
        var appsList = new List<string>();
        var appNumber = 1;

        foreach (var item in orderItems)
        {
            if (!item.IsEnabled) continue;

            string? entryText = null;

            switch (item.Key)
            {
                case "Schemas":
                    if (act.ActSchemas != null && act.ActSchemas.Any())
                    {
                        if (!string.IsNullOrEmpty(attachmentsRegistryName))
                        {
                            entryText = $"Приложение №{appNumber} — Реестр исполнительных схем ({attachmentsRegistryName})";
                        }
                        else
                        {
                            var schemasStr = string.Join("; ", act.ActSchemas
                                .Where(asc => asc.Schema != null)
                                .Select(asc => $"{asc.Schema.Number} от {asc.Schema.Date:dd.MM.yyyy}"));
                            entryText = $"Приложение №{appNumber} — Исполнительные схемы: {schemasStr}";
                        }
                    }
                    break;

                case "Protocols":
                    if (act.Protocols != null && act.Protocols.Any())
                    {
                        if (string.IsNullOrEmpty(attachmentsRegistryName))
                        {
                            var protocolsStr = string.Join("; ", act.Protocols
                                .Select(p => $"{p.Number} от {p.Date:dd.MM.yyyy} — {p.Laboratory}"));
                            entryText = $"Приложение №{appNumber} — Протоколы испытаний: {protocolsStr}";
                        }
                    }
                    break;

                case "Materials":
                    if (act.ActMaterials != null && act.ActMaterials.Any())
                    {
                        if (!string.IsNullOrEmpty(materialsRegistryName))
                        {
                            entryText = $"Приложение №{appNumber} — Реестр материалов ({materialsRegistryName})";
                        }
                        else
                        {
                            var matsStr = string.Join("; ", act.ActMaterials
                                .Take(Models.Settings.RegistryLimit)
                                .Where(am => am.Material != null)
                                .Select(am => $"{am.Material.Name} №{am.Material.CertificateNumber}"));

                            if (act.ActMaterials.Count > Models.Settings.RegistryLimit)
                                matsStr += " и др.";

                            entryText = $"Приложение №{appNumber} — Материалы: {matsStr}";
                        }
                    }
                    break;

                case "Manual":
                    if (!string.IsNullOrWhiteSpace(act.Appendix))
                    {
                        entryText = $"Приложение №{appNumber} — {act.Appendix}";
                    }
                    break;
            }

            if (!string.IsNullOrEmpty(entryText))
            {
                appsList.Add(entryText);
                appNumber++;
            }
        }

        return string.Join("\n", appsList);
    }

    /// <summary>
    /// Получить наименование организации-исполнителя для АОСР.
    /// Приоритет: подрядчик → генподрядчик → объект.Contractor.
    /// Подставляем именно наименование (Company), не реквизиты.
    /// </summary>
    private static string GetAOSRExecutorName(Act act)
    {
        // Приоритет: Подрядчик (организация из справочника)
        if (act.ContractorOrganization != null)
            return act.ContractorOrganization.Name.Trim();

        // Генподрядчик (организация из справочника)
        if (act.GenContractorOrganization != null)
            return act.GenContractorOrganization.Name.Trim();

        // Fallback: Подрядчик — наименование из сотрудника
        var contractorName = act.ContractorRep?.OrganizationName;
        if (!string.IsNullOrWhiteSpace(contractorName))
            return contractorName.Trim();

        // Fallback: Генподрядчик — наименование из сотрудника
        var genContractorName = act.GenContractorRep?.OrganizationName;
        if (!string.IsNullOrWhiteSpace(genContractorName))
            return genContractorName.Trim();

        // Fallback: Объект — Contractor (наименование организации)
        var objContractor = act.ConstructionObject?.Contractor;
        if (!string.IsNullOrWhiteSpace(objContractor))
            return objContractor.Trim();

        return "";
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
    /// Удалить пустые блоки для АОСР.
    /// Удаляет таблицы, содержащие закладки, если соответствующие поля акта пусты.
    /// Безопасный способ: по bookmark → таблица → удаление.
    /// </summary>
    /// <summary>
    /// Очистка пустых блоков для АОСР временно отключена.
    /// Старый вариант удалял целую ancestor-table по одной пустой закладке,
    /// из-за чего мог исчезать весь верхний блок документа с объектом и реквизитами.
    /// </summary>
    private static void RemoveEmptyBlocksForAOSR(Body body, Act act)
    {
        // Временно ничего не удаляем.
        // Для АОСР нужна адресная очистка по строкам/абзацам шаблона,
        // а не удаление всей таблицы.
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
