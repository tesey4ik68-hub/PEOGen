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
    /// Расчёт итогового номера акта.
    /// Упрощённая версия VBA-маски (мс1..мс13).
    /// Формат: "Тип Номер" (например: "АОСР 123")
    /// В будущем здесь будет полная логика с настраиваемой маской из настроек.
    /// </summary>
    private static void CalculateFinalActNumber(Act act)
    {
        var typePrefix = string.IsNullOrWhiteSpace(act.Type) ? "" : act.Type.Trim();
        var number = string.IsNullOrWhiteSpace(act.ActNumber) ? "№?" : act.ActNumber.Trim();

        act.FinalActNumber = string.IsNullOrWhiteSpace(typePrefix)
            ? number
            : $"{typePrefix} {number}";
    }

    /// <summary>
    /// Получить отображаемый тип интервала для подстановки в автоназвание.
    /// </summary>
    public static string GetIntervalTypeText(string intervalType)
    {
        return string.IsNullOrWhiteSpace(intervalType) ? "на интервале" : intervalType;
    }
}
