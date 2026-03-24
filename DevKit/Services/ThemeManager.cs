using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using DevKit.Models;

namespace DevKit.Services
{
    public static class ThemeManager
    {
        public static string CurrentThemeKey { get; private set; } = "Dark";

        private static readonly Dictionary<string, Uri> ThemeUris = new Dictionary<string, Uri>
        {
            ["Dark"]      = new Uri("pack://application:,,,/DevKit;component/Helpers/DarkTheme.xaml"),
            ["Light"]     = new Uri("pack://application:,,,/DevKit;component/Helpers/LightTheme.xaml"),
            ["Minimal"]   = new Uri("pack://application:,,,/DevKit;component/Helpers/MinimalTheme.xaml"),
            ["Ocean"]     = new Uri("pack://application:,,,/DevKit;component/Helpers/OceanTheme.xaml"),
            ["Nord"]      = new Uri("pack://application:,,,/DevKit;component/Helpers/NordTheme.xaml"),
            ["Solarized"] = new Uri("pack://application:,,,/DevKit;component/Helpers/SolarizedTheme.xaml"),
            ["Monokai"]   = new Uri("pack://application:,,,/DevKit;component/Helpers/MonokaiTheme.xaml"),
            ["Dracula"]   = new Uri("pack://application:,,,/DevKit;component/Helpers/DraculaTheme.xaml"),
        };

        public static List<ThemeInfo> GetAllThemes()
        {
            return new List<ThemeInfo>
            {
                new ThemeInfo { Key = "Dark",      Name = "Dark",      BgColor = C("#1E1E2E"), FgColor = C("#CDD6F4"), AccentColor = C("#7AA2F7"), CardColor = C("#313244"), BorderColor = C("#45475A") },
                new ThemeInfo { Key = "Light",     Name = "Light",     BgColor = C("#F5F5F7"), FgColor = C("#1E1E2E"), AccentColor = C("#4A6CF7"), CardColor = C("#EEEEF2"), BorderColor = C("#D4D4DC") },
                new ThemeInfo { Key = "Minimal",   Name = "Minimal",   BgColor = C("#F8F8FA"), FgColor = C("#2C2C34"), AccentColor = C("#5B6770"), CardColor = C("#F0F0F3"), BorderColor = C("#E2E2E8") },
                new ThemeInfo { Key = "Ocean",     Name = "Ocean",     BgColor = C("#0B1929"), FgColor = C("#C8DCF0"), AccentColor = C("#4A9FD9"), CardColor = C("#132D4A"), BorderColor = C("#1E3A5F") },
                new ThemeInfo { Key = "Nord",      Name = "Nord",      BgColor = C("#2E3440"), FgColor = C("#ECEFF4"), AccentColor = C("#88C0D0"), CardColor = C("#434C5E"), BorderColor = C("#4C566A") },
                new ThemeInfo { Key = "Solarized", Name = "Solarized", BgColor = C("#002B36"), FgColor = C("#93A1A1"), AccentColor = C("#268BD2"), CardColor = C("#0A4050"), BorderColor = C("#2A5460") },
                new ThemeInfo { Key = "Monokai",   Name = "Monokai",   BgColor = C("#272822"), FgColor = C("#F8F8F2"), AccentColor = C("#A6E22E"), CardColor = C("#3E3D32"), BorderColor = C("#4E4F43") },
                new ThemeInfo { Key = "Dracula",   Name = "Dracula",   BgColor = C("#282A36"), FgColor = C("#F8F8F2"), AccentColor = C("#BD93F9"), CardColor = C("#44475A"), BorderColor = C("#44475A") },
            };
        }

        public static void ApplyToWindow(Window window, string themeKey)
        {
            if (!ThemeUris.ContainsKey(themeKey)) return;
            CurrentThemeKey = themeKey;
            var uris = new HashSet<Uri>(ThemeUris.Values);
            for (int i = window.Resources.MergedDictionaries.Count - 1; i >= 0; i--)
            {
                var d = window.Resources.MergedDictionaries[i];
                if (d.Source != null && uris.Contains(d.Source))
                    window.Resources.MergedDictionaries.RemoveAt(i);
            }
            window.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = ThemeUris[themeKey] });
        }

        private static Color C(string hex)
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }
    }
}
