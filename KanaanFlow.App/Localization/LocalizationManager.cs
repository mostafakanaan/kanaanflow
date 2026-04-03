namespace KanaanFlow.App.Localization;

using System.ComponentModel;
using System.Globalization;

public sealed class LocalizationManager : INotifyPropertyChanged
{
    private static readonly Lazy<LocalizationManager> LazyInstance = new(() => new LocalizationManager());
    public static LocalizationManager Instance => LazyInstance.Value;

    private string currentLanguage = "en";

    private LocalizationManager()
    {
    }

    public string CurrentLanguage => currentLanguage;

    public string this[string key] => AppStrings.Get(key, currentLanguage);

    public bool IsRtl => currentLanguage == "ar";

    public event PropertyChangedEventHandler? PropertyChanged;

    public void SetLanguage(string language)
    {
        if (currentLanguage == language)
            return;

        currentLanguage = language;

        CultureInfo culture = new(language);
        CultureInfo.CurrentUICulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;

        Preferences.Set("AppLanguage", language);

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }

    public void LoadSavedLanguage()
    {
        string saved = Preferences.Get("AppLanguage", "en");
        SetLanguage(saved);
    }
}
