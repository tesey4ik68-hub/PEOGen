using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using AGenerator.Database;
using AGenerator.Models;
using AGenerator.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;

namespace AGenerator.ViewModels;

/// <summary>
/// ViewModel для управления актами строительства с inline-редактированием
/// </summary>
public partial class ActsViewModel : ViewModelBase
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly IFileService _fileService;
    private readonly WordDocumentService _wordService;
    private readonly int _objectId;
    private readonly string _objectName;

    [ObservableProperty]
    private ObservableCollection<Act> _acts = new();

    [ObservableProperty]
    private Act? _selectedAct;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "Загрузка актов...";

    [ObservableProperty]
    private string _searchText = string.Empty;

    // ==================== СПРАВОЧНИКИ ====================

    [ObservableProperty]
    private ObservableCollection<Employee> _employees = new();

    [ObservableProperty]
    private ObservableCollection<Organization> _organizations = new();

    [ObservableProperty]
    private ObservableCollection<Act> _availableActsForLink = new();

    public ActsViewModel(
        IDbContextFactory<AppDbContext> contextFactory,
        IFileService fileService,
        int objectId,
        string objectName)
    {
        _contextFactory = contextFactory;
        _fileService = fileService;
        _objectId = objectId;
        _objectName = objectName;

        // Инициализация сервиса Word с RegistryService
        var templatesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates");
        var baseOutputPath = AppDomain.CurrentDomain.BaseDirectory;
        var registryService = new RegistryService();
        _wordService = new WordDocumentService(templatesPath, baseOutputPath, registryService);

        // Запускаем загрузку асинхронно
        _ = LoadDataAsync();
    }

    /// <summary>
    /// Асинхронная загрузка данных актов, сотрудников и справочников
    /// </summary>
    private async Task LoadDataAsync()
    {
        IsLoading = true;
        StatusMessage = "Загрузка актов...";

        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            // Загрузка актов текущего объекта с навигационными свойствами
            var actsList = await context.Acts
                .Include(a => a.ConstructionObject)
                .Include(a => a.RelatedAct)
                .Include(a => a.RelatedAook)
                .Include(a => a.CustomerRep)
                .Include(a => a.GenContractorRep)
                .Include(a => a.GenContractorSkRep)
                .Include(a => a.ContractorRep)
                .Include(a => a.DesignerRep)
                .Include(a => a.OtherPerson1)
                .Include(a => a.OtherPerson2)
                .Include(a => a.OtherPerson3)
                .Include(a => a.CustomerOrganization)
                .Include(a => a.GenContractorOrganization)
                .Include(a => a.ContractorOrganization)
                .Include(a => a.DesignerOrganization)
                .Include(a => a.ActMaterials)
                    .ThenInclude(am => am.Material)
                .Include(a => a.ActSchemas)
                    .ThenInclude(asc => asc.Schema)
                .Include(a => a.ActProjectDocs)
                    .ThenInclude(apd => apd.ProjectDoc)
                .Include(a => a.Protocols)
                .Where(a => a.ConstructionObjectId == _objectId)
                .OrderByDescending(a => a.ActDate)
                .ToListAsync();

            // Загрузка сотрудников текущего объекта
            var employeesList = await context.Employees
                .Where(e => e.ConstructionObjectId == _objectId && e.IsActive)
                .OrderBy(e => e.FullName)
                .ToListAsync();

            // Все акты этого объекта для связей (ИД/АООК)
            var allActs = await context.Acts
                .Where(a => a.ConstructionObjectId == _objectId)
                .OrderByDescending(a => a.ActDate)
                .ToListAsync();

            // Загрузка всех организаций (справочник)
            var organizationsList = await context.Organizations
                .Where(o => o.IsActive)
                .OrderBy(o => o.Name)
                .ToListAsync();

            // Загрузка объекта с дефолтными организациями
            var constructionObject = await context.Objects
                .Include(o => o.DefaultCustomerOrganization)
                .Include(o => o.DefaultGenContractorOrganization)
                .Include(o => o.DefaultContractorOrganization)
                .Include(o => o.DefaultDesignerOrganization)
                .FirstOrDefaultAsync(o => o.Id == _objectId);

            // Заполняем коллекции
            Acts.Clear();
            foreach (var act in actsList)
            {
                // Пересчитываем вычисляемые поля перед отображением
                ActCalculationService.RecalculateAll(act);
                Acts.Add(act);
            }

            Employees.Clear();
            foreach (var emp in employeesList)
                Employees.Add(emp);

            AvailableActsForLink.Clear();
            foreach (var act in allActs)
                AvailableActsForLink.Add(act);

            Organizations.Clear();
            foreach (var org in organizationsList)
                Organizations.Add(org);

            StatusMessage = Acts.Count > 0
                ? $"Загружено актов: {Acts.Count}, сотрудников: {Employees.Count}"
                : "Нет актов для данного объекта. Нажмите «➕ Новый акт» для создания.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка загрузки актов: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[ERROR] Ошибка загрузки актов: {ex}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Обновить список актов
    /// </summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadDataAsync();
    }

    /// <summary>
    /// Создать новый акт
    /// </summary>
    [RelayCommand]
    private async Task AddNewAct()
    {
        var newAct = new Act
        {
            ConstructionObjectId = _objectId,
            Type = "АОСР",
            ActNumber = "",
            WorkName = "Новые работы",
            ActDate = DateTime.Now,
            WorkStartDate = DateTime.Now,
            WorkEndDate = DateTime.Now,
            Status = "Черновик",
            CreatedAt = DateTime.Now
        };

        // Подставляем дефолтные организации из объекта
        await ApplyDefaultOrganizationsAsync(newAct);

        // Пересчитываем вычисляемые поля
        ActCalculationService.RecalculateAll(newAct);

        // Вставляем в начало коллекции
        Acts.Insert(0, newAct);
        SelectedAct = newAct;

        // Сохраняем в БД для получения ID
        await SaveActToDbAsync(newAct);
    }

    /// <summary>
    /// Применить дефолтные организации из объекта к новому акту
    /// </summary>
    private async Task ApplyDefaultOrganizationsAsync(Act act)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var obj = await context.Objects
                .FirstOrDefaultAsync(o => o.Id == _objectId);

            if (obj == null) return;

            act.CustomerOrganizationId = obj.DefaultCustomerOrganizationId;
            act.GenContractorOrganizationId = obj.DefaultGenContractorOrganizationId;
            act.ContractorOrganizationId = obj.DefaultContractorOrganizationId;
            act.DesignerOrganizationId = obj.DefaultDesignerOrganizationId;
        }
        catch
        {
            // Игнорируем ошибки — организации останутся null
        }
    }

    /// <summary>
    /// Загрузить акт со всеми навигационными свойствами (для генерации документа)
    /// </summary>
    private async Task<Act?> LoadActWithDetailsAsync(int actId)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            return await context.Acts
                .Include(a => a.ConstructionObject)
                .Include(a => a.CustomerOrganization)
                .Include(a => a.GenContractorOrganization)
                .Include(a => a.ContractorOrganization)
                .Include(a => a.DesignerOrganization)
                .Include(a => a.CustomerRep)
                .Include(a => a.GenContractorRep)
                .Include(a => a.GenContractorSkRep)
                .Include(a => a.ContractorRep)
                .Include(a => a.DesignerRep)
                .Include(a => a.OtherPerson1)
                .Include(a => a.OtherPerson2)
                .Include(a => a.OtherPerson3)
                .Include(a => a.ActMaterials)
                    .ThenInclude(am => am.Material)
                .Include(a => a.ActSchemas)
                    .ThenInclude(asc => asc.Schema)
                .Include(a => a.ActProjectDocs)
                    .ThenInclude(apd => apd.ProjectDoc)
                .Include(a => a.Protocols)
                .FirstOrDefaultAsync(a => a.Id == actId);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ERROR] Ошибка загрузки акта: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Удалить выбранный акт
    /// </summary>
    [RelayCommand]
    private async Task DeleteSelectedActAsync()
    {
        if (SelectedAct == null) return;

        var actNumber = string.IsNullOrEmpty(SelectedAct.ActNumber)
            ? "(без номера)"
            : SelectedAct.ActNumber;

        var result = MessageBox.Show(
            $"Удалить акт №{actNumber}?\n\nЭто действие нельзя отменить.",
            "Подтверждение удаления",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var actToDelete = await context.Acts.FindAsync(SelectedAct.Id);
            if (actToDelete != null)
            {
                context.Acts.Remove(actToDelete);
                await context.SaveChangesAsync();

                Acts.Remove(SelectedAct);
                SelectedAct = null;

                StatusMessage = "Акт удалён";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Ошибка удаления акта: {ex.Message}",
                "Ошибка",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            StatusMessage = $"Ошибка удаления: {ex.Message}";
        }
    }

    // ==================== КОМАНДЫ ДЛЯ КНОПОК ВЫБОРА КОЛЛЕКЦИЙ ====================

    [RelayCommand]
    private async Task EditMaterials(Act act)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        // Все материалы текущего объекта
        var allMaterials = await context.Materials
            .Where(m => m.ConstructionObjectId == _objectId)
            .OrderBy(m => m.Name)
            .ToListAsync();

        if (!allMaterials.Any())
        {
            MessageBox.Show("В справочнике объекта нет материалов. Сначала добавьте материалы.",
                "Материалы", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Текущие выбранные материалы акта (через промежуточную таблицу)
        var currentMaterials = await context.ActMaterials
            .Where(am => am.ActId == act.Id)
            .Include(am => am.Material)
            .Select(am => am.Material!)
            .ToListAsync();

        // Создаём VM
        var vm = new MultiSelectViewModel<Material>(
            allMaterials,
            currentMaterials,
            m => $"{m.Name} ({m.Type})");

        vm.WindowTitle = $"Материалы — Акт {act.ActNumber}";

        // Открываем окно
        var window = new Views.MultiSelectWindow("Name")
        {
            DataContext = vm,
            Owner = Application.Current.MainWindow
        };

        if (window.ShowDialog() == true)
        {
            await SaveActMaterialsAsync(act, window.SelectedItemsResult
                .Cast<ViewModels.DisplayItem<Material>>()
                .Select(di => di.Item)
                .ToList());
            StatusMessage = $"Материалы сохранены: {window.SelectedItemsResult.Count} шт.";
            // Перезагружаем данные для обновления MaterialsDisplay
            await LoadDataAsync();
        }
    }

    [RelayCommand]
    private async Task EditSchemas(Act act)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var allSchemas = await context.Schemas
            .Where(s => s.ConstructionObjectId == _objectId)
            .OrderBy(s => s.Name)
            .ToListAsync();

        if (!allSchemas.Any())
        {
            MessageBox.Show("В справочнике объекта нет схем. Сначала добавьте схемы.",
                "Схемы", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var currentSchemas = await context.ActSchemas
            .Where(asc => asc.ActId == act.Id)
            .Include(asc => asc.Schema)
            .Select(asc => asc.Schema!)
            .ToListAsync();

        var vm = new MultiSelectViewModel<Schema>(
            allSchemas,
            currentSchemas,
            s => $"{s.Number} — {s.Name}");

        vm.WindowTitle = $"Исполнительные схемы — Акт {act.ActNumber}";

        var window = new Views.MultiSelectWindow("Name")
        {
            DataContext = vm,
            Owner = Application.Current.MainWindow
        };

        if (window.ShowDialog() == true)
        {
            await SaveActSchemasAsync(act, window.SelectedItemsResult
                .Cast<ViewModels.DisplayItem<Schema>>()
                .Select(di => di.Item)
                .ToList());
            StatusMessage = $"Схемы сохранены: {window.SelectedItemsResult.Count} шт.";
            await LoadDataAsync();
        }
    }

    [RelayCommand]
    private async Task EditProtocols(Act act)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var allProtocols = await context.Protocols
            .Where(p => p.ConstructionObjectId == _objectId)
            .OrderByDescending(p => p.Date)
            .ToListAsync();

        if (!allProtocols.Any())
        {
            MessageBox.Show("В справочнике объекта нет протоколов. Сначала добавьте протоколы.",
                "Протоколы", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Протоколы привязаны к акту напрямую (не через промежуточную таблицу)
        var currentProtocols = await context.Protocols
            .Where(p => p.ActId == act.Id)
            .ToListAsync();

        var vm = new MultiSelectViewModel<Protocol>(
            allProtocols,
            currentProtocols,
            p => $"{p.Number} от {p.Date:dd.MM.yyyy} — {p.Type}");

        vm.WindowTitle = $"Протоколы экспертиз — Акт {act.ActNumber}";

        var window = new Views.MultiSelectWindow("Number")
        {
            DataContext = vm,
            Owner = Application.Current.MainWindow
        };

        if (window.ShowDialog() == true)
        {
            var selectedProtocols = window.SelectedItemsResult
                .Cast<ViewModels.DisplayItem<Protocol>>()
                .Select(di => di.Item)
                .ToList();
            await SaveActProtocolsAsync(act, selectedProtocols);

            StatusMessage = $"Протоколы сохранены: {selectedProtocols.Count} шт.";
            await LoadDataAsync();
        }
    }

    [RelayCommand]
    private async Task EditProjectDocs(Act act)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var allProjectDocs = await context.ProjectDocs
            .Where(pd => pd.ConstructionObjectId == _objectId)
            .OrderBy(pd => pd.Code)
            .ToListAsync();

        if (!allProjectDocs.Any())
        {
            MessageBox.Show("В справочнике объекта нет проектной документации. Сначала добавьте документы.",
                "Проектная документация", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var currentProjectDocs = await context.ActProjectDocs
            .Where(apd => apd.ActId == act.Id)
            .Include(apd => apd.ProjectDoc)
            .Select(apd => apd.ProjectDoc!)
            .ToListAsync();

        var vm = new MultiSelectViewModel<ProjectDoc>(
            allProjectDocs,
            currentProjectDocs,
            pd => $"{pd.Code} — {pd.Name}");

        vm.WindowTitle = $"Проектная документация — Акт {act.ActNumber}";

        var window = new Views.MultiSelectWindow("Code")
        {
            DataContext = vm,
            Owner = Application.Current.MainWindow
        };

        if (window.ShowDialog() == true)
        {
            await SaveActProjectDocsAsync(act, window.SelectedItemsResult
                .Cast<ViewModels.DisplayItem<ProjectDoc>>()
                .Select(di => di.Item)
                .ToList());
            StatusMessage = $"Проектная документация сохранена: {window.SelectedItemsResult.Count} шт.";
            await LoadDataAsync();
        }
    }

    [RelayCommand]
    private void EditRegulatoryDocs(Act act)
    {
        MessageBox.Show(
            $"Выбор нормативных документов для акта \"{act.WorkName}\":\n\n" +
            "Функционал будет реализован после создания справочника нормативов.",
            "Нормативы", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [RelayCommand]
    private void SelectSubsequentWork(Act act)
    {
        MessageBox.Show(
            $"Выбор последующих работ для акта \"{act.WorkName}\":\n\n" +
            "Функционал будет реализован в отдельном окне выбора одного элемента.",
            "Последующие работы", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // ==================== ГЕНЕРАЦИЯ ДОКУМЕНТОВ ====================

    /// <summary>
    /// Сгенерировать Word-документ акта из шаблона.
    /// </summary>
    [RelayCommand]
    private async Task GenerateDocument(Act act)
    {
        if (act == null) return;

        try
        {
            StatusMessage = "Генерация документа...";

            // Перезагружаем акт из БД с навигационными свойствами организаций
            var freshAct = await LoadActWithDetailsAsync(act.Id);
            if (freshAct == null)
            {
                MessageBox.Show("Не удалось загрузить данные акта.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusMessage = "Ошибка: акт не найден в БД";
                return;
            }

            // Копируем загруженные навигационные свойства в UI-акт
            act.CustomerOrganization = freshAct.CustomerOrganization;
            act.GenContractorOrganization = freshAct.GenContractorOrganization;
            act.ContractorOrganization = freshAct.ContractorOrganization;
            act.DesignerOrganization = freshAct.DesignerOrganization;
            act.ConstructionObject = freshAct.ConstructionObject;
            act.CustomerRep = freshAct.CustomerRep;
            act.GenContractorRep = freshAct.GenContractorRep;
            act.GenContractorSkRep = freshAct.GenContractorSkRep;
            act.ContractorRep = freshAct.ContractorRep;
            act.DesignerRep = freshAct.DesignerRep;
            act.OtherPerson1 = freshAct.OtherPerson1;
            act.OtherPerson2 = freshAct.OtherPerson2;
            act.OtherPerson3 = freshAct.OtherPerson3;

            // Пересчитываем вычисляемые поля перед генерацией
            ActCalculationService.RecalculateAll(act);

            // Вызываем сервис
            var filePath = await _wordService.GenerateActAsync(act);

            // Обновляем статус в UI
            act.Status = "✅ Сгенерирован";
            act.GeneratedFilePath = filePath;
            act.UpdatedAt = DateTime.Now;

            // Сохраняем изменения пути в БД
            await SaveActToDbAsync(act);

            StatusMessage = $"Документ создан: {Path.GetFileName(filePath)}";

            // Открываем папку с выделенным файлом
            Process.Start("explorer.exe", $"/select,\"{filePath}\"");
        }
        catch (FileNotFoundException ex)
        {
            MessageBox.Show(
                $"Шаблон не найден:\n{ex.Message}\n\n" +
                "Убедитесь, что файл шаблона присутствует в папке Templates.",
                "Ошибка генерации", MessageBoxButton.OK, MessageBoxImage.Warning);
            act.Status = "❌ Ошибка";
            StatusMessage = "Ошибка генерации: шаблон не найден";
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Ошибка генерации документа:\n\n{ex.Message}",
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            act.Status = "❌ Ошибка";
            StatusMessage = $"Ошибка генерации: {ex.Message}";
        }
    }

    // ==================== СОХРАНЕНИЕ КОЛЛЕКЦИЙ ====================

    /// <summary>
    /// Сохранить выбранные материалы для акта (через промежуточную таблицу ActMaterial)
    /// </summary>
    private async Task SaveActMaterialsAsync(Act act, List<Material> selectedMaterials)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        // Удаляем старые связи
        var oldLinks = await context.ActMaterials
            .Where(am => am.ActId == act.Id)
            .ToListAsync();
        context.ActMaterials.RemoveRange(oldLinks);

        // Добавляем новые связи
        foreach (var material in selectedMaterials)
        {
            context.ActMaterials.Add(new ActMaterial
            {
                ActId = act.Id,
                MaterialId = material.Id,
                Quantity = material.Quantity,
                Note = null
            });
        }

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Сохранить выбранные схемы для акта (через промежуточную таблицу ActSchema)
    /// </summary>
    private async Task SaveActSchemasAsync(Act act, List<Schema> selectedSchemas)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var oldLinks = await context.ActSchemas
            .Where(asc => asc.ActId == act.Id)
            .ToListAsync();
        context.ActSchemas.RemoveRange(oldLinks);

        foreach (var schema in selectedSchemas)
        {
            context.ActSchemas.Add(new ActSchema
            {
                ActId = act.Id,
                SchemaId = schema.Id,
                Note = null
            });
        }

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Сохранить выбранные протоколы для акта (прямая связь Protocol.ActId)
    /// </summary>
    private async Task SaveActProtocolsAsync(Act act, List<Protocol> selectedProtocols)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        // Сбрасываем привязку у старых протоколов
        var oldProtocols = await context.Protocols
            .Where(p => p.ActId == act.Id)
            .ToListAsync();
        foreach (var p in oldProtocols)
            p.ActId = null;

        // Привязываем новые — ATTACH + помечаем как изменённые, т.к. они из другого контекста
        foreach (var protocol in selectedProtocols)
        {
            protocol.ActId = act.Id;
            var entry = context.Attach(protocol);
            entry.Property(p => p.ActId).IsModified = true;
        }

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Сохранить выбранную проектную документацию для акта (через промежуточную таблицу ActProjectDoc)
    /// </summary>
    private async Task SaveActProjectDocsAsync(Act act, List<ProjectDoc> selectedProjectDocs)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var oldLinks = await context.ActProjectDocs
            .Where(apd => apd.ActId == act.Id)
            .ToListAsync();
        context.ActProjectDocs.RemoveRange(oldLinks);

        foreach (var projectDoc in selectedProjectDocs)
        {
            context.ActProjectDocs.Add(new ActProjectDoc
            {
                ActId = act.Id,
                ProjectDocId = projectDoc.Id,
                Note = null
            });
        }

        await context.SaveChangesAsync();
    }

    // ==================== ПЕРЕСЧЁТ И СОХРАНЕНИЕ ====================

    /// <summary>
    /// Пересчитать вычисляемые поля акта и сохранить изменения.
    /// Вызывается из ActsView.xaml.cs при CellEditEnding.
    /// </summary>
    public void RecalculateAndSaveAct(Act act)
    {
        if (act == null) return;

        // Пересчитываем вычисляемые поля по формулам VBA
        ActCalculationService.RecalculateAll(act);

        // Сохраняем в БД
        OnActPropertyChanged(act);
    }

    /// <summary>
    /// Сохранить изменения акта в БД (вызывается при изменении свойств)
    /// </summary>
    public async void OnActPropertyChanged(Act act)
    {
        if (act == null) return;
        await SaveActToDbAsync(act);
    }

    /// <summary>
    /// Внутренний метод сохранения акта в БД
    /// </summary>
    private async Task SaveActToDbAsync(Act act)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            // Обновляем метаданные
            act.UpdatedAt = DateTime.Now;

            if (act.Id == 0)
            {
                // Новый акт
                context.Acts.Add(act);
            }
            else
            {
                // Существующий акт — прикрепляем и помечаем как изменённый
                context.Attach(act);
                context.Entry(act).State = EntityState.Modified;
            }

            await context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Ошибка сохранения акта: {ex.Message}",
                "Ошибка",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            StatusMessage = $"Ошибка сохранения: {ex.Message}";
        }
    }
}
