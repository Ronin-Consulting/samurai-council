namespace SamurAICouncil.Web.Services;

/// <summary>
/// Service for managing light/dark theme state with localStorage persistence
/// </summary>
public class ThemeService
{
    private const string StorageKey = "theme-is-dark-mode";

    /// <summary>
    /// Event fired when the theme changes
    /// </summary>
    public event Action? OnThemeChanged;

    /// <summary>
    /// Whether dark mode is currently active
    /// </summary>
    public bool IsDarkMode { get; private set; } = true;

    /// <summary>
    /// Toggle between light and dark mode
    /// </summary>
    public void ToggleTheme()
    {
        IsDarkMode = !IsDarkMode;
        OnThemeChanged?.Invoke();
    }

    /// <summary>
    /// Set theme to a specific mode
    /// </summary>
    public void SetDarkMode(bool isDarkMode)
    {
        if (IsDarkMode != isDarkMode)
        {
            IsDarkMode = isDarkMode;
            OnThemeChanged?.Invoke();
        }
    }

    /// <summary>
    /// Initialize theme from system preference (call from JS interop)
    /// </summary>
    public void InitializeFromSystemPreference(bool systemPrefersDark)
    {
        IsDarkMode = systemPrefersDark;
        // Don't fire event during initialization
    }
}