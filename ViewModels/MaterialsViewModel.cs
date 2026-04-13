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
/// ViewModel для управления материалами текущего объекта строительства.
/// </summary>
public partial class MaterialsViewModel : ViewModelBase
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly IFileService _fileService;
    private readonly int _objectId;
    private readonly string _objectName;

    [ObservableProperty]
    private ObservableCollection<Material> _materials = new();

    [ObservableProperty]
    private Material? _selectedMaterial;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private Material _editingMaterial = new();

    /// <summary>
    /// Временный путь к файлу сертификата
    /// </summary>
    private string? _tempCertFilePath;

    [ObservableProperty]
    private string _statusMessage = "Загрузка материалов...";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _searchText = string.Empty;

    public MaterialsViewModel(IDbContextFactory<AppDbContext> contextFactory, IFileService fileService, int objectId, string objectName)
    {
        _contextFactory = contextFactory;
        _fileService = fileService;
        _objectId = objectId;
        _objectName = objectName;
        _ = LoadMaterialsAsync();
    }

    private async Task LoadMaterialsAsync()
    {
        IsLoading = true;
        StatusMessage = "Загрузка материалов...";

        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var list = await context.Materials
                .Where(m => m.ConstructionObjectId == _objectId)
                .OrderBy(m => m.Type)
                .ThenBy(m => m.Name)
                .ToListAsync();

            // Проверяем существование файлов
            foreach (var mat in list)
            {
                if (!string.IsNullOrEmpty(mat.CertificateFilePath) && !_fileService.FileExists(mat.CertificateFilePath))
                {
                    mat.CertificateFilePath = string.Empty;
                    context.Entry(mat).Property(e => e.CertificateFilePath).IsModified = true;
                }
            }
            await context.SaveChangesAsync();

            Materials.Clear();
            foreach (var mat in list)
                Materials.Add(mat);

            StatusMessage = Materials.Count > 0
                ? $"Материалов: {Materials.Count}"
                : "Нет материалов. Нажмите «➕ Добавить» для создания.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка загрузки: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[ERROR] Ошибка загрузки материалов: {ex}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadMaterialsAsync();
    }

    [RelayCommand]
    private void AddNewMaterial()
    {
        _tempCertFilePath = null;
        EditingMaterial = new Material
        {
            ConstructionObjectId = _objectId
        };
        OnPropertyChanged(nameof(HasTempCertFile));
        OnPropertyChanged(nameof(HasSavedCertFile));
        OnPropertyChanged(nameof(CertFileDisplayName));
        IsEditing = true;
    }

    [RelayCommand]
    private void EditSelectedMaterial()
    {
        if (SelectedMaterial == null) return;

        _tempCertFilePath = null;
        EditingMaterial = new Material
        {
            Id = SelectedMaterial.Id,
            ConstructionObjectId = SelectedMaterial.ConstructionObjectId,
            Name = SelectedMaterial.Name,
            Type = SelectedMaterial.Type,
            DocumentType = SelectedMaterial.DocumentType,
            Unit = SelectedMaterial.Unit,
            Quantity = SelectedMaterial.Quantity,
            GostNumber = SelectedMaterial.GostNumber,
            CertificateNumber = SelectedMaterial.CertificateNumber,
            CertificateDateText = SelectedMaterial.CertificateDateText,
            Manufacturer = SelectedMaterial.Manufacturer,
            Supplier = SelectedMaterial.Supplier,
            DeliveryDate = SelectedMaterial.DeliveryDate,
            CertificateFilePath = SelectedMaterial.CertificateFilePath
        };
        OnPropertyChanged(nameof(HasTempCertFile));
        OnPropertyChanged(nameof(HasSavedCertFile));
        OnPropertyChanged(nameof(CertFileDisplayName));
        IsEditing = true;
    }

    /// <summary>
    /// Копировать материал — открывает форму как новая запись
    /// </summary>
    [RelayCommand]
    private void CopyMaterial()
    {
        if (SelectedMaterial == null) return;

        _tempCertFilePath = null;
        EditingMaterial = new Material
        {
            ConstructionObjectId = _objectId,
            Name = SelectedMaterial.Name,
            Type = SelectedMaterial.Type,
            DocumentType = SelectedMaterial.DocumentType,
            Unit = SelectedMaterial.Unit,
            Quantity = SelectedMaterial.Quantity,
            GostNumber = SelectedMaterial.GostNumber,
            CertificateNumber = SelectedMaterial.CertificateNumber,
            CertificateDateText = SelectedMaterial.CertificateDateText,
            Manufacturer = SelectedMaterial.Manufacturer,
            Supplier = SelectedMaterial.Supplier,
            DeliveryDate = SelectedMaterial.DeliveryDate,
            // CertificateFilePath НЕ копируем
        };
        OnPropertyChanged(nameof(HasTempCertFile));
        OnPropertyChanged(nameof(HasSavedCertFile));
        OnPropertyChanged(nameof(CertFileDisplayName));
        IsEditing = true;
    }

    [RelayCommand]
    private async Task SaveMaterial()
    {
        if (string.IsNullOrWhiteSpace(EditingMaterial.Name))
        {
            MessageBox.Show("Название материала не может быть пустым.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(EditingMaterial.Type))
        {
            MessageBox.Show("Тип материала не может быть пустым.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            if (EditingMaterial.Id == 0)
            {
                // Новый материал — сохраняем файл
                if (!string.IsNullOrEmpty(_tempCertFilePath) && File.Exists(_tempCertFilePath))
                {
                    using (var sourceStream = File.OpenRead(_tempCertFilePath))
                    {
                        EditingMaterial.CertificateFilePath = _fileService.SaveMaterialCert(
                            _objectId,
                            _objectName,
                            EditingMaterial.Type,
                            sourceStream,
                            EditingMaterial.Name,
                            EditingMaterial.DocumentTypeDisplay,
                            EditingMaterial.CertificateNumber,
                            EditingMaterial.CertificateDateText,
                            Path.GetFileName(_tempCertFilePath));
                    }
                    File.Delete(_tempCertFilePath);
                }
                _tempCertFilePath = null;

                context.Materials.Add(EditingMaterial);
            }
            else
            {
                var existing = await context.Materials.FindAsync(EditingMaterial.Id);
                if (existing != null)
                {
                    if (!string.IsNullOrEmpty(_tempCertFilePath) && File.Exists(_tempCertFilePath))
                    {
                        if (!string.IsNullOrEmpty(existing.CertificateFilePath) && _fileService.FileExists(existing.CertificateFilePath))
                            File.Delete(existing.CertificateFilePath);

                        using (var sourceStream = File.OpenRead(_tempCertFilePath))
                        {
                            existing.CertificateFilePath = _fileService.SaveMaterialCert(
                                _objectId,
                                _objectName,
                                EditingMaterial.Type,
                                sourceStream,
                                EditingMaterial.Name,
                                EditingMaterial.DocumentTypeDisplay,
                                EditingMaterial.CertificateNumber,
                                EditingMaterial.CertificateDateText,
                                Path.GetFileName(_tempCertFilePath));
                        }
                        File.Delete(_tempCertFilePath);
                        _tempCertFilePath = null;
                    }

                    existing.Name = EditingMaterial.Name;
                    existing.Type = EditingMaterial.Type;
                    existing.DocumentType = EditingMaterial.DocumentType;
                    existing.Unit = EditingMaterial.Unit;
                    existing.Quantity = EditingMaterial.Quantity;
                    existing.GostNumber = EditingMaterial.GostNumber;
                    existing.CertificateNumber = EditingMaterial.CertificateNumber;
                    existing.CertificateDateText = EditingMaterial.CertificateDateText;
                    existing.Manufacturer = EditingMaterial.Manufacturer;
                    existing.Supplier = EditingMaterial.Supplier;
                    existing.DeliveryDate = EditingMaterial.DeliveryDate;
                }
            }

            await context.SaveChangesAsync();
            IsEditing = false;
            _tempCertFilePath = null;
            OnPropertyChanged(nameof(HasTempCertFile));
            OnPropertyChanged(nameof(HasSavedCertFile));
            OnPropertyChanged(nameof(CertFileDisplayName));
            await LoadMaterialsAsync();
            StatusMessage = EditingMaterial.Id == 0 ? "Материал добавлен" : "Материал обновлён";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void CancelEdit()
    {
        if (!string.IsNullOrEmpty(_tempCertFilePath) && File.Exists(_tempCertFilePath))
            try { File.Delete(_tempCertFilePath); } catch { }
        _tempCertFilePath = null;
        OnPropertyChanged(nameof(HasTempCertFile));
        OnPropertyChanged(nameof(HasSavedCertFile));
        OnPropertyChanged(nameof(CertFileDisplayName));
        IsEditing = false;
    }

    [RelayCommand]
    private async Task DeleteSelectedMaterial()
    {
        if (SelectedMaterial == null) return;

        var result = MessageBox.Show(
            $"Удалить материал \"{SelectedMaterial.Name}\"?\n\n" +
            "Это действие нельзя отменить. Связанный файл сертификата также будет удалён.",
            "Подтверждение удаления",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            if (!string.IsNullOrEmpty(SelectedMaterial.CertificateFilePath) && _fileService.FileExists(SelectedMaterial.CertificateFilePath))
                File.Delete(SelectedMaterial.CertificateFilePath);

            var mat = await context.Materials.FindAsync(SelectedMaterial.Id);
            if (mat != null)
            {
                context.Materials.Remove(mat);
                await context.SaveChangesAsync();
            }
            await LoadMaterialsAsync();
            StatusMessage = "Материал удалён";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка удаления: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ==================== КОМАНДЫ ДЛЯ РАБОТЫ С ФАЙЛАМИ ====================

    public bool HasTempCertFile => !string.IsNullOrEmpty(_tempCertFilePath);
    public bool HasSavedCertFile => !string.IsNullOrEmpty(EditingMaterial?.CertificateFilePath);

    public string CertFileDisplayName
    {
        get
        {
            if (!string.IsNullOrEmpty(_tempCertFilePath))
                return Path.GetFileName(_tempCertFilePath);
            if (!string.IsNullOrEmpty(EditingMaterial?.CertificateFilePath))
                return Path.GetFileName(EditingMaterial.CertificateFilePath);
            return string.Empty;
        }
    }

    [RelayCommand]
    private void AttachCertificateFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Выберите файл сертификата",
            Filter = "Все файлы (*.*)|*.*|PDF (*.pdf)|*.pdf|Изображения (*.jpg;*.png)|*.jpg;*.png"
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "AGenerator_Temp");
            Directory.CreateDirectory(tempDir);

            if (!string.IsNullOrEmpty(_tempCertFilePath) && File.Exists(_tempCertFilePath))
                File.Delete(_tempCertFilePath);

            var tempFileName = $"cert_{Guid.NewGuid():N}{Path.GetExtension(dialog.FileName)}";
            _tempCertFilePath = Path.Combine(tempDir, tempFileName);
            File.Copy(dialog.FileName, _tempCertFilePath, true);

            OnPropertyChanged(nameof(HasTempCertFile));
            OnPropertyChanged(nameof(HasSavedCertFile));
            OnPropertyChanged(nameof(CertFileDisplayName));

            StatusMessage = $"Файл выбран: {Path.GetFileName(dialog.FileName)} (будет сохранён при подтверждении)";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка выбора файла: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void RemoveCertificateFile()
    {
        if (EditingMaterial == null) return;

        if (!string.IsNullOrEmpty(_tempCertFilePath) && File.Exists(_tempCertFilePath))
            try { File.Delete(_tempCertFilePath); } catch { }
        _tempCertFilePath = null;

        if (!string.IsNullOrEmpty(EditingMaterial.CertificateFilePath))
        {
            if (_fileService.FileExists(EditingMaterial.CertificateFilePath))
                File.Delete(EditingMaterial.CertificateFilePath);
            EditingMaterial.CertificateFilePath = string.Empty;
        }

        OnPropertyChanged(nameof(HasTempCertFile));
        OnPropertyChanged(nameof(HasSavedCertFile));
        OnPropertyChanged(nameof(CertFileDisplayName));
        StatusMessage = "Файл сертификата удалён";
    }

    [RelayCommand]
    private void OpenCertificateFile()
    {
        if (SelectedMaterial == null) return;
        var filePath = SelectedMaterial.CertificateFilePath;
        if (string.IsNullOrEmpty(filePath)) return;

        if (!_fileService.FileExists(filePath))
        {
            SelectedMaterial.CertificateFilePath = string.Empty;
            MessageBox.Show("Файл сертификата не найден. Возможно, он был удалён или перемещён.",
                "Файл не найден", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try { _fileService.OpenFile(filePath); }
        catch (Exception ex)
        {
            MessageBox.Show($"Не удалось открыть файл: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    public void HandleCertificateFileDrop(string[] filePaths)
    {
        if (EditingMaterial == null || filePaths.Length == 0) return;
        var sourcePath = filePaths[0];
        if (!File.Exists(sourcePath)) return;

        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "AGenerator_Temp");
            Directory.CreateDirectory(tempDir);

            if (!string.IsNullOrEmpty(_tempCertFilePath) && File.Exists(_tempCertFilePath))
                File.Delete(_tempCertFilePath);

            var tempFileName = $"cert_{Guid.NewGuid():N}{Path.GetExtension(sourcePath)}";
            _tempCertFilePath = Path.Combine(tempDir, tempFileName);
            File.Copy(sourcePath, _tempCertFilePath, true);

            OnPropertyChanged(nameof(HasTempCertFile));
            OnPropertyChanged(nameof(HasSavedCertFile));
            OnPropertyChanged(nameof(CertFileDisplayName));

            StatusMessage = $"Файл выбран: {Path.GetFileName(sourcePath)} (будет сохранён при подтверждении)";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка выбора файла: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
