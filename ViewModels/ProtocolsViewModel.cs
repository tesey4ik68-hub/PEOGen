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
/// ViewModel для управления протоколами испытаний текущего объекта.
/// Аналог VBA-листа "т_Протоколы".
/// </summary>
public partial class ProtocolsViewModel : ViewModelBase
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly IFileService _fileService;
    private readonly int _objectId;
    private readonly string _objectName;

    [ObservableProperty]
    private ObservableCollection<Protocol> _protocols = new();

    [ObservableProperty]
    private Protocol? _selectedProtocol;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private Protocol _editingProtocol = new();

    [ObservableProperty]
    private string _statusMessage = "Загрузка протоколов...";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _searchText = string.Empty;

    public ProtocolsViewModel(IDbContextFactory<AppDbContext> contextFactory, IFileService fileService, int objectId, string objectName)
    {
        _contextFactory = contextFactory;
        _fileService = fileService;
        _objectId = objectId;
        _objectName = objectName;
        _ = LoadProtocolsAsync();
    }

    private async Task LoadProtocolsAsync()
    {
        IsLoading = true;
        StatusMessage = "Загрузка протоколов...";

        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var list = await context.Protocols
                .Where(p => p.ConstructionObjectId == _objectId)
                .OrderByDescending(p => p.Date)
                .ToListAsync();

            Protocols.Clear();
            foreach (var protocol in list)
                Protocols.Add(protocol);

            StatusMessage = Protocols.Count > 0
                ? $"Протоколов: {Protocols.Count}"
                : "Нет протоколов. Нажмите «➕ Добавить» для создания.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка загрузки: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[ERROR] Ошибка загрузки протоколов: {ex}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadProtocolsAsync();
    }

    [RelayCommand]
    private void AddNewProtocol()
    {
        EditingProtocol = new Protocol
        {
            ConstructionObjectId = _objectId,
            Date = DateTime.Now,
            CreatedAt = DateTime.Now
        };
        IsEditing = true;
    }

    [RelayCommand]
    private void EditSelectedProtocol()
    {
        if (SelectedProtocol == null) return;

        EditingProtocol = new Protocol
        {
            Id = SelectedProtocol.Id,
            ConstructionObjectId = SelectedProtocol.ConstructionObjectId,
            Number = SelectedProtocol.Number,
            Name = SelectedProtocol.Name,
            Type = SelectedProtocol.Type,
            DocumentType = SelectedProtocol.DocumentType,
            Date = SelectedProtocol.Date,
            Laboratory = SelectedProtocol.Laboratory,
            Result = SelectedProtocol.Result,
            FilePath = SelectedProtocol.FilePath
        };
        IsEditing = true;
    }

    [RelayCommand]
    private async Task SaveProtocol()
    {
        if (string.IsNullOrWhiteSpace(EditingProtocol.Number))
        {
            MessageBox.Show("Номер протокола не может быть пустым.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            if (EditingProtocol.Id == 0)
            {
                context.Protocols.Add(EditingProtocol);
            }
            else
            {
                var existing = await context.Protocols.FindAsync(EditingProtocol.Id);
                if (existing != null)
                {
                    existing.Number = EditingProtocol.Number;
                    existing.Name = EditingProtocol.Name;
                    existing.Type = EditingProtocol.Type;
                    existing.DocumentType = EditingProtocol.DocumentType;
                    existing.Date = EditingProtocol.Date;
                    existing.Laboratory = EditingProtocol.Laboratory;
                    existing.Result = EditingProtocol.Result;
                    existing.FilePath = EditingProtocol.FilePath;
                }
            }

            await context.SaveChangesAsync();
            IsEditing = false;
            await LoadProtocolsAsync();
            StatusMessage = EditingProtocol.Id == 0 ? "Протокол добавлен" : "Протокол обновлён";
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
    private async Task DeleteSelectedProtocol()
    {
        if (SelectedProtocol == null) return;

        var result = MessageBox.Show(
            $"Удалить протокол №{SelectedProtocol.Number}?\n\n" +
            "Это действие нельзя отменить.",
            "Подтверждение удаления",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var protocol = await context.Protocols.FindAsync(SelectedProtocol.Id);
            if (protocol != null)
            {
                context.Protocols.Remove(protocol);
                await context.SaveChangesAsync();
                await LoadProtocolsAsync();
                StatusMessage = "Протокол удалён";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Ошибка удаления: {ex.Message}",
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ==================== КОМАНДЫ ДЛЯ РАБОТЫ С ФАЙЛАМИ ПРОТОКОЛОВ ====================

    /// <summary>
    /// Есть ли файл протокола у редактируемого протокола
    /// </summary>
    public bool HasProtocolFile => !string.IsNullOrEmpty(EditingProtocol?.FilePath);

    [RelayCommand]
    private async Task AttachProtocolFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Выберите файл протокола",
            Filter = "Все файлы (*.*)|*.*|PDF (*.pdf)|*.pdf|DOCX (*.docx)|*.docx|Изображения (*.jpg;*.jpeg;*.png;*.tiff)|*.jpg;*.jpeg;*.png;*.tiff"
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            await using var stream = File.OpenRead(dialog.FileName);
            var savedPath = _fileService.SaveProtocolFile(
                _objectId,
                _objectName,
                stream,
                EditingProtocol.DocumentTypeDisplay,
                EditingProtocol.Number,
                EditingProtocol.Date,
                Path.GetFileName(dialog.FileName));

            EditingProtocol.FilePath = savedPath;
            OnPropertyChanged(nameof(HasProtocolFile));

            // Автосохранение протокола
            await SaveProtocol();
            StatusMessage = $"Файл протокола прикреплён: {Path.GetFileName(savedPath)}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка сохранения файла: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task RemoveProtocolFile()
    {
        if (EditingProtocol == null) return;

        var result = MessageBox.Show("Удалить файл протокола?\n\nФайл будет удалён с диска.",
            "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            var filePath = EditingProtocol.FilePath;
            EditingProtocol.FilePath = string.Empty;
            OnPropertyChanged(nameof(HasProtocolFile));

            // Удаляем файл с диска
            if (_fileService.FileExists(filePath))
                File.Delete(filePath);

            await SaveProtocol();
            StatusMessage = "Файл протокола удалён";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка удаления файла: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void OpenProtocolFile()
    {
        if (SelectedProtocol == null || string.IsNullOrEmpty(SelectedProtocol.FilePath)) return;

        try
        {
            _fileService.OpenFile(SelectedProtocol.FilePath);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Не удалось открыть файл: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>
    /// Обработка Drop файла протокола (вызывается из code-behind)
    /// </summary>
    public async Task HandleProtocolFileDrop(string[] filePaths)
    {
        if (EditingProtocol == null || filePaths.Length == 0) return;

        var sourcePath = filePaths[0];
        if (!File.Exists(sourcePath)) return;

        try
        {
            await using var stream = File.OpenRead(sourcePath);
            var savedPath = _fileService.SaveProtocolFile(
                _objectId,
                _objectName,
                stream,
                EditingProtocol.DocumentTypeDisplay,
                EditingProtocol.Number,
                EditingProtocol.Date,
                Path.GetFileName(sourcePath));

            EditingProtocol.FilePath = savedPath;
            OnPropertyChanged(nameof(HasProtocolFile));

            await SaveProtocol();
            StatusMessage = $"Файл протокола прикреплён: {Path.GetFileName(savedPath)}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка сохранения файла: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
