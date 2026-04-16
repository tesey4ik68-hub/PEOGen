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
using System.Collections.Generic;

namespace AGenerator.ViewModels;

/// <summary>
/// ViewModel для управления сотрудниками текущего объекта строительства.
/// Аналог VBA-листа "Люди".
/// </summary>
public partial class EmployeesViewModel : ViewModelBase
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly IFileService _fileService;
    private readonly int _objectId;
    private readonly string _objectName;

    [ObservableProperty]
    private ObservableCollection<Employee> _employees = new();

    [ObservableProperty]
    private Employee? _selectedEmployee;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private Employee _editingEmployee = new();

    /// <summary>
    /// Список организаций для выбора (из объекта + справочник)
    /// </summary>
    public ObservableCollection<Organization> _availableOrganizations = new();
    public ObservableCollection<Organization> AvailableOrganizations => _availableOrganizations;

    /// <summary>
    /// Выбранная организация в форме редактирования
    /// </summary>
    [ObservableProperty]
    private Organization? _selectedOrganizationForEmployee;

    /// <summary>
    /// Временный путь к файлу приказа (копия, которая удаляется при отмене).
    /// Если файл уже сохранён в БД — это основной путь.
    /// </summary>
    private string? _tempOrderFilePath;

    [ObservableProperty]
    private string _statusMessage = "Загрузка сотрудников...";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _searchText = string.Empty;

    public EmployeesViewModel(IDbContextFactory<AppDbContext> contextFactory, IFileService fileService, int objectId, string objectName)
    {
        _contextFactory = contextFactory;
        _fileService = fileService;
        _objectId = objectId;
        _objectName = objectName;
        _ = LoadEmployeesAsync();
        _ = LoadOrganizationsAsync();
    }

    private async Task LoadEmployeesAsync()
    {
        IsLoading = true;
        StatusMessage = "Загрузка сотрудников...";

        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var list = await context.Employees
                .Where(e => e.ConstructionObjectId == _objectId && e.IsActive)
                .OrderBy(e => e.Role)
                .ThenBy(e => e.FullName)
                .ToListAsync();

            // Проверяем существование файлов и очищаем битые ссылки
            foreach (var emp in list)
            {
                if (!string.IsNullOrEmpty(emp.OrderFilePath) && !_fileService.FileExists(emp.OrderFilePath))
                {
                    emp.OrderFilePath = string.Empty;
                    context.Entry(emp).Property(e => e.OrderFilePath).IsModified = true;
                }
            }
            await context.SaveChangesAsync();

            Employees.Clear();
            foreach (var emp in list)
                Employees.Add(emp);

            StatusMessage = Employees.Count > 0
                ? $"Сотрудников: {Employees.Count}"
                : "Нет сотрудников. Нажмите «➕ Добавить» для создания.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка загрузки: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[ERROR] Ошибка загрузки сотрудников: {ex}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadOrganizationsAsync()
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            // Загружаем организации только текущего объекта
            var objectOrgs = await context.Organizations
                .Where(o => o.ConstructionObjectId == _objectId)
                .OrderBy(o => o.Name)
                .ToListAsync();

            _availableOrganizations.Clear();
            foreach (var org in objectOrgs)
                _availableOrganizations.Add(org);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ERROR] Ошибка загрузки организаций: {ex}");
        }
    }

    /// <summary>
    /// Загрузить организации из справочника (публичный метод для вызова из View)
    /// </summary>
    public async Task LoadOrganizationsFromDirectoryAsync()
    {
        await LoadOrganizationsAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadEmployeesAsync();
    }

    /// <summary>
    /// Вызывается при выборе организации из ComboBox — заполняет наименование и реквизиты
    /// </summary>
    public void OnOrganizationSelected()
    {
        if (SelectedOrganizationForEmployee == null) return;
        EditingEmployee.OrganizationName = SelectedOrganizationForEmployee.DisplayName;
        EditingEmployee.OrganizationRequisites = SelectedOrganizationForEmployee.FullRequisites;
        OnPropertyChanged(nameof(EditingEmployee));
    }

    /// <summary>
    /// Обновить привязку EditingEmployee для отображения изменений в UI
    /// </summary>
    public void RefreshEditingEmployee()
    {
        OnPropertyChanged(nameof(EditingEmployee));
    }

    /// <summary>
    /// Обновить привязку SelectedOrganizationForEmployee
    /// </summary>
    public void RefreshSelectedOrganization()
    {
        OnPropertyChanged(nameof(SelectedOrganizationForEmployee));
    }

    /// <summary>
    /// Событие для запроса открытия окна добавления организации
    /// </summary>
    public event EventHandler? RequestAddOrganization;

    [RelayCommand]
    private async Task AddOrganizationAsync()
    {
        RequestAddOrganization?.Invoke(this, EventArgs.Empty);
        // После добавления перезагружаем список
        await LoadOrganizationsAsync();
    }

    [RelayCommand]
    private void AddNewEmployee()
    {
        _tempOrderFilePath = null;
        EditingEmployee = new Employee
        {
            ConstructionObjectId = _objectId,
            Role = RepresentativeType.SK_Zakazchika,
            IsActive = true
        };
        SelectedOrganizationForEmployee = null;
        OnPropertyChanged(nameof(EditingEmployee));
        OnPropertyChanged(nameof(HasTempOrderFile));
        OnPropertyChanged(nameof(HasSavedOrderFile));
        OnPropertyChanged(nameof(OrderFileDisplayName));
        IsEditing = true;
    }

    [RelayCommand]
    private void EditSelectedEmployee()
    {
        if (SelectedEmployee == null) return;

        _tempOrderFilePath = null;

        // Клонируем, чтобы можно было отменить изменения
        EditingEmployee = new Employee
        {
            Id = SelectedEmployee.Id,
            ConstructionObjectId = SelectedEmployee.ConstructionObjectId,
            FullName = SelectedEmployee.FullName,
            Position = SelectedEmployee.Position,
            OrganizationName = SelectedEmployee.OrganizationName,
            OrderNumber = SelectedEmployee.OrderNumber,
            OrderDate = SelectedEmployee.OrderDate,
            NrsNumber = SelectedEmployee.NrsNumber,
            NrsDate = SelectedEmployee.NrsDate,
            WorkStartDate = SelectedEmployee.WorkStartDate,
            WorkEndDate = SelectedEmployee.WorkEndDate,
            IncludeOrganizationInAct = SelectedEmployee.IncludeOrganizationInAct,
            OrganizationRequisites = SelectedEmployee.OrganizationRequisites,
            Role = SelectedEmployee.Role,
            IsActive = SelectedEmployee.IsActive,
            OrderFilePath = SelectedEmployee.OrderFilePath
        };

        // Пытаемся найти организацию по имени
        SelectedOrganizationForEmployee = _availableOrganizations.FirstOrDefault(
            o => o.DisplayName.Equals(EditingEmployee.OrganizationName, StringComparison.OrdinalIgnoreCase));

        OnPropertyChanged(nameof(EditingEmployee));
        OnPropertyChanged(nameof(HasTempOrderFile));
        OnPropertyChanged(nameof(HasSavedOrderFile));
        OnPropertyChanged(nameof(OrderFileDisplayName));
        IsEditing = true;
    }

    /// <summary>
    /// Копировать сотрудника — открывает форму с данными выбранного, но как новая запись
    /// </summary>
    [RelayCommand]
    private void CopyEmployee()
    {
        if (SelectedEmployee == null) return;

        _tempOrderFilePath = null;

        EditingEmployee = new Employee
        {
            ConstructionObjectId = _objectId,
            FullName = SelectedEmployee.FullName,
            Position = SelectedEmployee.Position,
            OrganizationName = SelectedEmployee.OrganizationName,
            OrderNumber = SelectedEmployee.OrderNumber,
            OrderDate = SelectedEmployee.OrderDate,
            NrsNumber = SelectedEmployee.NrsNumber,
            NrsDate = SelectedEmployee.NrsDate,
            WorkStartDate = SelectedEmployee.WorkStartDate,
            WorkEndDate = SelectedEmployee.WorkEndDate,
            IncludeOrganizationInAct = SelectedEmployee.IncludeOrganizationInAct,
            OrganizationRequisites = SelectedEmployee.OrganizationRequisites,
            Role = SelectedEmployee.Role,
            IsActive = true,
            // OrderFilePath НЕ копируем — файл нужно прикрепить заново
        };
        OnPropertyChanged(nameof(HasTempOrderFile));
        OnPropertyChanged(nameof(HasSavedOrderFile));
        OnPropertyChanged(nameof(OrderFileDisplayName));
        IsEditing = true;
    }

    [RelayCommand]
    private async Task SaveEmployee()
    {
        if (string.IsNullOrWhiteSpace(EditingEmployee.FullName))
        {
            MessageBox.Show("ФИО не может быть пустым.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            if (EditingEmployee.Id == 0)
            {
                // Новый сотрудник — сохраняем файл в основную папку
                if (!string.IsNullOrEmpty(_tempOrderFilePath) && File.Exists(_tempOrderFilePath))
                {
                    using (var sourceStream = File.OpenRead(_tempOrderFilePath))
                    {
                        EditingEmployee.OrderFilePath = _fileService.SaveOrderFile(
                            _objectId,
                            _objectName,
                            sourceStream,
                            EditingEmployee.FullName,
                            EditingEmployee.OrderNumber,
                            EditingEmployee.OrderDate,
                            Path.GetFileName(_tempOrderFilePath));
                    }
                    File.Delete(_tempOrderFilePath);
                }
                _tempOrderFilePath = null;

                context.Employees.Add(EditingEmployee);
            }
            else
            {
                var existing = await context.Employees.FindAsync(EditingEmployee.Id);
                if (existing != null)
                {
                    // Если был временный файл — сохраняем в основную папку
                    if (!string.IsNullOrEmpty(_tempOrderFilePath) && File.Exists(_tempOrderFilePath))
                    {
                        // Удаляем старый файл
                        if (!string.IsNullOrEmpty(existing.OrderFilePath) && _fileService.FileExists(existing.OrderFilePath))
                            File.Delete(existing.OrderFilePath);

                        using (var sourceStream = File.OpenRead(_tempOrderFilePath))
                        {
                            existing.OrderFilePath = _fileService.SaveOrderFile(
                                _objectId,
                                _objectName,
                                sourceStream,
                                EditingEmployee.FullName,
                                EditingEmployee.OrderNumber,
                                EditingEmployee.OrderDate,
                                Path.GetFileName(_tempOrderFilePath));
                        }
                        File.Delete(_tempOrderFilePath);
                        _tempOrderFilePath = null;
                    }

                    existing.FullName = EditingEmployee.FullName;
                    existing.Position = EditingEmployee.Position;
                    existing.OrganizationName = EditingEmployee.OrganizationName;
                    existing.OrderNumber = EditingEmployee.OrderNumber;
                    existing.OrderDate = EditingEmployee.OrderDate;
                    existing.NrsNumber = EditingEmployee.NrsNumber;
                    existing.NrsDate = EditingEmployee.NrsDate;
                    existing.WorkStartDate = EditingEmployee.WorkStartDate;
                    existing.WorkEndDate = EditingEmployee.WorkEndDate;
                    existing.IncludeOrganizationInAct = EditingEmployee.IncludeOrganizationInAct;
                    existing.OrganizationRequisites = EditingEmployee.OrganizationRequisites;
                    existing.Role = EditingEmployee.Role;
                    existing.IsActive = EditingEmployee.IsActive;
                }
            }

            await context.SaveChangesAsync();
            IsEditing = false;
            _tempOrderFilePath = null;
            OnPropertyChanged(nameof(HasTempOrderFile));
            OnPropertyChanged(nameof(HasSavedOrderFile));
            OnPropertyChanged(nameof(OrderFileDisplayName));
            await LoadEmployeesAsync();
            StatusMessage = EditingEmployee.Id == 0 ? "Сотрудник добавлен" : "Сотрудник обновлён";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void CancelEdit()
    {
        // Удаляем временный файл при отмене
        if (!string.IsNullOrEmpty(_tempOrderFilePath) && File.Exists(_tempOrderFilePath))
        {
            try { File.Delete(_tempOrderFilePath); } catch { /* игнорируем */ }
        }
        _tempOrderFilePath = null;
        SelectedOrganizationForEmployee = null;
        OnPropertyChanged(nameof(HasTempOrderFile));
        OnPropertyChanged(nameof(HasSavedOrderFile));
        OnPropertyChanged(nameof(OrderFileDisplayName));
        IsEditing = false;
    }

    [RelayCommand]
    private async Task DeleteSelectedEmployee()
    {
        if (SelectedEmployee == null) return;

        var result = MessageBox.Show(
            $"Удалить сотрудника \"{SelectedEmployee.FullName}\"?\n\n" +
            "Это действие нельзя отменить. Связанный файл приказа также будет удалён.",
            "Подтверждение удаления",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            // Удаляем файл приказа с диска
            if (!string.IsNullOrEmpty(SelectedEmployee.OrderFilePath) && _fileService.FileExists(SelectedEmployee.OrderFilePath))
                File.Delete(SelectedEmployee.OrderFilePath);

            // Жёсткое удаление из БД
            var dbEmp = await context.Employees.FindAsync(SelectedEmployee.Id);
            if (dbEmp != null)
            {
                context.Employees.Remove(dbEmp);
            }

            await context.SaveChangesAsync();
            await LoadEmployeesAsync();
            StatusMessage = "Сотрудник удалён";
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Ошибка удаления: {ex.Message}",
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Получить отображаемое имя роли
    /// </summary>
    public static string GetRoleDisplayName(RepresentativeType role)
    {
        return role switch
        {
            RepresentativeType.SK_Zakazchika => "СК Заказчика",
            RepresentativeType.GenPodryadchik => "Генподрядчик",
            RepresentativeType.SK_GenPodryadchika => "СК Генподрядчика",
            RepresentativeType.Podryadchik => "Подрядчик",
            RepresentativeType.AvtorskiyNadzor => "Авторский надзор",
            RepresentativeType.InoeLico => "Иное лицо",
            _ => role.ToString()
        };
    }

    // ==================== КОМАНДЫ ДЛЯ РАБОТЫ С ФАЙЛАМИ ====================

    /// <summary>
    /// Есть ли ВРЕМЕННЫЙ файл приказа (выбран но не сохранён)
    /// </summary>
    public bool HasTempOrderFile => !string.IsNullOrEmpty(_tempOrderFilePath);

    /// <summary>
    /// Есть ли сохранённый файл приказа у редактируемого сотрудника
    /// </summary>
    public bool HasSavedOrderFile => !string.IsNullOrEmpty(EditingEmployee?.OrderFilePath);

    /// <summary>
    /// Отображаемое имя файла (временного или основного)
    /// </summary>
    public string OrderFileDisplayName
    {
        get
        {
            if (!string.IsNullOrEmpty(_tempOrderFilePath))
                return Path.GetFileName(_tempOrderFilePath);
            if (!string.IsNullOrEmpty(EditingEmployee?.OrderFilePath))
                return Path.GetFileName(EditingEmployee.OrderFilePath);
            return string.Empty;
        }
    }

    [RelayCommand]
    private void AttachOrderFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Выберите файл приказа",
            Filter = "Все файлы (*.*)|*.*|PDF (*.pdf)|*.pdf|DOCX (*.docx)|*.docx|Изображения (*.jpg;*.png)|*.jpg;*.png"
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            // Копируем во временную папку
            var tempDir = Path.Combine(Path.GetTempPath(), "AGenerator_Temp");
            Directory.CreateDirectory(tempDir);

            // Удаляем старый временный файл если есть
            if (!string.IsNullOrEmpty(_tempOrderFilePath) && File.Exists(_tempOrderFilePath))
                File.Delete(_tempOrderFilePath);

            var tempFileName = $"order_{Guid.NewGuid():N}{Path.GetExtension(dialog.FileName)}";
            _tempOrderFilePath = Path.Combine(tempDir, tempFileName);
            File.Copy(dialog.FileName, _tempOrderFilePath, true);

            OnPropertyChanged(nameof(HasTempOrderFile));
            OnPropertyChanged(nameof(HasSavedOrderFile));
            OnPropertyChanged(nameof(OrderFileDisplayName));

            StatusMessage = $"Файл выбран: {Path.GetFileName(dialog.FileName)} (будет сохранён при подтверждении)";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка выбора файла: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void RemoveOrderFile()
    {
        if (EditingEmployee == null) return;

        // Удаляем временный файл
        if (!string.IsNullOrEmpty(_tempOrderFilePath) && File.Exists(_tempOrderFilePath))
        {
            try { File.Delete(_tempOrderFilePath); } catch { /* игнорируем */ }
        }
        _tempOrderFilePath = null;

        // Для существующего сотрудника — помечаем на удаление при сохранении
        if (!string.IsNullOrEmpty(EditingEmployee.OrderFilePath))
        {
            // Удаляем основной файл
            if (_fileService.FileExists(EditingEmployee.OrderFilePath))
                File.Delete(EditingEmployee.OrderFilePath);

            EditingEmployee.OrderFilePath = string.Empty;
        }

        OnPropertyChanged(nameof(HasTempOrderFile));
        OnPropertyChanged(nameof(HasSavedOrderFile));
        OnPropertyChanged(nameof(OrderFileDisplayName));
        StatusMessage = "Файл приказа удалён";
    }

    [RelayCommand]
    private void OpenOrderFile()
    {
        if (SelectedEmployee == null) return;

        var filePath = SelectedEmployee.OrderFilePath;
        if (string.IsNullOrEmpty(filePath)) return;

        // Проверяем существование
        if (!_fileService.FileExists(filePath))
        {
            // Файл удалён/перемещён — очищаем ссылку
            SelectedEmployee.OrderFilePath = string.Empty;
            MessageBox.Show("Файл приказа не найден. Возможно, он был удалён или перемещён.",
                "Файл не найден", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            _fileService.OpenFile(filePath);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Не удалось открыть файл: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>
    /// Обработка Drop файла приказа (вызывается из code-behind)
    /// </summary>
    public void HandleOrderFileDrop(string[] filePaths)
    {
        if (EditingEmployee == null || filePaths.Length == 0) return;

        var sourcePath = filePaths[0];
        if (!File.Exists(sourcePath)) return;

        try
        {
            // Копируем во временную папку
            var tempDir = Path.Combine(Path.GetTempPath(), "AGenerator_Temp");
            Directory.CreateDirectory(tempDir);

            // Удаляем старый временный файл
            if (!string.IsNullOrEmpty(_tempOrderFilePath) && File.Exists(_tempOrderFilePath))
                File.Delete(_tempOrderFilePath);

            var tempFileName = $"order_{Guid.NewGuid():N}{Path.GetExtension(sourcePath)}";
            _tempOrderFilePath = Path.Combine(tempDir, tempFileName);
            File.Copy(sourcePath, _tempOrderFilePath, true);

            OnPropertyChanged(nameof(HasTempOrderFile));
            OnPropertyChanged(nameof(HasSavedOrderFile));
            OnPropertyChanged(nameof(OrderFileDisplayName));

            StatusMessage = $"Файл выбран: {Path.GetFileName(sourcePath)} (будет сохранён при подтверждении)";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка выбора файла: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
