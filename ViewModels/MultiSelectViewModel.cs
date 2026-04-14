using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AGenerator.ViewModels;

/// <summary>
/// Обёртка элемента с отображаемым текстом для ListBox
/// </summary>
public class DisplayItem<T> where T : class
{
    public T Item { get; }
    public string DisplayText { get; }

    public DisplayItem(T item, string displayText)
    {
        Item = item;
        DisplayText = displayText;
    }

    public override string ToString() => DisplayText;
}

/// <summary>
/// Универсальный ViewModel для окна множественного выбора элементов.
/// Используется для выбора материалов, схем, протоколов и т.д.
/// </summary>
public partial class MultiSelectViewModel<T> : ObservableObject where T : class
{
    private readonly Func<T, string> _displaySelector;
    private readonly List<T> _allItems;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _windowTitle = "Выбор элементов";

    /// <summary>
    /// Доступные элементы (левый список) — фильтруется по SearchText
    /// Содержит DisplayItem&lt;T&gt; для корректного отображения
    /// </summary>
    public ObservableCollection<DisplayItem<T>> AvailableItems { get; } = new();

    /// <summary>
    /// Выбранные элементы (правый список)
    /// Содержит DisplayItem&lt;T&gt; для корректного отображения
    /// </summary>
    public ObservableCollection<DisplayItem<T>> SelectedItems { get; } = new();

    public MultiSelectViewModel(
        IEnumerable<T> allItems,
        IEnumerable<T> initiallySelected,
        Func<T, string> displaySelector)
    {
        _displaySelector = displaySelector;
        _allItems = allItems.ToList();

        // Заполняем доступные элементы обёртками
        foreach (var item in _allItems)
            AvailableItems.Add(new DisplayItem<T>(item, _displaySelector(item)));

        // Предвыбранные элементы
        var selectedSet = new HashSet<T>(initiallySelected);
        foreach (var item in _allItems)
        {
            if (selectedSet.Contains(item))
                SelectedItems.Add(new DisplayItem<T>(item, _displaySelector(item)));
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        RefreshAvailableItems();
    }

    private void RefreshAvailableItems()
    {
        AvailableItems.Clear();

        var filtered = string.IsNullOrWhiteSpace(SearchText)
            ? _allItems
            : _allItems.Where(i => _displaySelector(i)
                .Contains(SearchText, StringComparison.OrdinalIgnoreCase));

        foreach (var item in filtered)
            AvailableItems.Add(new DisplayItem<T>(item, _displaySelector(item)));
    }

    // ==================== КОМАНДЫ ====================

    [RelayCommand]
    private void AddSelected(object? parameter)
    {
        if (parameter is not IList selected) return;

        foreach (var displayItem in selected.Cast<DisplayItem<T>>().ToList())
        {
            if (!SelectedItems.Contains(displayItem))
                SelectedItems.Add(displayItem);
        }
    }

    [RelayCommand]
    private void RemoveSelected(object? parameter)
    {
        if (parameter is not IList selected) return;

        foreach (var displayItem in selected.Cast<DisplayItem<T>>().ToList())
            SelectedItems.Remove(displayItem);
    }

    [RelayCommand]
    private void AddAll()
    {
        foreach (var displayItem in AvailableItems)
        {
            if (!SelectedItems.Contains(displayItem))
                SelectedItems.Add(displayItem);
        }
    }

    [RelayCommand]
    private void RemoveAll()
    {
        SelectedItems.Clear();
    }

    /// <summary>
    /// Возвращает список выбранных элементов (без обёртки DisplayItem)
    /// </summary>
    public IList SelectedItemsResult => SelectedItems.Select(d => d.Item).ToList();
}
