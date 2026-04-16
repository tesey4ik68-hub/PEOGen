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
    private readonly int _constructionObjectId;

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

    [ObservableProperty]
    private OrganizationRole? _roleFilter;

    public OrganizationsViewModel(IDbContextFactory<AppDbContext> contextFactory, int constructionObjectId)
    {
        _contextFactory = contextFactory;
        _constructionObjectId = constructionObjectId;
        _ = LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        IsLoading = true;
        StatusMessage = "Загрузка организаций...";

        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var query = context.Organizations
                .Where(o => o.ConstructionObjectId == _constructionObjectId);

            if (RoleFilter.HasValue)
                query = query.Where(o => o.Role == RoleFilter.Value);

            var list = await query
                .OrderBy(o => o.Role)
                .ThenBy(o => o.Name)
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

    partial void OnRoleFilterChanged(OrganizationRole? value)
    {
        _ = LoadDataAsync();
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
            ConstructionObjectId = _constructionObjectId,
            Role = OrganizationRole.Other,
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

        var orgName = SelectedOrganization.Name;
        var orgId = SelectedOrganization.Id;

        var result = MessageBox.Show(
            $"Удалить организацию \"{orgName}\"?\n\n" +
            "Это действие нельзя отменить.",
            "Подтверждение удаления",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        // Сразу удаляем из UI-коллекции
        Organizations.Remove(SelectedOrganization);
        SelectedOrganization = null;

        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            if (orgId > 0)
            {
                // Существующая организация — удаляем из БД
                var orgToDelete = await context.Organizations.FindAsync(orgId);
                if (orgToDelete != null)
                {
                    context.Organizations.Remove(orgToDelete);
                    await context.SaveChangesAsync();
                }
            }
            // Для новой (Id == 0) — просто не сохраняем, она уже удалена из UI

            StatusMessage = Organizations.Count > 0
                ? $"Организация удалена. Осталось: {Organizations.Count}"
                : "Нет организаций. Нажмите «➕ Добавить» для создания.";
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

            // Логика активности: только одна активная организация на роль
            if (org.IsActive && (org.Role == OrganizationRole.Customer || org.Role == OrganizationRole.GenContractor || org.Role == OrganizationRole.Designer))
            {
                // Находим все активные организации той же роли и деактивируем их
                var sameRoleOrgs = await context.Organizations
                    .Where(o => o.ConstructionObjectId == _constructionObjectId 
                             && o.Role == org.Role 
                             && o.IsActive 
                             && o.Id != org.Id)
                    .ToListAsync();

                foreach (var sameOrg in sameRoleOrgs)
                {
                    sameOrg.IsActive = false;
                    context.Entry(sameOrg).State = EntityState.Modified;
                }
            }

            if (org.Id == 0)
            {
                org.ConstructionObjectId = _constructionObjectId;
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
