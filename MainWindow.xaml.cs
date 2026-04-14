using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using AlgorithmVisualizer.View;

namespace AlgorithmVisualizer
{
    public partial class MainWindow : Window
    {
        public MainViewModel ViewModel { get; set; } = new MainViewModel();

        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = ViewModel;
            ViewModel.GenerateItems();
        }

        private void Generate_Click(object sender, RoutedEventArgs e) => ViewModel.GenerateItems();
        private async void Start_Click(object sender, RoutedEventArgs e) => await ViewModel.StartSort();
        private void Stop_Click(object sender, RoutedEventArgs e) => ViewModel.StopSort();
    }

    public class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool b = (bool)value;
            if (targetType == typeof(Visibility))
                return (parameter?.ToString() == "CollapseToHidden" && b) ? Visibility.Collapsed : (b ? Visibility.Collapsed : Visibility.Visible);
            return !b;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}