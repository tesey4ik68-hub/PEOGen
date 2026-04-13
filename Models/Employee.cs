using System;

namespace AGenerator.Models;

/// <summary>
/// Тип представителя (роль в акте) — аналог VBA "Тип представителя"
/// </summary>
public enum RepresentativeType
{
    SK_Zakazchika,      // СК Заказчика (ТНЗ)
    GenPodryadchik,     // Генподрядчик (Г)
    SK_GenPodryadchika, // СК Генподрядчика (ТНГ)
    Podryadchik,        // Подрядчик (Пд)
    AvtorskiyNadzor,    // Авторский надзор (Пр)
    InoeLico            // Иное лицо
}

/// <summary>
/// Сотрудник (аналог листа "Люди" в Excel)
/// </summary>
public class Employee
{
    public int Id { get; set; }

    public int ConstructionObjectId { get; set; }
    public ConstructionObject? ConstructionObject { get; set; }

    // Основные данные
    public string FullName { get; set; } = string.Empty; // ФИО
    public string Position { get; set; } = string.Empty; // Должность
    public string OrganizationName { get; set; } = string.Empty; // Организация

    // Реквизиты приказа/документа о назначении
    public string OrderNumber { get; set; } = string.Empty; // № приказа
    public DateTime? OrderDate { get; set; } // Дата приказа

    // НРС (Национальный реестр специалистов)
    public string NrsNumber { get; set; } = string.Empty; // Идентификационный номер специалиста в НРС
    public DateTime? NrsDate { get; set; } // Дата НРС

    // Даты работы
    public DateTime? WorkStartDate { get; set; } // Дата начала работы
    public DateTime? WorkEndDate { get; set; } // Дата окончания работы

    // Организация в акте
    public bool IncludeOrganizationInAct { get; set; } // Включить организацию в акт
    public string OrganizationRequisites { get; set; } = string.Empty; // Реквизиты организации

    // Роль в акте (для фильтрации в ComboBox-ах акта)
    public RepresentativeType Role { get; set; }

    public bool IsActive { get; set; } = true;

    // Файл приказа
    public string OrderFilePath { get; set; } = string.Empty;

    /// <summary>
    /// Имя файла приказа (только имя, без пути) — для отображения в UI
    /// </summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string OrderFileName => string.IsNullOrEmpty(OrderFilePath)
        ? string.Empty
        : System.IO.Path.GetFileName(OrderFilePath);
}
