using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using AGenerator.Database;
using AGenerator.Models;
using AGenerator.Services;
using AGenerator.ViewModels;
using AGenerator.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AGenerator;

public partial class App : Application
{
    public static IServiceProvider? ServiceProvider { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Устанавливаем режим завершения на явный shutdown
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // Глобальная обработка исключений
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            MessageBox.Show(
                $"Критическая ошибка: {ex?.Message ?? args.ExceptionObject?.ToString() ?? "Неизвестная ошибка"}",
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        };

        var services = new ServiceCollection();

        // Регистрация DbContext с фабрикой
        services.AddDbContextFactory<AppDbContext>(options =>
        {
            var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "agenerator.db");
            options.UseSqlite($"Data Source={dbPath}");
        });

        // Регистрация сервисов
        services.AddSingleton<IFileService, FileService>();
        services.AddSingleton<ThemeService>();

        // Регистрация View и ViewModel
        services.AddTransient<ObjectSelectionWindow>();
        services.AddTransient<ObjectSelectionViewModel>();
        services.AddTransient<MainWindow>();
        services.AddTransient<MainViewModel>();

        ServiceProvider = services.BuildServiceProvider();

        // Инициализация БД
        InitializeDatabase();

        // Применение сохранённой темы
        ApplySavedTheme();

        // ВАЖНО: Запускаем цикл ПОСЛЕ полной инициализации Application
        // Используем Dispatcher.BeginInvoke, чтобы ресурсы App.xaml успели загрузиться
        Dispatcher.BeginInvoke(new Action(RunObjectSelectionLoop));
    }

    private static void InitializeDatabase()
    {
        using var scope = ServiceProvider!.CreateScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        using var context = contextFactory.CreateDbContext();
        context.Database.EnsureCreated();

        // Миграция: переименование ManholeType/Manhole → IntervalType, удаление Manhole
        try
        {
            var connection = context.Database.GetDbConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "PRAGMA table_info(Acts)";

            connection.Open();
            var columnNames = new List<string>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                columnNames.Add(reader.GetString(1)); // имя столбца — 2-й параметр
            }
            connection.Close();

            var hasIntervalType = columnNames.Contains("IntervalType");
            var hasManholeType = columnNames.Contains("ManholeType");

            if (!hasIntervalType)
            {
                context.Database.ExecuteSqlRaw(
                    "ALTER TABLE Acts ADD COLUMN IntervalType TEXT DEFAULT 'на интервале'");

                if (hasManholeType)
                {
                    context.Database.ExecuteSqlRaw(
                        @"UPDATE Acts SET IntervalType = CASE
                            WHEN ManholeType = 'Камера' THEN 'в камере'
                            WHEN ManholeType = 'Дождеприемная решетка' THEN 'на интервале'
                            ELSE 'в колодце'
                        END");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DB Migration] Ошибка миграции: {ex.Message}");
        }
    }

    private static void ApplySavedTheme()
    {
        try
        {
            var themeService = ServiceProvider!.GetRequiredService<ThemeService>();
            themeService.ApplySavedTheme();
        }
        catch
        {
            // Игнорируем — используем тему по умолчанию
        }

        // Убедимся, что цвета текста установлены (даже если тема не загружена)
        EnsureTextColorsAreSet();
    }

    private static void EnsureTextColorsAreSet()
    {
        var resources = Application.Current.Resources;

        // Проверяем, установлены ли цвета текста
        if (!resources.Contains("TextPrimary"))
        {
            // Для темы по умолчанию (светло-синяя) используем тёмный текст
            resources["TextPrimary"] = System.Windows.Media.Color.FromRgb(0x1A, 0x20, 0x2C);
            resources["TextSecondary"] = System.Windows.Media.Color.FromRgb(0x4A, 0x55, 0x68);
        }
    }

    /// <summary>
    /// Цикл выбора объекта: показывает ObjectSelectionWindow, затем MainWindow.
    /// При закрытии MainWindow возвращается к выбору объекта.
    /// </summary>
    private void RunObjectSelectionLoop()
    {
        while (true)
        {
            using var scope = ServiceProvider!.CreateScope();
            var selectionWindow = scope.ServiceProvider.GetRequiredService<ObjectSelectionWindow>();
            var selectionVm = scope.ServiceProvider.GetRequiredService<ObjectSelectionViewModel>();

            selectionWindow.DataContext = selectionVm;

            ConstructionObject? selectedObject = null;

            // Подписка на событие выбора объекта
            selectionVm.RequestSelect += (s, obj) =>
            {
                selectedObject = obj;
                selectionWindow.DialogResult = true;
                selectionWindow.Close();
            };

            // Показываем окно выбора
            var dialogResult = selectionWindow.ShowDialog();

            if (dialogResult != true || selectedObject is null)
            {
                // Пользователь закрыл окно выбора -> выход из приложения
                break;
            }

            System.Diagnostics.Debug.WriteLine($"[DEBUG] Объект выбран: {selectedObject.Name} (ID={selectedObject.Id})");

            // Создаём главное окно с выбранным объектом
            var mainWindow = scope.ServiceProvider.GetRequiredService<MainWindow>();
            var mainVm = scope.ServiceProvider.GetRequiredService<MainViewModel>();

            mainWindow.DataContext = mainVm;
            Application.Current.MainWindow = mainWindow;

            // Устанавливаем объект и запускаем асинхронную инициализацию
            mainVm.SetCurrentObject(selectedObject);

            // Показываем главное окно
            mainWindow.Show();

            // Ждем закрытия окна через DispatcherFrame (не блокирует UI)
            var waitTcs = new TaskCompletionSource<bool>();
            mainWindow.Closed += (s, args) => waitTcs.SetResult(true);
            
            var frame = new System.Windows.Threading.DispatcherFrame();
            waitTcs.Task.ContinueWith(_ => frame.Continue = false);
            System.Windows.Threading.Dispatcher.PushFrame(frame);
        }

        Shutdown();
    }
}
