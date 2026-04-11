using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using AlgorithmVisualizer.Model;
using AlgorithmVisualizer.View;

namespace AlgorithmVisualizer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainViewModel ViewModel { get; set; } = new();

        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = ViewModel;
            Random rng = new Random();

            for (int i = 1; i < 150; i++) ViewModel.Items.Add(new VisualElement { Value = rng.Next(10,200) });

            Task.Run(() => ViewModel.SortParallel());
        }
    }
}