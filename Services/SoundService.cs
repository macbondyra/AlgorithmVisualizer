using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using AlgorithmVisualizer.Model;

namespace AlgorithmVisualizer.Services
{
    public static class SoundService
    {
        public static void PlayTone(double value, bool isSoundEnabled, bool isSorting, int delay)
        {
            if (!isSoundEnabled || !isSorting) return;
            double frequency = 300 + (value * 2);
            int toneDuration = Math.Max(10, Math.Min(50, delay / 2));
            Task.Run(() => Helpers.SoundHelper.PlaySineTone(frequency, toneDuration, 0.1));
        }

        public static async Task PlaySuccessMelody(ObservableCollection<VisualElement> items, Brush selectedColor, CancellationToken token)
        {
            double[] successNotes = { 523.25, 659.25, 783.99, 1046.50 };
            foreach (var freq in successNotes)
            {
                if (token.IsCancellationRequested) return;
                _ = Task.Run(() => Helpers.SoundHelper.PlaySineTone(freq, 200, 0.2));
                Application.Current.Dispatcher.Invoke(() => { foreach (var item in items) item.Color = Brushes.White; });
                await Task.Delay(70);
                Application.Current.Dispatcher.Invoke(() => { foreach (var item in items) item.Color = selectedColor; });
                await Task.Delay(100);
            }
        }
    }
}