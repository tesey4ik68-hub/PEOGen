using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using AGenerator.Database;
using AGenerator.Models;
using AGenerator.Services;

namespace AGenerator.ViewModels;

/// <summary>
/// ViewModel для управления исполнительными схемами текущего объекта.
/// Аналог VBA-листа "т_Схемы".
/// </summary>
public partial class SchemasViewModel : ViewModelBase
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly IFileService _fileService;
    private readonly int _objectId;
    private readonly string _objectName;

    [ObservableProperty]
    private ObservableCollection<Schema> _schemas = new();

    [ObservableProperty]
    private Schema? _selectedSchema;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private Schema _editingSchema = new();

    [ObservableProperty]
    private string _statusMessage = "Загрузка схем...";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _searchText = string.Empty;

    public SchemasViewModel(IDbContextFactory<AppDbContext> contextFactory, IFileService fileService, int objectId, string objectName)
    {
        _contextFactory = contextFactory;
        _fileService = fileService;
        _objectId = objectId;
        _objectName = objectName;
        _ = LoadSchemasAsync();
    }

    private async Task LoadSchemasAsync()
    {
        IsLoading = true;
        StatusMessage = "Загрузка схем...";

        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var list = await context.Schemas
                .Where(s => s.ConstructionObjectId == _objectId)
                .OrderBy(s => s.Number)
                .ThenBy(s => s.Name)
                .ToListAsync();

            Schemas.Clear();
            foreach (var schema in list)
                Schemas.Add(schema);

            StatusMessage = Schemas.Count > 0
                ? $"Схем: {Schemas.Count}"
                : "Нет схем. Нажмите «➕ Добавить» для создания.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка загрузки: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[ERROR] Ошибка загрузки схем: {ex}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadSchemasAsync();
    }

    [RelayCommand]
    private void AddNewSchema()
    {
        EditingSchema = new Schema
        {
            ConstructionObjectId = _objectId,
            Date = DateTime.Now
        };
        IsEditing = true;
    }

    [RelayCommand]
    private void EditSelectedSchema()
    {
        if (SelectedSchema == null) return;

        EditingSchema = new Schema
        {
            Id = SelectedSchema.Id,
            ConstructionObjectId = SelectedSchema.ConstructionObjectId,
            Number = SelectedSchema.Number,
            Name = SelectedSchema.Name,
            Stage = SelectedSchema.Stage,
            Date = SelectedSchema.Date,
            Author = SelectedSchema.Author,
            FilePath = SelectedSchema.FilePath
        };
        IsEditing = true;
    }

    [RelayCommand]
    private async Task SaveSchema()
    {
        if (string.IsNullOrWhiteSpace(EditingSchema.Number))
        {
            MessageBox.Show("Номер схемы не может быть пустым.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            if (EditingSchema.Id == 0)
            {
                context.Schemas.Add(EditingSchema);
            }
            else
            {
                var existing = await context.Schemas.FindAsync(EditingSchema.Id);
                if (existing != null)
                {
                    existing.Number = EditingSchema.Number;
                    existing.Name = EditingSchema.Name;
                    existing.Stage = EditingSchema.Stage;
                    existing.Date = EditingSchema.Date;
                    existing.Author = EditingSchema.Author;
                    existing.FilePath = EditingSchema.FilePath;
                }
            }

            await context.SaveChangesAsync();
            IsEditing = false;
            await LoadSchemasAsync();
            StatusMessage = EditingSchema.Id == 0 ? "Схема добавлена" : "Схема обновлена";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void CancelEdit()
    {
        IsEditing = false;
    }

    [RelayCommand]
    private async Task DeleteSelectedSchema()
    {
        if (SelectedSchema == null) return;

        var result = MessageBox.Show(
            $"Удалить схему \"{SelectedSchema.Number} — {SelectedSchema.Name}\"?\n\n" +
            "Это действие нельзя отменить.",
            "Подтверждение удаления",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var schema = await context.Schemas.FindAsync(SelectedSchema.Id);
            if (schema != null)
            {
                context.Schemas.Remove(schema);
                await context.SaveChangesAsync();
                await LoadSchemasAsync();
                StatusMessage = "Схема удалена";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Ошибка удаления: {ex.Message}",
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ==================== КОМАНДЫ ДЛЯ РАБОТЫ С ФАЙЛАМИ СХЕМ ====================

    /// <summary>
    /// Есть ли файл схемы у редактируемой схемы
    /// </summary>
    public bool HasSchemaFile => !string.IsNullOrEmpty(EditingSchema?.FilePath);

    [RelayCommand]
    private async Task AttachSchemaFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Выберите файл схемы",
            Filter = "Все файлы (*.*)|*.*|PDF (*.pdf)|*.pdf|DWG (*.dwg)|*.dwg|Изображения (*.jpg;*.png)|*.jpg;*.png"
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            await using var stream = File.OpenRead(dialog.FileName);
            var savedPath = _fileService.SaveSchemaFile(
                _objectId,
                _objectName,
                stream,
                EditingSchema.Name,
                EditingSchema.Number,
                EditingSchema.Date,
                Path.GetFileName(dialog.FileName));

            EditingSchema.FilePath = savedPath;
            OnPropertyChanged(nameof(HasSchemaFile));

            // Автосохранение схемы
            await SaveSchema();
            StatusMessage = $"Файл схемы прикреплён: {Path.GetFileName(savedPath)}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка сохранения файла: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task RemoveSchemaFile()
    {
        if (EditingSchema == null) return;

        var result = MessageBox.Show("Удалить файл схемы?\n\nФайл будет удалён с диска.",
            "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            var filePath = EditingSchema.FilePath;
            EditingSchema.FilePath = string.Empty;
            OnPropertyChanged(nameof(HasSchemaFile));

            // Удаляем файл с диска
            if (_fileService.FileExists(filePath))
                File.Delete(filePath);

            await SaveSchema();
            StatusMessage = "Файл схемы удалён";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка удаления файла: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void OpenSchemaFile()
    {
        if (SelectedSchema == null || string.IsNullOrEmpty(SelectedSchema.FilePath)) return;

        try
        {
            _fileService.OpenFile(SelectedSchema.FilePath);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Не удалось открыть файл: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>
    /// Обработка Drop файла схемы (вызывается из code-behind)
    /// </summary>
    public async Task HandleSchemaFileDrop(string[] filePaths)
    {
        if (EditingSchema == null || filePaths.Length == 0) return;

        var sourcePath = filePaths[0];
        if (!File.Exists(sourcePath)) return;

        try
        {
            await using var stream = File.OpenRead(sourcePath);
            var savedPath = _fileService.SaveSchemaFile(
                _objectId,
                _objectName,
                stream,
                EditingSchema.Name,
                EditingSchema.Number,
                EditingSchema.Date,
                Path.GetFileName(sourcePath));

            EditingSchema.FilePath = savedPath;
            OnPropertyChanged(nameof(HasSchemaFile));

            await SaveSchema();
            StatusMessage = $"Файл схемы прикреплён: {Path.GetFileName(savedPath)}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка сохранения файла: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
