using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace AlgorithmVisualizer.Model
{
    public class VisualElement : INotifyPropertyChanged
    {
        private double _value;
        private Brush _color;

        public double Value { get => _value; set { _value = value; OnPropChanged(); } }
        public Brush Color { get => _color; set { _color = value; OnPropChanged(); } }

        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropChanged([CallerMemberName] string p = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }
}