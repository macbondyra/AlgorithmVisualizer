using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using AlgorithmVisualizer.Model;

namespace AlgorithmVisualizer.View
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<VisualElement> Items { get; set; } = new();
        private CancellationTokenSource _cts;

        // --- Właściwości Sterujące ---
        private int _delay = 20;
        public int Delay { get => _delay; set { _delay = value; OnPropChanged(); } }

        private int _dataCount = 100;
        public int DataCount { get => _dataCount; set { _dataCount = value; OnPropChanged(); } }

        private string _sortTime = "00:00:000";
        public string SortTime { get => _sortTime; set { _sortTime = value; OnPropChanged(); } }

        private bool _isSorting = false;
        public bool IsSorting { get => _isSorting; set { _isSorting = value; OnPropChanged(); } }

        private bool _isSoundEnabled = true;
        public bool IsSoundEnabled { get => _isSoundEnabled; set { _isSoundEnabled = value; OnPropChanged(); } }

        // --- Wybór Koloru i Algorytmu ---
        public List<Brush> AvailableColors { get; } = new() { Brushes.SkyBlue, Brushes.Orange, Brushes.MediumPurple, Brushes.Coral, Brushes.LightGreen };
        private Brush _selectedColor = Brushes.SkyBlue;
        public Brush SelectedColor { get => _selectedColor; set { _selectedColor = value; ResetItemsColor(); OnPropChanged(); } }

        public List<string> Algorithms { get; } = new() { "Bubble Sort", "Parallel Merge Sort" };
        public string SelectedAlgorithm { get; set; } = "Parallel Merge Sort";

        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropChanged([CallerMemberName] string p = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));

        // --- Synchronizowany Dźwięk ---
        private void PlayTone(double value)
        {
            if (!IsSoundEnabled || !IsSorting) return;

            double frequency = 300 + (value * 2);
            // Dźwięk trwa tyle, co opóźnienie (ale nie za długo, by nie trzeszczało)
            int toneDuration = Math.Clamp(Delay, 5, 50);

            Task.Run(() => Helpers.SoundHelper.PlaySineTone(frequency, toneDuration, 0.1));
        }

        // --- Logika Aplikacji ---
        public void GenerateItems()
        {
            StopSort();
            Items.Clear();
            SortTime = "00:00:000";
            Random rng = new Random();
            for (int i = 0; i < DataCount; i++)
                Items.Add(new VisualElement { Value = rng.Next(10, 550), Color = SelectedColor });
        }

        public void StopSort() { _cts?.Cancel(); IsSorting = false; }

        private void ResetItemsColor() { foreach (var item in Items) item.Color = SelectedColor; }

        public async Task StartSort()
        {
            if (IsSorting) return;
            IsSorting = true;
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            Stopwatch sw = Stopwatch.StartNew();

            try
            {
                _ = Task.Run(async () => {
                    while (!token.IsCancellationRequested && IsSorting)
                    {
                        SortTime = sw.Elapsed.ToString(@"ss\:fff") + " ms";
                        await Task.Delay(50);
                    }
                }, token);

                if (SelectedAlgorithm == "Bubble Sort") await BubbleSort(token);
                else await Task.Run(async () => await ParallelMergeSort(0, Items.Count - 1, token), token);
            }
            catch (OperationCanceledException) { }
            finally { sw.Stop(); IsSorting = false; ResetItemsColor(); SortTime = sw.Elapsed.ToString(@"ss\:fff") + " ms"; }
        }

        // --- Algorytmy ---
        private async Task BubbleSort(CancellationToken token)
        {
            for (int i = 0; i < Items.Count; i++)
            {
                for (int j = 0; j < Items.Count - 1 - i; j++)
                {
                    if (token.IsCancellationRequested) return;
                    UpdateColor(j, j + 1, Brushes.Red);
                    if (Items[j].Value > Items[j + 1].Value)
                    {
                        (Items[j].Value, Items[j + 1].Value) = (Items[j + 1].Value, Items[j].Value);
                        PlayTone(Items[j].Value);
                        await Task.Delay(Delay, token); // Synchronizacja suwakiem
                    }
                    UpdateColor(j, j + 1, SelectedColor);
                }
            }
        }

        private async Task ParallelMergeSort(int l, int r, CancellationToken t)
        {
            if (t.IsCancellationRequested || l >= r) return;
            int m = (l + r) / 2;
            if (r - l > 15) await Task.WhenAll(ParallelMergeSort(l, m, t), ParallelMergeSort(m + 1, r, t));
            else { await ParallelMergeSort(l, m, t); await ParallelMergeSort(m + 1, r, t); }
            await Merge(l, m, r, t);
        }

        private async Task Merge(int l, int m, int r, CancellationToken t)
        {
            List<double> temp = new();
            int i = l, j = m + 1;
            while (i <= m && j <= r)
            {
                if (t.IsCancellationRequested) return;
                if (Items[i].Value <= Items[j].Value) temp.Add(Items[i++].Value);
                else temp.Add(Items[j++].Value);
            }
            while (i <= m) temp.Add(Items[i++].Value);
            while (j <= r) temp.Add(Items[j++].Value);

            for (int k = 0; k < temp.Count; k++)
            {
                if (t.IsCancellationRequested) return;
                int idx = l + k;
                App.Current.Dispatcher.Invoke(() => {
                    Items[idx].Value = temp[k];
                    Items[idx].Color = Brushes.LightGreen;
                });
                PlayTone(temp[k]);
                await Task.Delay(Delay, t); // Synchronizacja suwakiem
                App.Current.Dispatcher.Invoke(() => Items[idx].Color = SelectedColor);
            }
        }

        private void UpdateColor(int i, int j, Brush c) => App.Current.Dispatcher.Invoke(() => {
            if (i >= 0 && i < Items.Count) Items[i].Color = c;
            if (j >= 0 && j < Items.Count) Items[j].Color = c;
        });
    }
}