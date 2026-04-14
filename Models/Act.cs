using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace AGenerator.Models;

/// <summary>
/// Акт (аналог листа "База актов" в Excel)
/// </summary>
public class Act
{
    public int Id { get; set; }

    public int ConstructionObjectId { get; set; }
    public ConstructionObject? ConstructionObject { get; set; }

    // ==================== ОСНОВНЫЕ ПОЛЯ ====================

    public string ActNumber { get; set; } = string.Empty;
    public string Type { get; set; } = "АОСР"; // АОСР, АООК, АОИО, etc.
    public string WorkName { get; set; } = string.Empty;
    public string WorkDescription { get; set; } = string.Empty;
    public string Interval { get; set; } = string.Empty;
    public string IntervalType { get; set; } = "на интервале";

    // ==================== ДАТЫ ====================

    public DateTime? WorkStartDate { get; set; }
    public DateTime? WorkEndDate { get; set; }
    public DateTime ActDate { get; set; } = DateTime.Now;
    public DateTime? ProtocolDate { get; set; }

    // ==================== СВЯЗИ С ДРУГИМИ АКТАМИ ====================

    public int? RelatedActId { get; set; } // ИД (исполнительная документация)
    public Act? RelatedAct { get; set; } // Навигационное свойство
    public int? RelatedAookId { get; set; } // АООК
    public Act? RelatedAook { get; set; } // Навигационное свойство

    // ==================== ПОДПИСАНТЫ (ID сотрудников) ====================

    public int? CustomerRepId { get; set; } // СК Заказчика
    public Employee? CustomerRep { get; set; } // Навигационное свойство
    public int? GenContractorRepId { get; set; } // СК Генподрядчика
    public Employee? GenContractorRep { get; set; } // Навигационное свойство
    public int? GenContractorSkRepId { get; set; } // Генподрядчик
    public Employee? GenContractorSkRep { get; set; } // Навигационное свойство
    public int? ContractorRepId { get; set; } // Подрядчик
    public Employee? ContractorRep { get; set; } // Навигационное свойство
    public int? AuthorSupervisionId { get; set; } // Авторский надзор
    public Employee? DesignerRep { get; set; } // Навигационное свойство
    public int? OtherPerson1Id { get; set; } // Иное лицо 1
    public Employee? OtherPerson1 { get; set; } // Навигационное свойство
    public int? OtherPerson2Id { get; set; } // Иное лицо 2
    public Employee? OtherPerson2 { get; set; } // Навигационное свойство
    public int? OtherPerson3Id { get; set; } // Иное лицо 3
    public Employee? OtherPerson3 { get; set; } // Навигационное свойство

    // ==================== ОРГАНИЗАЦИИ (СПРАВОЧНИК) ====================

    public int? CustomerOrganizationId { get; set; }
    public Organization? CustomerOrganization { get; set; }
    public int? GenContractorOrganizationId { get; set; }
    public Organization? GenContractorOrganization { get; set; }
    public int? ContractorOrganizationId { get; set; }
    public Organization? ContractorOrganization { get; set; }
    public int? DesignerOrganizationId { get; set; }
    public Organization? DesignerOrganization { get; set; }

    // ==================== УРОВНИ И СТРУКТУРА (из VBA) ====================

    public string Level1 { get; set; } = string.Empty; // Уровень1
    public string Level2 { get; set; } = string.Empty; // Уровень2
    public string Level3 { get; set; } = string.Empty; // Уровень3
    public string Mark { get; set; } = string.Empty; // Отм.
    public string InAxes { get; set; } = string.Empty; // В осях
    public string Volume { get; set; } = string.Empty; // Объём
    public string UnitOfMeasure { get; set; } = string.Empty; // Ед.изм.

    // ==================== ДОП. ПОЛЯ ====================

    public string WorkVolume { get; set; } = string.Empty; // Объем работ
    public string DrawingNumber { get; set; } = string.Empty; // Номер чертежа
    public string ProjectDocumentation { get; set; } = string.Empty; // Проектная документация
    public string StandardReference { get; set; } = string.Empty; // Ссылка на стандарт (Выполнено в соответствии с)
    public string GeoCondition { get; set; } = string.Empty; // Геологические условия
    public string WeatherCondition { get; set; } = string.Empty; // Погодные условия
    public string EquipmentUsed { get; set; } = string.Empty; // Используемое оборудование
    public string MaterialsUsed { get; set; } = string.Empty; // Используемые материалы
    public string QualityControl { get; set; } = string.Empty; // Контроль качества
    public string SafetyMeasures { get; set; } = string.Empty; // Меры безопасности (Условия производства работ)
    public string AdditionalInfo { get; set; } = string.Empty; // Дополнительная информация / Доп. сведения
    public string Remarks { get; set; } = string.Empty; // Примечания

