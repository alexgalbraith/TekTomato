using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TekTomato.Data;
using TekTomato.Services;
using TekTomato.ViewModels;
using TekTomato.Views;
using System.Windows;

namespace TekTomato
{
    /// <summary>
    /// Entry point for the TekTomato application.
    /// Responsible for initializing the DI container, applying migrations, and bootstrapping the UI.
    /// </summary>
    public partial class App : Application
    {
        private readonly IServiceProvider _serviceProvider;

        public App()
        {
            var services = new ServiceCollection();
            
            // Register Services and ViewModels
            ConfigureServices(services);

            _serviceProvider = services.BuildServiceProvider();
        }

        /// <summary>
        /// Configures the dependency injection container with all required services.
        /// </summary>
        private static void ConfigureServices(IServiceCollection services)
        {
            // Database Context - Scoped per request, but for desktop usually we can treat as Singleton if thread safe, 
            // but EF Core recommends transient or scoped to avoid change tracker conflicts. 
            // We use a factory pattern via constructor injection in App.xaml.cs specifically for the DbContext lifetime here 
            // because we want to run Migrations synchronously at startup, which requires direct access.
            services.AddDbContext<TekTomatoDbContext>(options =>
                options.UseSqlite(BuildConnection()));

            // Services
            services.AddSingleton<SettingsService>();
            services.AddSingleton<SessionService>();
            services.AddSingleton<SoundService>();
            services.AddSingleton<ThemeService>();
            services.AddSingleton<TimerEngine>();

            // ViewModels (Singletons for the duration of the app life)
            services.AddSingleton<OverlayViewModel>();
            services.AddSingleton<SettingsViewModel>();
            services.AddSingleton<StatsViewModel>();

            // Views
            services.AddSingleton<OverlayWindow>();
        }

        /// <summary>
        /// Builds the connection string for SQLite based on user's AppData folder.
        /// </summary>
        private static string BuildConnection()
        {
            var basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TekTomato");
            
            // Ensure directory exists before attempting to open DB
            if (!Directory.Exists(basePath))
            {
                Directory.CreateDirectory(basePath);
            }

            return $"Data Source={basePath}\\tektomato.db";
        }

        /// <summary>
        /// Handles Application startup. Applies migrations and shows the main window.
        /// </summary>
        private void OnStartup(object sender, StartupEventArgs e)
        {
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    // Run Migrations automatically on launch
                    var context = scope.ServiceProvider.GetRequiredService<TekTomatoDbContext>();
                    
                    // Create directory manually before migration just in case connection string creation wasn't enough for DB file lock reasons
                    var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TekTomato");
                    if (!Directory.Exists(dbPath)) Directory.CreateDirectory(dbPath);

                    context.Database.Migrate();
                }
            }
            catch (Exception ex)
            {
                // Log error or show dialog. For now, standard WPF error handling applies.
                MessageBox.Show($"Failed to initialize database: {ex.Message}", "TekTomato Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
                return;
            }

            // Resolve and Show Main Window
            var overlayWindow = _serviceProvider.GetRequiredService<OverlayWindow>();
            
            // Ensure the OverlayWindow is configured correctly by the ViewModel/Service (e.g. dragging logic)
            // The ThemeService might need to apply initial theme here if not done via App.xaml default
            var themeService = _serviceProvider.GetRequiredService<ThemeService>();
            themeService.ApplyCurrentTheme();

            overlayWindow.Show();
        }

        /// <summary>
        /// Handles Application exit cleanup.
        /// </summary>
        private void OnExit(object sender, ExitEventArgs e)
        {
            // Clean up resources if necessary (e.g., close DB connections explicitly, though GC handles it)
            _serviceProvider.Dispose();
        }
    }
}