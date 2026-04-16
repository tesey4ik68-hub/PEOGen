using System;
using System.ComponentModel;
using System.Globalization;

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
public class Employee : INotifyPropertyChanged
{
    private int _id;
    private int _constructionObjectId;
    private string _fullName = string.Empty;
    private string _position = string.Empty;
    private string _organizationName = string.Empty;
    private string _orderNumber = string.Empty;
    private DateTime? _orderDate;
    private string _nrsNumber = string.Empty;
    private DateTime? _nrsDate;
    private DateTime? _workStartDate;
    private DateTime? _workEndDate;
    private bool _includeOrganizationInAct;
    private string _organizationRequisites = string.Empty;
    private RepresentativeType _role;
    private bool _isActive = true;
    private string _orderFilePath = string.Empty;

    public int Id
    {
        get => _id;
        set { _id = value; OnPropertyChanged(nameof(Id)); }
    }

    public int ConstructionObjectId
    {
        get => _constructionObjectId;
        set { _constructionObjectId = value; OnPropertyChanged(nameof(ConstructionObjectId)); }
    }

    public ConstructionObject? ConstructionObject { get; set; }

    public string FullName
    {
        get => _fullName;
        set { _fullName = value; OnPropertyChanged(nameof(FullName)); }
    }

    public string Position
    {
        get => _position;
        set { _position = value; OnPropertyChanged(nameof(Position)); }
    }

    public string OrganizationName
    {
        get => _organizationName;
        set { _organizationName = value; OnPropertyChanged(nameof(OrganizationName)); }
    }

    public string OrderNumber
    {
        get => _orderNumber;
        set { _orderNumber = value; OnPropertyChanged(nameof(OrderNumber)); }
    }

    public DateTime? OrderDate
    {
        get => _orderDate;
        set { _orderDate = value; OnPropertyChanged(nameof(OrderDate)); OnPropertyChanged(nameof(OrderDateText)); }
    }

    public string NrsNumber
    {
        get => _nrsNumber;
        set { _nrsNumber = value; OnPropertyChanged(nameof(NrsNumber)); }
    }

    public DateTime? NrsDate
    {
        get => _nrsDate;
        set { _nrsDate = value; OnPropertyChanged(nameof(NrsDate)); OnPropertyChanged(nameof(NrsDateText)); }
    }

    public DateTime? WorkStartDate
    {
        get => _workStartDate;
        set { _workStartDate = value; OnPropertyChanged(nameof(WorkStartDate)); OnPropertyChanged(nameof(WorkStartDateText)); }
    }

    public DateTime? WorkEndDate
    {
        get => _workEndDate;
        set { _workEndDate = value; OnPropertyChanged(nameof(WorkEndDate)); OnPropertyChanged(nameof(WorkEndDateText)); }
    }

    public bool IncludeOrganizationInAct
    {
        get => _includeOrganizationInAct;
        set { _includeOrganizationInAct = value; OnPropertyChanged(nameof(IncludeOrganizationInAct)); }
    }

    public string OrganizationRequisites
    {
        get => _organizationRequisites;
        set { _organizationRequisites = value; OnPropertyChanged(nameof(OrganizationRequisites)); }
    }

    public RepresentativeType Role
    {
        get => _role;
        set { _role = value; OnPropertyChanged(nameof(Role)); }
    }

    public bool IsActive
    {
        get => _isActive;
        set { _isActive = value; OnPropertyChanged(nameof(IsActive)); }
    }

    public string OrderFilePath
    {
        get => _orderFilePath;
        set { _orderFilePath = value; OnPropertyChanged(nameof(OrderFilePath)); }
    }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string OrderFileName => string.IsNullOrEmpty(OrderFilePath)
        ? string.Empty
        : System.IO.Path.GetFileName(OrderFilePath);

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string OrderDateText
    {
        get => OrderDate?.ToString("dd.MM.yyyy") ?? "";
        set
        {
            if (string.IsNullOrWhiteSpace(value)) { OrderDate = null; OnPropertyChanged(nameof(OrderDateText)); return; }
            if (TryParseDate(value, out var dt))
            {
                OrderDate = dt;
                OnPropertyChanged(nameof(OrderDateText));
            }
        }
    }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string NrsDateText
    {
        get => NrsDate?.ToString("dd.MM.yyyy") ?? "";
        set
        {
            if (string.IsNullOrWhiteSpace(value)) { NrsDate = null; OnPropertyChanged(nameof(NrsDateText)); return; }
            if (TryParseDate(value, out var dt))
            {
                NrsDate = dt;
                OnPropertyChanged(nameof(NrsDateText));
            }
        }
    }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string WorkStartDateText
    {
        get => WorkStartDate?.ToString("dd.MM.yyyy") ?? "";
        set
        {
            if (string.IsNullOrWhiteSpace(value)) { WorkStartDate = null; OnPropertyChanged(nameof(WorkStartDateText)); return; }
            if (TryParseDate(value, out var dt))
            {
                WorkStartDate = dt;
                OnPropertyChanged(nameof(WorkStartDateText));
            }
        }
    }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string WorkEndDateText
    {
        get => WorkEndDate?.ToString("dd.MM.yyyy") ?? "";
        set
        {
            if (string.IsNullOrWhiteSpace(value)) { WorkEndDate = null; OnPropertyChanged(nameof(WorkEndDateText)); return; }
            if (TryParseDate(value, out var dt))
            {
                WorkEndDate = dt;
                OnPropertyChanged(nameof(WorkEndDateText));
            }
        }
    }

    private static bool TryParseDate(string text, out DateTime result)
    {
        result = default;
        text = text.Trim();
        if (string.IsNullOrEmpty(text)) return false;

        if (DateTime.TryParseExact(text, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
            return true;
        if (DateTime.TryParseExact(text, "MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
            return true;
        if (int.TryParse(text, out var year) && year >= 1900 && year <= 2100)
        {
            result = new DateTime(year, 1, 1);
            return true;
        }
        if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
            return true;
        return false;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
