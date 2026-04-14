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
        private ManualResetEventSlim _pauseEvent = new ManualResetEventSlim(true);
        private Stopwatch _sw = new Stopwatch();

        private int _delay = 100;
        public int Delay { get => _delay; set { _delay = value; OnPropChanged(); } }

        private int _dataCount = 100;
        public int DataCount { get => _dataCount; set { _dataCount = value; OnPropChanged(); } }

        private string _sortTime = "00:00:000";
        public string SortTime { get => _sortTime; set { _sortTime = value; OnPropChanged(); } }

        private bool _isSorting = false;
        public bool IsSorting { get => _isSorting; set { _isSorting = value; OnPropChanged(); } }

        private bool _isPaused = false;
        public bool IsPaused { get => _isPaused; set { _isPaused = value; OnPropChanged(); } }

        private bool _isSoundEnabled = true;
        public bool IsSoundEnabled { get => _isSoundEnabled; set { _isSoundEnabled = value; OnPropChanged(); } }

        public List<Brush> AvailableColors { get; } = new() { Brushes.SkyBlue, Brushes.Orange, Brushes.MediumPurple, Brushes.Coral, Brushes.LightGreen };
        private Brush _selectedColor = Brushes.SkyBlue;
        public Brush SelectedColor { get => _selectedColor; set { _selectedColor = value; ResetItemsColor(); OnPropChanged(); } }

        public List<string> Algorithms { get; } = new() { "Bubble Sort", "Parallel Merge Sort" };
        public string SelectedAlgorithm { get; set; } = "Parallel Merge Sort";

        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropChanged([CallerMemberName] string p = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));

        private async Task CheckPause()
        {
            if (!IsPaused) return;
            await Task.Run(() => _pauseEvent.Wait());
        }

        public void TogglePause()
        {
            if (!IsSorting) return;
            IsPaused = !IsPaused;
            if (IsPaused) { _pauseEvent.Reset(); _sw.Stop(); }
            else { _pauseEvent.Set(); _sw.Start(); }
        }

        private void PlayTone(double value)
        {
            if (!IsSoundEnabled || !IsSorting) return;
            double frequency = 300 + (value * 2);
            int toneDuration = Math.Clamp(Delay / 2, 10, 50);
            Task.Run(() => Helpers.SoundHelper.PlaySineTone(frequency, toneDuration, 0.1));
        }

        private async Task PlaySuccessMelody(CancellationToken token)
        {
            double[] successNotes = { 523.25, 659.25, 783.99, 1046.50 };
            foreach (var freq in successNotes)
            {
                if (token.IsCancellationRequested) return;
                _ = Task.Run(() => Helpers.SoundHelper.PlaySineTone(freq, 200, 0.2));
                App.Current.Dispatcher.Invoke(() => { foreach (var item in Items) item.Color = Brushes.White; });
                await Task.Delay(70);
                App.Current.Dispatcher.Invoke(() => { foreach (var item in Items) item.Color = SelectedColor; });
                await Task.Delay(100);
            }
        }

        public void GenerateItems()
        {
            StopSort();
            Items.Clear();
            SortTime = "00:00:000";
            Random rng = new Random();
            for (int i = 0; i < DataCount; i++)
                Items.Add(new VisualElement { Value = rng.Next(10, 550), Color = SelectedColor });
        }

        // To jest metoda, której brakowało w Twoim błędzie:
        public void StopSort()
        {
            _cts?.Cancel();
            _pauseEvent.Set();
            IsPaused = false;
            IsSorting = false;
            _sw.Reset();
        }

        public async Task StartSort()
        {
            if (IsSorting && IsPaused) { TogglePause(); return; }
            if (IsSorting) return;

            IsSorting = true;
            IsPaused = false;
            _pauseEvent.Set();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            _sw.Restart();

            try
            {
                _ = Task.Run(async () => {
                    while (!token.IsCancellationRequested && IsSorting)
                    {
                        SortTime = _sw.Elapsed.ToString(@"ss\:fff") + " ms";
                        await Task.Delay(50);
                    }
                }, token);

                if (SelectedAlgorithm == "Bubble Sort") await BubbleSort(token);
                else await Task.Run(async () => await ParallelMergeSort(0, Items.Count - 1, token), token);

                if (!token.IsCancellationRequested)
                {
                    _sw.Stop();
                    SortTime = _sw.Elapsed.ToString(@"ss\:fff") + " ms";
                    await PlaySuccessMelody(token);
                }
            }
            catch (OperationCanceledException) { }
            finally { IsSorting = false; ResetItemsColor(); }
        }

        private async Task BubbleSort(CancellationToken token)
        {
            for (int i = 0; i < Items.Count; i++)
            {
                for (int j = 0; j < Items.Count - 1 - i; j++)
                {
                    if (token.IsCancellationRequested) return;
                    UpdateColor(j, j + 1, Brushes.White);
                    if (Items[j].Value > Items[j + 1].Value)
                    {
                        (Items[j].Value, Items[j + 1].Value) = (Items[j + 1].Value, Items[j].Value);
                        PlayTone(Items[j].Value);
                    }
                    await Task.Delay(Delay, token);
                    await CheckPause();
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
                UpdateColor(i, j, Brushes.White);
                await Task.Delay(Delay, t);
                await CheckPause();
                if (Items[i].Value <= Items[j].Value) temp.Add(Items[i++].Value);
                else temp.Add(Items[j++].Value);
                UpdateColor(i - 1, j - 1, SelectedColor);
            }
            while (i <= m) temp.Add(Items[i++].Value);
            while (j <= r) temp.Add(Items[j++].Value);
            for (int k = 0; k < temp.Count; k++)
            {
                if (t.IsCancellationRequested) return;
                int idx = l + k;
                App.Current.Dispatcher.Invoke(() => {
                    Items[idx].Value = temp[k];
                    Items[idx].Color = Brushes.WhiteSmoke;
                });
                PlayTone(temp[k]);
                await Task.Delay(Delay, t);
                await CheckPause();
                App.Current.Dispatcher.Invoke(() => Items[idx].Color = SelectedColor);
            }
        }

        private void UpdateColor(int i, int j, Brush c) => App.Current.Dispatcher.Invoke(() => {
            if (i >= 0 && i < Items.Count) Items[i].Color = c;
            if (j >= 0 && j < Items.Count) Items[j].Color = c;
        });

        private void ResetItemsColor() { foreach (var item in Items) item.Color = SelectedColor; }
    }
}