using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AGenerator.Models;

/// <summary>
/// Модель настройки маски итогового номера акта.
/// Аналог VBA-функции GetActNumber (мс1..мс12).
/// Нечётные сегменты — статический текст, чётные — поля акта.
/// </summary>
public partial class ActNumberMaskSettings : ObservableObject
{
    // Сегмент 1 — статический текст
    [ObservableProperty] private string _segment1Text = "";
    // Сегмент 2 — поле акта
    [ObservableProperty] private string _segment2Field = "Type";
    // Сегмент 3 — статический текст
    [ObservableProperty] private string _segment3Text = " ";
    // Сегмент 4 — поле акта
    [ObservableProperty] private string _segment4Field = "ActNumber";
    // Сегмент 5 — статический текст
    [ObservableProperty] private string _segment5Text = "";
    // Сегмент 6 — поле акта
    [ObservableProperty] private string _segment6Field = "";
    // Сегмент 7 — статический текст
    [ObservableProperty] private string _segment7Text = "";
    // Сегмент 8 — поле акта
    [ObservableProperty] private string _segment8Field = "";
    // Сегмент 9 — статический текст
    [ObservableProperty] private string _segment9Text = "";
    // Сегмент 10 — поле акта
    [ObservableProperty] private string _segment10Field = "";
    // Сегмент 11 — статический текст
    [ObservableProperty] private string _segment11Text = "";
    // Сегмент 12 — поле акта
    [ObservableProperty] private string _segment12Field = "";

    /// <summary>
    /// Создать настройки маски по умолчанию (повторяет текущее поведение: "Тип Номер").
    /// </summary>
    public static ActNumberMaskSettings CreateDefault()
    {
        return new ActNumberMaskSettings
        {
            Segment1Text = "",
            Segment2Field = "Type",
            Segment3Text = " ",
            Segment4Field = "ActNumber",
            Segment5Text = "",
            Segment6Field = "",
            Segment7Text = "",
            Segment8Field = "",
            Segment9Text = "",
            Segment10Field = "",
            Segment11Text = "",
            Segment12Field = ""
        };
    }

    /// <summary>
    /// Список допустимых полей акта для использования в маске.
    /// </summary>
    public static readonly IReadOnlyList<ActFieldDescriptor> AvailableFields = new List<ActFieldDescriptor>
    {
        new ActFieldDescriptor("", "— не выбрано —"),
        new ActFieldDescriptor("Type", "Тип акта"),
        new ActFieldDescriptor("ActNumber", "Номер акта"),
        new ActFieldDescriptor("ActDate", "Дата акта (yyyy)"),
        new ActFieldDescriptor("ActDateMonth", "Дата акта (месяц)"),
        new ActFieldDescriptor("ActDateDay", "Дата акта (день)"),
        new ActFieldDescriptor("WorkName", "Наименование работ"),
        new ActFieldDescriptor("Interval", "Интервал"),
        new ActFieldDescriptor("IntervalType", "Тип интервала"),
        new ActFieldDescriptor("Level1", "Уровень 1"),
        new ActFieldDescriptor("Level2", "Уровень 2"),
        new ActFieldDescriptor("Level3", "Уровень 3"),
        new ActFieldDescriptor("Mark", "Отметка"),
        new ActFieldDescriptor("InAxes", "В осях"),
        new ActFieldDescriptor("Volume", "Объём"),
        new ActFieldDescriptor("UnitOfMeasure", "Ед.изм."),
    };
}

/// <summary>
/// Описание поля акта для использования в маске номера.
/// </summary>
public class ActFieldDescriptor
{
    public string Value { get; }
    public string DisplayName { get; }

    public ActFieldDescriptor(string value, string displayName)
    {
        Value = value;
        DisplayName = displayName;
    }
}
