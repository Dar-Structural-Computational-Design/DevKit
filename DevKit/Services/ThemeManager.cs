using System;
using System.Windows;

namespace DevKit.Services
{
    public static class ThemeManager
    {
        public static bool IsDark { get; private set; } = true;

        private static readonly Uri DarkUri = new Uri("pack://application:,,,/DevKit;component/Helpers/DarkTheme.xaml");
        private static readonly Uri LightUri = new Uri("pack://application:,,,/DevKit;component/Helpers/LightTheme.xaml");

        public static void ApplyTheme(bool dark)
        {
            IsDark = dark;
            var app = Application.Current;
            if (app == null) return;

            // Remove existing theme dictionaries
            for (int i = app.Resources.MergedDictionaries.Count - 1; i >= 0; i--)
            {
                var d = app.Resources.MergedDictionaries[i];
                if (d.Source != null && (d.Source == DarkUri || d.Source == LightUri))
                    app.Resources.MergedDictionaries.RemoveAt(i);
            }

            app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = dark ? DarkUri : LightUri });
        }

        /// <summary>
        /// Applies theme to a specific window's resources (for modeless windows in Revit).
        /// </summary>
        public static void ApplyToWindow(Window window, bool dark)
        {
            IsDark = dark;
            window.Resources.MergedDictionaries.Clear();
            window.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = dark ? DarkUri : LightUri });
        }

        public static void Toggle(Window window)
        {
            ApplyToWindow(window, !IsDark);
        }
    }
}
