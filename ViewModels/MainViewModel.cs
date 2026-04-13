using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using AGenerator.Database;
using AGenerator.Models;
using AGenerator.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;

namespace AGenerator.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IFileService _fileService;
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    [ObservableProperty]
    private ConstructionObject? _currentObject;

    [ObservableProperty]
    private string _windowTitle = "P-генератор";

    [ObservableProperty]
    private string _statusMessage = "Готово";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private ViewModelBase? _currentView;

    [ObservableProperty]
    private int _selectedTabIndex = 0;

    // ViewModels для вкладок
    [ObservableProperty]
    private ActsViewModel? _actsViewModel;

    [ObservableProperty]
    private EmployeesViewModel? _employeesViewModel;

    [ObservableProperty]
    private MaterialsViewModel? _materialsViewModel;

    [ObservableProperty]
    private SchemasViewModel? _schemasViewModel;

    [ObservableProperty]
    private ProtocolsViewModel? _protocolsViewModel;

    [ObservableProperty]
    private ProjectDocsViewModel? _projectDocsViewModel;

    public MainViewModel(IFileService fileService, IDbContextFactory<AppDbContext> contextFactory)
    {
        _fileService = fileService;
        _contextFactory = contextFactory;
    }

    /// <summary>
    /// Установить текущий объект строительства (вызывается после выбора в ObjectSelectionWindow)
    /// </summary>
    public void SetCurrentObject(ConstructionObject obj)
    {
        CurrentObject = obj;
        WindowTitle = $"P-генератор — {obj.Name}";
        StatusMessage = $"Работа с объектом: {obj.Name}";

        // ВАЖНО: Не ждем завершения загрузки. Запускаем как fire-and-forget.
        // Данные появятся в таблице чуть позже, но окно откроется сразу.
        _ = InitializeViewAsync(obj.Id);
    }

    /// <summary>
    /// Асинхронная инициализация: создание папок, загрузка данных
    /// </summary>
    private async Task InitializeViewAsync(int objectId)
    {
        try
        {
            // Создаем папки
            _fileService.EnsureFoldersExist(objectId, CurrentObject?.Name ?? "Unknown");

            // Загружаем данные объекта
            await LoadObjectDataAsync();

            // Создаем ViewModel'ы для вкладок
            var objectName = CurrentObject?.Name ?? "Unknown";
            ActsViewModel = new ActsViewModel(_contextFactory, _fileService, objectId, objectName);
            EmployeesViewModel = new EmployeesViewModel(_contextFactory, _fileService, objectId, objectName);
            MaterialsViewModel = new MaterialsViewModel(_contextFactory, _fileService, objectId, objectName);
            SchemasViewModel = new SchemasViewModel(_contextFactory, _fileService, objectId, objectName);
            ProtocolsViewModel = new ProtocolsViewModel(_contextFactory, _fileService, objectId, objectName);
            ProjectDocsViewModel = new ProjectDocsViewModel(_contextFactory, _fileService, objectId, objectName);

            // По умолчанию отображаем Акты
            CurrentView = ActsViewModel;

            StatusMessage = "Данные загружены успешно";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка инициализации: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[ERROR] Ошибка инициализации: {ex}");
        }
    }

    /// <summary>
    /// Обработчик смены вкладки
    /// </summary>
    partial void OnSelectedTabIndexChanged(int value)
    {
        CurrentView = value switch
        {
            0 => ActsViewModel,
            1 => EmployeesViewModel,
            2 => MaterialsViewModel,
            3 => SchemasViewModel,
            4 => ProtocolsViewModel,
            5 => ProjectDocsViewModel,
            _ => ActsViewModel
        };
    }

    /// <summary>
    /// Загрузка данных для текущего объекта (акты, сотрудники и т.д.)
    /// </summary>
    private async Task LoadObjectDataAsync()
    {
        if (CurrentObject == null) return;

        IsLoading = true;
        StatusMessage = "Загрузка данных...";

        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var obj = await context.Objects
                .Include(o => o.Acts)
                .Include(o => o.Employees)
                .Include(o => o.Materials)
                .Include(o => o.Schemas)
                .Include(o => o.Protocols)
                .Include(o => o.ProjectDocs)
                .FirstOrDefaultAsync(o => o.Id == CurrentObject.Id);

            if (obj != null)
            {
                CurrentObject = obj;
                StatusMessage = $"Загружено: актов {obj.Acts.Count}, сотрудников {obj.Employees.Count}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка загрузки: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[ERROR] Ошибка загрузки данных: {ex}");
        }
        finally
        {
            IsLoading = false;
        }
    }
}
