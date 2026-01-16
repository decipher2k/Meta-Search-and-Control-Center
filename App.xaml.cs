//Meta Search and Control Center (c) 2026 Dennis Michael Heine
using System.Windows;
using MSCC.Services;

namespace MSCC
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // Sprache aus Einstellungen laden und anwenden
            SettingsService.Instance.ApplyLanguage();
        }
    }
}
