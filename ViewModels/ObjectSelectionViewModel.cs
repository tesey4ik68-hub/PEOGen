using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using AGenerator.Database;
using AGenerator.Models;
using AGenerator.Services;
using AGenerator.Views;

namespace AGenerator.ViewModels
{
    public partial class ObjectSelectionViewModel : ObservableObject
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private readonly IFileService _fileService;

        [ObservableProperty]
        private ObservableCollection<ConstructionObject> _objects = new();

        [ObservableProperty]
        private ConstructionObject? _selectedObject;

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private bool _isCreatingNew;

        [ObservableProperty]
        private string _newObjectName = string.Empty;

        [ObservableProperty]
        private string _newObjectAddress = string.Empty;

        public ObjectSelectionViewModel(IDbContextFactory<AppDbContext> contextFactory, IFileService fileService)
        {
            _contextFactory = contextFactory;
            _fileService = fileService;
            _ = LoadObjectsAsync();
        }

        private async Task LoadObjectsAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var list = await context.Objects
                .OrderBy(o => o.Name)
                .ToListAsync();
            
            Objects.Clear();
            foreach (var obj in list)
                Objects.Add(obj);
        }

        partial void OnSearchTextChanged(string value)
        {
            FilterObjects();
        }

        private void FilterObjects()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                _ = LoadObjectsAsync();
                return;
            }

            var filtered = Objects
                .Where(o => o.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                         || o.Address.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Временно заменяем список отфильтрованными данными
            // Для простоты оставляем полную загрузку, фильтрация на уровне UI
        }

        [RelayCommand]
        private void StartCreateNew()
        {
            IsCreatingNew = true;
            NewObjectName = string.Empty;
            NewObjectAddress = string.Empty;
        }

        [RelayCommand]
        private void CancelCreateNew()
        {
            IsCreatingNew = false;
        }

        [RelayCommand]
        private async Task SaveNewObject()
        {
            if (string.IsNullOrWhiteSpace(NewObjectName))
            {
                MessageBox.Show("Введите название объекта", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            await using var context = await _contextFactory.CreateDbContextAsync();
            
            var newObj = new ConstructionObject
            {
                Name = NewObjectName,
                Address = NewObjectAddress
            };

            context.Objects.Add(newObj);
            await context.SaveChangesAsync();

            // Обновляем список и выбираем новый объект
            await LoadObjectsAsync();
            SelectedObject = Objects.FirstOrDefault(o => o.Id == newObj.Id);
            
            IsCreatingNew = false;
        }

        [RelayCommand]
        private void SelectObject()
        {
            if (SelectedObject != null)
            {
                try
                {
                    // Создаем файловую структуру для объекта
                    _fileService.EnsureFoldersExist(SelectedObject.Id, SelectedObject.Name);
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] Папки созданы для: {SelectedObject.Name}");

                    // Событие для закрытия окна и передачи объекта в MainWindow
                    RequestSelect?.Invoke(this, SelectedObject);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] Ошибка в SelectObject: {ex}");
                    MessageBox.Show($"Ошибка при подготовке объекта: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("Выберите объект из списка или создайте новый.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        public event EventHandler<ConstructionObject>? RequestSelect;

        [RelayCommand]
        private async Task EditObjectAsync()
        {
            if (SelectedObject == null) return;

            try
            {
                var editVm = new ObjectEditViewModel(_contextFactory, SelectedObject);

                var editWindow = new ObjectEditWindow
                {
                    DataContext = editVm,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen
                };

                // Показываем модально и обновляем список после закрытия
                var result = editWindow.ShowDialog();

                if (result == true)
                {
                    // Обновляем список объектов, чтобы отобразить изменения
                    await LoadObjectsAsync();

                    // Восстанавливаем выбор отредактированного объекта
                    SelectedObject = Objects.FirstOrDefault(o => o.Id == editVm.EditingObject?.Id);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка редактирования: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
