using System.Windows;
using Aurora.Core.Interfaces;
using Aurora.Core.Settings;

namespace Aurora.Admin;

public partial class AdminPanelWindow : Window
{
    private readonly IAppSettings _settings;
    private readonly ISettingsWriter _writer;
    private readonly IModelManager _modelManager;

    public AdminPanelWindow(IAppSettings settings, ISettingsWriter writer, IModelManager modelManager)
    {
        _settings = settings;
        _writer = writer;
        _modelManager = modelManager;
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        OverlayBgColorBox.Text = _settings.OverlayBackgroundColor;
        OverlayFgColorBox.Text = _settings.OverlayForegroundColor;
        PrivateDictBox.Text = _settings.PrivateDictionaryPath;
        GenericDictBox.Text = _settings.GenericDictionaryPath;
        UpdateChannelBox.Text = _settings.UpdateChannel;
        ModelCachePathBox.Text = _settings.ModelCachePath;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var data = new SettingsData
        {
            SourceLanguage = _settings.SourceLanguage,
            TargetLanguage = _settings.TargetLanguage,
            HotkeyTrigger = _settings.HotkeyTrigger,
            HotkeyRullo = _settings.HotkeyRullo,
            ModelCachePath = ModelCachePathBox.Text.Trim(),
            OverlayDismissTimeoutSeconds = (int)_settings.OverlayDismissTimeout.TotalSeconds,
            HoverDwellThreshold = _settings.HoverDwellThreshold,
            RulloSamplingInterval = _settings.RulloSamplingInterval,
            OverlayBackgroundColor = OverlayBgColorBox.Text.Trim(),
            OverlayForegroundColor = OverlayFgColorBox.Text.Trim(),
            PrivateDictionaryPath = PrivateDictBox.Text.Trim(),
            GenericDictionaryPath = GenericDictBox.Text.Trim(),
            UpdateChannel = UpdateChannelBox.Text.Trim(),
        };
        _writer.Save(data);
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => Close();

    private async void DownloadModel_Click(object sender, RoutedEventArgs e)
    {
        DownloadModelButton.IsEnabled = false;
        try
        {
            await _modelManager.EnsureLoadedAsync();
            MessageBox.Show("Model ready.", "Download Model", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Model load failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { DownloadModelButton.IsEnabled = true; }
    }
}