    // ==================== БЛОК АООК ====================

    public string UsageAsIntended { get; set; } = string.Empty; // Использование по назначению
    public string LoadPercentage { get; set; } = string.Empty; // % нагрузки от проектной
    public string FullLoadConditions { get; set; } = string.Empty; // условия полного нагружения

    // ==================== ПРИЛОЖЕНИЯ И ЭКЗЕМПЛЯРЫ ====================

    public string Appendix { get; set; } = string.Empty; // Приложения (вручную)
    public int? CopiesCount { get; set; } // Кол-во экз.

    // ==================== ПОСЛЕДУЮЩИЕ РАБОТЫ ====================

    public string SubsequentWork { get; set; } = string.Empty; // Последующие работы

    // ==================== КОЛЛЕКЦИИ СВЯЗАННЫХ СУЩНОСТЕЙ ====================

    public List<ActMaterial> ActMaterials { get; set; } = new();
    public List<ActSchema> ActSchemas { get; set; } = new();
    public List<ActProjectDoc> ActProjectDocs { get; set; } = new();
    public List<Protocol> Protocols { get; set; } = new();

    // ==================== СТАТУС ГЕНЕРАЦИИ ====================

    public string Status { get; set; } = "Черновик"; // Черновик, Сгенерирован, Подписан
    public string GeneratedFilePath { get; set; } = string.Empty;

    // ==================== МЕТАДАННЫЕ ====================

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }

    // ==================== ВЫЧИСЛЯЕМЫЕ ПОЛЯ (НЕ СОХРАНЯЮТСЯ В БД) ====================

    /// <summary>
    /// Автоназвание работ — вычисляется из WorkName + Level1-3 + Mark + InAxes + Volume + Unit
    /// </summary>
    [NotMapped]
    public string AutoWorkName { get; set; } = string.Empty;

    /// <summary>
    /// Итоговый номер акта — вычисляется по маске: Тип + Номер
    /// </summary>
    [NotMapped]
    public string FinalActNumber { get; set; } = string.Empty;

    /// <summary>
    /// Отображение выбранных материалов через "; "
    /// Пример: "Песок №12123; Бетон В25 №456"
    /// </summary>
    [NotMapped]
    public string MaterialsDisplay
    {
        get
        {
            if (ActMaterials == null || ActMaterials.Count == 0)
                return "—";
            return string.Join("; ", ActMaterials
                .Where(am => am.Material != null)
                .Select(am => $"{am.Material.Name} №{am.Material.CertificateNumber}"));
        }
    }

    /// <summary>
    /// Отображение выбранных схем через "; "
    /// Пример: "АР-1 — План фундаментов; КЖ-2 — Армирование"
    /// </summary>
    [NotMapped]
    public string SchemasDisplay
    {
        get
        {
            if (ActSchemas == null || ActSchemas.Count == 0)
                return "—";
            return string.Join("; ", ActSchemas
                .Where(asc => asc.Schema != null)
                .Select(asc => $"{asc.Schema.Number} — {asc.Schema.Name}"));
        }
    }

    /// <summary>
    /// Отображение привязанных протоколов через "; "
    /// Пример: "123 от 15.01.2026 — Испытание грунта"
    /// </summary>
    [NotMapped]
    public string ProtocolsDisplay
    {
        get
        {
            if (Protocols == null || Protocols.Count == 0)
                return "—";
            return string.Join("; ", Protocols
                .Select(p => $"{p.Number} от {p.Date:dd.MM.yyyy} — {p.Type}"));
        }
    }

    /// <summary>
    /// Отображение выбранной проектной документации через "; "
    /// Пример: "РД шифр 123 — Архитектурные решения"
    /// </summary>
    [NotMapped]
    public string ProjectDocsDisplay
    {
        get
        {
            if (ActProjectDocs == null || ActProjectDocs.Count == 0)
                return "—";
            return string.Join("; ", ActProjectDocs
                .Where(apd => apd.ProjectDoc != null)
                .Select(apd => $"{apd.ProjectDoc.Code} — {apd.ProjectDoc.Name}"));
        }
    }
}
