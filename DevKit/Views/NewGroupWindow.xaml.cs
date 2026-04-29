using System.Windows;
using System.Windows.Input;

namespace DevKit.Views
{
    public partial class NewGroupWindow : Window
    {
        public string GroupName { get; private set; } = "";

        public NewGroupWindow()
        {
            InitializeComponent();
            Loaded += (s, e) => txtGroupName.Focus();
        }

        private void BtnCreate_Click(object sender, RoutedEventArgs e)
        {
            string name = txtGroupName.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(name))
            {
                txtGroupName.Focus();
                return;
            }
            GroupName = name;
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void TxtGroupName_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                BtnCreate_Click(sender, e);
                e.Handled = true;
            }
        }
    }
}
