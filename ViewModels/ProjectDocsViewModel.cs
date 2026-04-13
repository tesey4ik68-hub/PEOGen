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
/// ViewModel для управления проектной документацией текущего объекта.
/// Аналог VBA-листа "т_Проекты".
/// </summary>
public partial class ProjectDocsViewModel : ViewModelBase
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly IFileService _fileService;
    private readonly int _objectId;
    private readonly string _objectName;

    [ObservableProperty]
    private ObservableCollection<ProjectDoc> _projectDocs = new();

    [ObservableProperty]
    private ProjectDoc? _selectedProjectDoc;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private ProjectDoc _editingProjectDoc = new();

    [ObservableProperty]
    private string _statusMessage = "Загрузка проектной документации...";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _searchText = string.Empty;

    public ProjectDocsViewModel(IDbContextFactory<AppDbContext> contextFactory, IFileService fileService, int objectId, string objectName)
    {
        _contextFactory = contextFactory;
        _fileService = fileService;
        _objectId = objectId;
        _objectName = objectName;
        _ = LoadProjectDocsAsync();
    }

    private async Task LoadProjectDocsAsync()
    {
        IsLoading = true;
        StatusMessage = "Загрузка проектной документации...";

        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var list = await context.ProjectDocs
                .Where(pd => pd.ConstructionObjectId == _objectId)
                .OrderBy(pd => pd.Code)
                .ThenBy(pd => pd.Name)
                .ToListAsync();

            ProjectDocs.Clear();
            foreach (var doc in list)
                ProjectDocs.Add(doc);

            StatusMessage = ProjectDocs.Count > 0
                ? $"Проектных документов: {ProjectDocs.Count}"
                : "Нет проектной документации. Нажмите «➕ Добавить» для создания.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка загрузки: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[ERROR] Ошибка загрузки проектной документации: {ex}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadProjectDocsAsync();
    }

    [RelayCommand]
    private void AddNewProjectDoc()
    {
        EditingProjectDoc = new ProjectDoc
        {
            ConstructionObjectId = _objectId
        };
        IsEditing = true;
    }

    [RelayCommand]
    private void EditSelectedProjectDoc()
    {
        if (SelectedProjectDoc == null) return;

        EditingProjectDoc = new ProjectDoc
        {
            Id = SelectedProjectDoc.Id,
            ConstructionObjectId = SelectedProjectDoc.ConstructionObjectId,
            Code = SelectedProjectDoc.Code,
            Name = SelectedProjectDoc.Name,
            Sheets = SelectedProjectDoc.Sheets,
            Organization = SelectedProjectDoc.Organization,
            GIP = SelectedProjectDoc.GIP,
            FilePath = SelectedProjectDoc.FilePath
        };
        IsEditing = true;
    }

    [RelayCommand]
    private async Task SaveProjectDoc()
    {
        if (string.IsNullOrWhiteSpace(EditingProjectDoc.Code))
        {
            MessageBox.Show("Шифр раздела не может быть пустым.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            if (EditingProjectDoc.Id == 0)
            {
                context.ProjectDocs.Add(EditingProjectDoc);
            }
            else
            {
                var existing = await context.ProjectDocs.FindAsync(EditingProjectDoc.Id);
                if (existing != null)
                {
                    existing.Code = EditingProjectDoc.Code;
                    existing.Name = EditingProjectDoc.Name;
                    existing.Sheets = EditingProjectDoc.Sheets;
                    existing.Organization = EditingProjectDoc.Organization;
                    existing.GIP = EditingProjectDoc.GIP;
                    existing.FilePath = EditingProjectDoc.FilePath;
                }
            }

            await context.SaveChangesAsync();
            IsEditing = false;
            await LoadProjectDocsAsync();
            StatusMessage = EditingProjectDoc.Id == 0 ? "Документ добавлен" : "Документ обновлён";
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
    private async Task DeleteSelectedProjectDoc()
    {
        if (SelectedProjectDoc == null) return;

        var result = MessageBox.Show(
            $"Удалить документ \"{SelectedProjectDoc.Code} — {SelectedProjectDoc.Name}\"?\n\n" +
            "Это действие нельзя отменить.",
            "Подтверждение удаления",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var doc = await context.ProjectDocs.FindAsync(SelectedProjectDoc.Id);
            if (doc != null)
            {
                context.ProjectDocs.Remove(doc);
                await context.SaveChangesAsync();
                await LoadProjectDocsAsync();
                StatusMessage = "Документ удалён";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Ошибка удаления: {ex.Message}",
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ==================== КОМАНДЫ ДЛЯ РАБОТЫ С ФАЙЛАМИ ====================

    /// <summary>
    /// Есть ли файл проекта у редактируемого документа
    /// </summary>
    public bool HasProjectFile => !string.IsNullOrEmpty(EditingProjectDoc?.FilePath);

    [RelayCommand]
    private async Task AttachProjectFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Выберите файл проекта",
            Filter = "Все файлы (*.*)|*.*|PDF (*.pdf)|*.pdf|DOCX (*.docx)|*.docx|DWG (*.dwg)|*.dwg|Изображения (*.jpg;*.png)|*.jpg;*.png"
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            await using var stream = File.OpenRead(dialog.FileName);
            var savedPath = _fileService.SaveProjectFile(
                _objectId,
                _objectName,
                stream,
                EditingProjectDoc.Name,
                EditingProjectDoc.Code,
                Path.GetFileName(dialog.FileName));

            EditingProjectDoc.FilePath = savedPath;
            OnPropertyChanged(nameof(HasProjectFile));

            // Автосохранение документа
            await SaveProjectDoc();
            StatusMessage = $"Файл проекта прикреплён: {Path.GetFileName(savedPath)}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка сохранения файла: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task RemoveProjectFile()
    {
        if (EditingProjectDoc == null) return;

        var result = MessageBox.Show("Удалить файл проекта?\n\nФайл будет удалён с диска.",
            "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            var filePath = EditingProjectDoc.FilePath;
            EditingProjectDoc.FilePath = string.Empty;
            OnPropertyChanged(nameof(HasProjectFile));

            // Удаляем файл с диска
            if (_fileService.FileExists(filePath))
                File.Delete(filePath);

            await SaveProjectDoc();
            StatusMessage = "Файл проекта удалён";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка удаления файла: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void OpenProjectFile()
    {
        if (SelectedProjectDoc == null || string.IsNullOrEmpty(SelectedProjectDoc.FilePath)) return;

        try
        {
            _fileService.OpenFile(SelectedProjectDoc.FilePath);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Не удалось открыть файл: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>
    /// Обработка Drop файла проекта (вызывается из code-behind)
    /// </summary>
    public async Task HandleProjectFileDrop(string[] filePaths)
    {
        if (EditingProjectDoc == null || filePaths.Length == 0) return;

        var sourcePath = filePaths[0];
        if (!File.Exists(sourcePath)) return;

        try
        {
            await using var stream = File.OpenRead(sourcePath);
            var savedPath = _fileService.SaveProjectFile(
                _objectId,
                _objectName,
                stream,
                EditingProjectDoc.Name,
                EditingProjectDoc.Code,
                Path.GetFileName(sourcePath));

            EditingProjectDoc.FilePath = savedPath;
            OnPropertyChanged(nameof(HasProjectFile));

            await SaveProjectDoc();
            StatusMessage = $"Файл проекта прикреплён: {Path.GetFileName(savedPath)}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка сохранения файла: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
