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

namespace AGenerator.ViewModels;

/// <summary>
/// ViewModel для управления справочником организаций
/// </summary>
public partial class OrganizationsViewModel : ViewModelBase
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    [ObservableProperty]
    private ObservableCollection<Organization> _organizations = new();

    [ObservableProperty]
    private Organization? _selectedOrganization;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "Загрузка организаций...";

    [ObservableProperty]
    private string _searchText = string.Empty;

    public OrganizationsViewModel(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
        _ = LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        IsLoading = true;
        StatusMessage = "Загрузка организаций...";

        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var list = await context.Organizations
                .OrderBy(o => o.Name)
                .ToListAsync();

            Organizations.Clear();
            foreach (var org in list)
                Organizations.Add(org);

            StatusMessage = Organizations.Count > 0
                ? $"Организаций: {Organizations.Count}"
                : "Нет организаций. Нажмите «➕ Добавить» для создания.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка загрузки: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[ERROR] Ошибка загрузки организаций: {ex}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadDataAsync();
    }

    [RelayCommand]
    private void AddNewOrganization()
    {
        var newOrg = new Organization
        {
            Name = "Новая организация",
            Requisites = "",
            ShortName = "",
            IsActive = true,
            CreatedAt = DateTime.Now
        };

        Organizations.Insert(0, newOrg);
        SelectedOrganization = newOrg;
        StatusMessage = "Введите данные организации и сохраните";
    }

    [RelayCommand]
    private async Task DeleteSelectedOrganizationAsync()
    {
        if (SelectedOrganization == null) return;

        var result = MessageBox.Show(
            $"Удалить организацию \"{SelectedOrganization.Name}\"?\n\n" +
            "Это действие нельзя отменить.",
            "Подтверждение удаления",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var orgToDelete = await context.Organizations.FindAsync(SelectedOrganization.Id);
            if (orgToDelete != null)
            {
                context.Organizations.Remove(orgToDelete);
                await context.SaveChangesAsync();

                Organizations.Remove(SelectedOrganization);
                SelectedOrganization = null;

                StatusMessage = "Организация удалена";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Ошибка удаления: {ex.Message}",
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);

            StatusMessage = $"Ошибка удаления: {ex.Message}";
        }
    }

    /// <summary>
    /// Сохранить изменения организации в БД (вызывается из View при потере фокуса или вручную)
    /// </summary>
    public async void OnOrganizationChanged(Organization org)
    {
        if (org == null) return;
        await SaveOrganizationAsync(org);
    }

    private async Task SaveOrganizationAsync(Organization org)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            if (org.Id == 0)
            {
                context.Organizations.Add(org);
            }
            else
            {
                context.Attach(org);
                context.Entry(org).State = EntityState.Modified;
            }

            await context.SaveChangesAsync();
            StatusMessage = "Организация сохранена";
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Ошибка сохранения: {ex.Message}",
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);

            StatusMessage = $"Ошибка сохранения: {ex.Message}";
        }
    }
}
