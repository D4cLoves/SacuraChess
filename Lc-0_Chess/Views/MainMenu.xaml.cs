using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace Lc_0_Chess.Views
{
    /// <summary>
    /// Логика взаимодействия для MainMenu.xaml
    /// </summary>
    public partial class MainMenu : Window
    {
        public MainMenu()
        {
            InitializeComponent();
        }

        private void NewGameButton_Click(object sender, RoutedEventArgs e)
        {
            var gameSetup = new GameSetup("Classic");
            gameSetup.Show();
            Close();
        }

        //private void SettingsButton_Click(object sender, RoutedEventArgs e)
        //{
        //    MessageBox.Show("Настройки пока в разработке!", "Sakura Chess", MessageBoxButton.OK, MessageBoxImage.Information);
        //}

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}
