using System;
using System.Linq;

namespace AGenerator.Services;

/// <summary>
/// Утилита для склонения русских ФИО в родительный падеж.
/// Реализует базовые правила — для MVP покрывает ~80% случаев.
/// </summary>
public static class RussianNameDeclension
{
    /// <summary>
    /// Склоняет ФИО в родительный падеж.
    /// Формат: "Иванов Иван Иванович" → "Иванова Ивана Ивановича"
    /// </summary>
    public static string ToGenitiveCase(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            return fullName;

        var parts = fullName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        var declined = parts.Select((part, index) => DeclineWord(part, index)).ToArray();
        return string.Join(" ", declined);
    }

    private static string DeclineWord(string word, int position)
    {
        if (string.IsNullOrEmpty(word))
            return word;

        var lower = word.ToLowerInvariant();

        // Фамилии
        if (position == 0)
            return DeclineSurname(word);

        // Имя
        if (position == 1)
            return DeclineFirstName(word);

        // Отчество
        if (position == 2)
            return DeclinePatronymic(word);

        // Остальное — не склоняем
        return word;
    }

    private static string DeclineSurname(string word)
    {
        var lower = word.ToLowerInvariant();

        // -ов, -ев, -ин, -ын → -ова/-ева/-ина/-ына (род. падеж совпадает с им. для мужских, но для род. падежа: -ов → -ова)
        // Для родительного падежа мужских фамилий:
        // Иванов → Иванова, Петров → Петрова
        if (lower.EndsWith("ов") || lower.EndsWith("ев") || lower.EndsWith("ёв"))
            return word + "а";
        if (lower.EndsWith("ин") || lower.EndsWith("ын"))
            return word + "а";

        // -ий, -ой → -его (для прилагательных)
        if (lower.EndsWith("ий"))
            return word.Substring(0, word.Length - 2) + "его";
        if (lower.EndsWith("ой"))
            return word.Substring(0, word.Length - 2) + "ого";

        // -ов/-ев для женских (уже оканчивается на а) — не склоняем или -ой
        if (lower.EndsWith("ова") || lower.EndsWith("ева"))
            return word.Substring(0, word.Length - 1) + "ой";

        // Согласная на конце (мужские) — добавляем -а
        if (!lower.EndsWith("а") && !lower.EndsWith("я") && !lower.EndsWith("е") && !lower.EndsWith("о"))
            return word + "а";

        // -а → -ы/-и
        if (lower.EndsWith("а"))
        {
            var stem = word.Substring(0, word.Length - 1);
            var lastChar = stem.Length > 0 ? stem[^1] : ' ';
            if ("жчшщ".Contains(char.ToLowerInvariant(lastChar)))
                return stem + "и";
            return stem + "ы";
        }

        if (lower.EndsWith("я"))
            return word.Substring(0, word.Length - 1) + "и";

        return word;
    }

    private static string DeclineFirstName(string word)
    {
        var lower = word.ToLowerInvariant();

        // -й (Андрей, Сергей) → -я
        if (lower.EndsWith("й"))
            return word.Substring(0, word.Length - 1) + "я";

        // -а (Никита, Лука) → -ы
        if (lower.EndsWith("а"))
            return word.Substring(0, word.Length - 1) + "ы";

        // Согласная на конце (мужские имена) → -а
        if (!lower.EndsWith("а") && !lower.EndsWith("я") && !lower.EndsWith("е") && !lower.EndsWith("о") && !lower.EndsWith("и"))
            return word + "а";

        if (lower.EndsWith("я"))
            return word.Substring(0, word.Length - 1) + "и";

        if (lower.EndsWith("ь"))
            return word.Substring(0, word.Length - 1) + "я";

        return word;
    }

    private static string DeclinePatronymic(string word)
    {
        var lower = word.ToLowerInvariant();

        // -ич → -ича
        if (lower.EndsWith("ич"))
            return word + "а";

        // -на → -ны
        if (lower.EndsWith("на"))
            return word.Substring(0, word.Length - 1) + "ы";

        // Согласная → -а
        if (!lower.EndsWith("а") && !lower.EndsWith("я") && !lower.EndsWith("е") && !lower.EndsWith("о"))
            return word + "а";

        return word;
    }
}
