using System;
using System.Windows;
using AlgorithmVisualizer.View; // Tu masz MainViewModel.cs

namespace AlgorithmVisualizer
{
    public partial class MainWindow : Window
    {
        public MainViewModel ViewModel { get; set; } = new MainViewModel();

        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = ViewModel;

            this.Closing += (s, e) => ViewModel.Dispose();
        }

        private void Generate_Click(object sender, RoutedEventArgs e) => ViewModel.GenerateItems();
        private async void Start_Click(object sender, RoutedEventArgs e) => await ViewModel.StartSort();
        private void Pause_Click(object sender, RoutedEventArgs e) => ViewModel.TogglePause();

        private void Master_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.SetMasterRole();
            RoleOverlay.Visibility = Visibility.Collapsed;
        }

        private void Worker_Click(object sender, RoutedEventArgs e)
        {
            WorkerPanel.Visibility = Visibility.Visible;
        }

        private void Connect_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(IpTextBox.Text)) return;
            ViewModel.SetWorkerRole(IpTextBox.Text.Trim());
            RoleOverlay.Visibility = Visibility.Collapsed;
        }
    }
}