using System;
using System.Collections.Generic;
using System.Linq;
using AGenerator.Models;

namespace AGenerator.Services;

/// <summary>
/// Сервис для автоматического расчёта вычисляемых полей акта.
/// Аналог VBA-логики из функции ВосстановитьФормулы (столбцы "Автоназвание работ" и "Итоговый номер акта").
/// </summary>
public class ActCalculationService
{
    /// <summary>
    /// Пересчитать все вычисляемые поля акта.
    /// Вызывать при каждом изменении зависимых свойств.
    /// </summary>
    public static void RecalculateAll(Act act)
    {
        if (act == null) return;

        CalculateAutoWorkName(act);
        CalculateFinalActNumber(act);
    }

    /// <summary>
    /// Расчёт автоназвания работ.
    /// Формула: [Наименование работ] + " " + [Тип интервала] + " " + [Интервал]
    /// Пример: "Бетонирование" + "в камере" + "К1" = "Бетонирование в камере К1"
    /// </summary>
    private static void CalculateAutoWorkName(Act act)
    {
        var parts = new List<string>();

        // 1. Наименование работ
        if (!string.IsNullOrWhiteSpace(act.WorkName))
            parts.Add(act.WorkName.Trim());

        // 2. Тип интервала
        if (!string.IsNullOrWhiteSpace(act.IntervalType))
            parts.Add(act.IntervalType.Trim());

        // 3. Интервал
        if (!string.IsNullOrWhiteSpace(act.Interval))
            parts.Add(act.Interval.Trim());

        act.AutoWorkName = string.Join(" ", parts);
    }

    /// <summary>
    /// Расчёт итогового номера акта по настраиваемой маске (аналог VBA GetActNumber, мс1..мс12).
    /// Загружает маску из настроек и собирает итоговый номер последовательно по сегментам.
    /// Пустые сегменты пропускаются. При ошибке конфигурации — fallback: "Тип Номер".
    /// </summary>
    private static void CalculateFinalActNumber(Act act)
    {
        try
        {
            var maskService = new DocumentSettingsService();
            var mask = maskService.LoadActNumberMaskSettings();
            var result = BuildActNumberFromMask(act, mask);

            // Fallback, если результат пустой
            act.FinalActNumber = string.IsNullOrWhiteSpace(result)
                ? BuildFallbackActNumber(act)
                : result;
        }
        catch
        {
            // При любой ошибке — безопасный fallback
            act.FinalActNumber = BuildFallbackActNumber(act);
        }
    }

    /// <summary>
    /// Собрать номер акта по маске из 12 сегментов.
    /// Нечётные сегменты — статический текст, чётные — поля акта.
    /// Пустые сегменты пропускаются.
    /// </summary>
    private static string BuildActNumberFromMask(Act act, ActNumberMaskSettings mask)
    {
        var segments = new List<string>();

        // Сегмент 1 (текст)
        AddSegmentIfNotEmpty(segments, mask.Segment1Text);
        // Сегмент 2 (поле)
        AddSegmentIfNotEmpty(segments, GetActFieldValue(act, mask.Segment2Field));
        // Сегмент 3 (текст)
        AddSegmentIfNotEmpty(segments, mask.Segment3Text);
        // Сегмент 4 (поле)
        AddSegmentIfNotEmpty(segments, GetActFieldValue(act, mask.Segment4Field));
        // Сегмент 5 (текст)
        AddSegmentIfNotEmpty(segments, mask.Segment5Text);
        // Сегмент 6 (поле)
        AddSegmentIfNotEmpty(segments, GetActFieldValue(act, mask.Segment6Field));
        // Сегмент 7 (текст)
        AddSegmentIfNotEmpty(segments, mask.Segment7Text);
        // Сегмент 8 (поле)
        AddSegmentIfNotEmpty(segments, GetActFieldValue(act, mask.Segment8Field));
        // Сегмент 9 (текст)
        AddSegmentIfNotEmpty(segments, mask.Segment9Text);
        // Сегмент 10 (поле)
        AddSegmentIfNotEmpty(segments, GetActFieldValue(act, mask.Segment10Field));
        // Сегмент 11 (текст)
        AddSegmentIfNotEmpty(segments, mask.Segment11Text);
        // Сегмент 12 (поле)
        AddSegmentIfNotEmpty(segments, GetActFieldValue(act, mask.Segment12Field));

        var result = string.Join("", segments);

        // Убираем двойные пробелы и чистим результат
        return NormalizeSpaces(result);
    }

    /// <summary>
    /// Получить значение поля акта по имени.
    /// Поддерживает: Type, ActNumber, ActDate, ActDateMonth, ActDateDay, WorkName,
    /// Interval, IntervalType, Level1-3, Mark, InAxes, Volume, UnitOfMeasure.
    /// </summary>
    private static string GetActFieldValue(Act act, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
            return "";

        return fieldName.Trim() switch
        {
            "Type" => act.Type ?? "",
            "ActNumber" => act.ActNumber ?? "",
            "ActDate" => act.ActDate.Year.ToString(),
            "ActDateMonth" => GetMonthName(act.ActDate.Month),
            "ActDateDay" => act.ActDate.Day.ToString("D2"),
            "WorkName" => act.WorkName ?? "",
            "Interval" => act.Interval ?? "",
            "IntervalType" => act.IntervalType ?? "",
            "Level1" => act.Level1 ?? "",
            "Level2" => act.Level2 ?? "",
            "Level3" => act.Level3 ?? "",
            "Mark" => act.Mark ?? "",
            "InAxes" => act.InAxes ?? "",
            "Volume" => act.Volume ?? "",
            "UnitOfMeasure" => act.UnitOfMeasure ?? "",
            _ => ""
        };
    }

    /// <summary>
    /// Безопасный fallback — повторяет текущее поведение: "Тип Номер".
    /// </summary>
    private static string BuildFallbackActNumber(Act act)
    {
        var typePrefix = string.IsNullOrWhiteSpace(act.Type) ? "" : act.Type.Trim();
        var number = string.IsNullOrWhiteSpace(act.ActNumber) ? "№?" : act.ActNumber.Trim();

        return string.IsNullOrWhiteSpace(typePrefix)
            ? number
            : $"{typePrefix} {number}";
    }

    /// <summary>
    /// Добавить сегмент в результат, если он не пустой.
    /// </summary>
    private static void AddSegmentIfNotEmpty(List<string> segments, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            segments.Add(value.Trim());
    }

    /// <summary>
    /// Убрать двойные (и более) пробелы, склеить результат.
    /// </summary>
    private static string NormalizeSpaces(string input)
    {
        var parts = input.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", parts);
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
    /// Получить отображаемый тип интервала для подстановки в автоназвание.
    /// </summary>
    public static string GetIntervalTypeText(string intervalType)
    {
        return string.IsNullOrWhiteSpace(intervalType) ? "на интервале" : intervalType;
    }
}
