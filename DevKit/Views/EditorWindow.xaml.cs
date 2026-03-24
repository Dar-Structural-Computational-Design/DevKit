using System.Collections.Specialized;
using System.Windows;
using System.Windows.Input;
using DevKit.Services;
using DevKit.ViewModels;

namespace DevKit.Views
{
    public partial class EditorWindow : Window
    {
        public EditorWindow()
        {
            InitializeComponent();
            DataContext = new EditorViewModel();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            txtCode.Focus();
            txtCode.CaretIndex = txtCode.Text?.Length ?? 0;
            var vm = DataContext as EditorViewModel;
            if (vm == null) return;
            pwdApiKey.Password = vm.ClaudeApiKey ?? "";
            if (vm.ChatHistory is INotifyCollectionChanged col)
                col.CollectionChanged += (s, ev) => { if (lstChat.Items.Count > 0) lstChat.ScrollIntoView(lstChat.Items[lstChat.Items.Count - 1]); };
        }

        private void BtnSaveApiKey_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as EditorViewModel;
            if (vm == null) return;
            vm.ClaudeApiKey = pwdApiKey.Password;
            vm.SaveApiKeyCommand.Execute(null);
        }

        private void BtnToggleTheme_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as EditorViewModel;
            if (vm == null) return;
            vm.ToggleThemeCommand.Execute(this);
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            var vm = DataContext as EditorViewModel;
            if (vm == null) return;
            if (e.Key == Key.F5) { vm.TestCommand.Execute(null); e.Handled = true; }
            else if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control) { vm.AddCommand.Execute(null); e.Handled = true; }
            else if (e.Key == Key.L && Keyboard.Modifiers == ModifierKeys.Control) { vm.ClearCommand.Execute(null); e.Handled = true; }
        }
    }
}
