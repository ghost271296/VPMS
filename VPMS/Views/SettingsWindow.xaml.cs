using System.Windows;
using System.Windows.Controls;
using VPMS.ViewModels;

namespace VPMS.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;

        // Pre-populate the password box (PasswordBox doesn't support binding)
        ApiKeyBox.Password = vm.ApiKey;
    }

    private void ApiKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
            vm.ApiKey = ApiKeyBox.Password;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
