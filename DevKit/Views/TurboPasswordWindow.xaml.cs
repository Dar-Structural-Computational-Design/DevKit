using System.Windows;
using System.Windows.Input;

namespace DevKit.Views
{
    public partial class TurboPasswordWindow : Window
    {
        public string Password { get; private set; } = "";

        public TurboPasswordWindow()
        {
            InitializeComponent();
            Loaded += (s, e) => pwdBox.Focus();
        }

        private void BtnActivate_Click(object sender, RoutedEventArgs e)
        {
            Password = pwdBox.Password ?? "";
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void PwdBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                BtnActivate_Click(sender, e);
                e.Handled = true;
            }
        }
    }
}
