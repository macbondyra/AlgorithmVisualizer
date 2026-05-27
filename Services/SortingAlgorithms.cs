using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using AlgorithmVisualizer.Model;

namespace AlgorithmVisualizer.Services
{
    public class SortingAlgorithms
    {
        private readonly ObservableCollection<VisualElement> _items;
        private readonly int _delay;
        private readonly Brush _selectedColor;
        private readonly Action<int, int, Brush> _updateColor;
        private readonly Action<double> _playTone;
        private readonly Func<Task> _checkPause;

        public int ItemsCount => _items.Count;

        public SortingAlgorithms(
            ObservableCollection<VisualElement> items,
            int delay,
            Brush selectedColor,
            Action<int, int, Brush> updateColor,
            Action<double> playTone,
            Func<Task> checkPause)
        {
            _items = items;
            _delay = delay;
            _selectedColor = selectedColor;
            _updateColor = updateColor;
            _playTone = playTone;
            _checkPause = checkPause;
        }

        public List<double> GetCurrentData()
        {
            return _items.Select(i => i.Value).ToList();
        }

        public void UpdateValuesFromNetwork(int offset, List<double> currentData)
        {
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                for (int i = 0; i < currentData.Count; i++)
                {
                    int targetIndex = offset + i;
                    if (targetIndex < _items.Count) _items[targetIndex].Value = currentData[i];
                }
            });
        }

        public async Task MergeSortedChunks(List<List<double>> sortedChunks, CancellationToken token)
        {
            int currentPos = 0;
            foreach (var chunk in sortedChunks)
            {
                for (int i = 0; i < chunk.Count; i++)
                {
                    int targetIndex = currentPos + i;
                    if (targetIndex < _items.Count)
                    {
                        Application.Current.Dispatcher.Invoke(() => _items[targetIndex].Value = chunk[i]);
                    }
                }
                currentPos += chunk.Count;
            }

            if (sortedChunks.Count > 1)
            {
                int totalMergedSize = sortedChunks[0].Count;
                for (int i = 1; i < sortedChunks.Count; i++)
                {
                    if (token.IsCancellationRequested) break;
                    int leftStart = 0;
                    int mid = totalMergedSize - 1;
                    int rightEnd = mid + sortedChunks[i].Count;
                    await Merge(leftStart, mid, rightEnd, token);
                    totalMergedSize += sortedChunks[i].Count;
                }
            }
        }

        public async Task BubbleSort(CancellationToken token)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                for (int j = 0; j < _items.Count - 1 - i; j++)
                {
                    if (token.IsCancellationRequested) return;
                    _updateColor(j, j + 1, Brushes.White);
                    if (_items[j].Value > _items[j + 1].Value)
                    {
                        (_items[j].Value, _items[j + 1].Value) = (_items[j + 1].Value, _items[j].Value);
                        _playTone(_items[j].Value);
                    }
                    await Task.Delay(_delay, token);
                    await _checkPause();
                    _updateColor(j, j + 1, _selectedColor);
                }
            }
        }

        public async Task ParallelMergeSort(int l, int r, CancellationToken t)
        {
            if (t.IsCancellationRequested || l >= r) return;
            int m = (l + r) / 2;
            if (r - l > 15) await Task.WhenAll(ParallelMergeSort(l, m, t), ParallelMergeSort(m + 1, r, t));
            else { await ParallelMergeSort(l, m, t); await ParallelMergeSort(m + 1, r, t); }
            await Merge(l, m, r, t);
        }

        public async Task Merge(int l, int m, int r, CancellationToken t)
        {
            List<double> temp = new();
            int i = l, j = m + 1;
            while (i <= m && j <= r)
            {
                if (t.IsCancellationRequested) return;
                _updateColor(i, j, Brushes.White);
                await Task.Delay(_delay, t);
                await _checkPause();
                if (_items[i].Value <= _items[j].Value) temp.Add(_items[i++].Value);
                else temp.Add(_items[j++].Value);
                _updateColor(i - 1, j - 1, _selectedColor);
            }
            while (i <= m) temp.Add(_items[i++].Value);
            while (j <= r) temp.Add(_items[j++].Value);
            for (int k = 0; k < temp.Count; k++)
            {
                if (t.IsCancellationRequested) return;
                int idx = l + k;
                Application.Current.Dispatcher.Invoke(() => {
                    _items[idx].Value = temp[k];
                    _items[idx].Color = Brushes.WhiteSmoke;
                });
                _playTone(temp[k]);
                await Task.Delay(_delay, t);
                await _checkPause();
                Application.Current.Dispatcher.Invoke(() => _items[idx].Color = _selectedColor);
            }
        }
        private void ResetColors()
        {
            foreach (var item in _items) item.Color = _selectedColor;
        }   
        public async Task ParallelOddEvenSort(CancellationToken token)
        {
            int n = _items.Count;
            bool isSorted = false;

            while (!isSorted)
            {
                if (token.IsCancellationRequested) return;
                isSorted = true;

                // 1. FAZA PARZYSTA (Równoległe porównywanie par: 0-1, 2-3, 4-5...)
                List<Task<bool>> evenTasks = new();
                for (int i = 0; i < n - 1; i += 2)
                {
                    int idx = i;
                    evenTasks.Add(Task.Run(async () =>
                    {
                        bool swapped = false;
                        if (_items[idx].Value > _items[idx + 1].Value)
                        {
                            // Blokada Dispatchera tylko na czas zamiany struktur WPF
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                (_items[idx].Value, _items[idx + 1].Value) = (_items[idx + 1].Value, _items[idx].Value);
                                _items[idx].Color = Brushes.White;
                                _items[idx + 1].Color = Brushes.White;
                            });

                            _playTone(_items[idx].Value);
                            swapped = true;
                        }
                        return swapped;
                    }));
                }

                // Czekaj na zakończenie wszystkich równoległych wątków z fazy parzystej
                var evenResults = await Task.WhenAll(evenTasks);
                if (evenResults.Any(r => r == true)) isSorted = false;

                // Opóźnienie animacji po wykonaniu równoległego kroku
                await Task.Delay(_delay, token);
                await _checkPause();
                Application.Current.Dispatcher.Invoke(() => ResetColors());

                // 2. FAZA NIEPARZYSTA (Równoległe porównywanie par: 1-2, 3-4, 5-6...)
                List<Task<bool>> oddTasks = new();
                for (int i = 1; i < n - 1; i += 2)
                {
                    int idx = i;
                    oddTasks.Add(Task.Run(async () =>
                    {
                        bool swapped = false;
                        if (_items[idx].Value > _items[idx + 1].Value)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                (_items[idx].Value, _items[idx + 1].Value) = (_items[idx + 1].Value, _items[idx].Value);
                                _items[idx].Color = Brushes.White;
                                _items[idx + 1].Color = Brushes.White;
                            });

                            _playTone(_items[idx].Value);
                            swapped = true;
                        }
                        return swapped;
                    }));
                }

                var oddResults = await Task.WhenAll(oddTasks);
                if (oddResults.Any(r => r == true)) isSorted = false;

                await Task.Delay(_delay, token);
                await _checkPause();
                Application.Current.Dispatcher.Invoke(() => ResetColors());
            }
        }
        public async Task ParallelQuickSort(int low, int high, CancellationToken t)
        {
            if (t.IsCancellationRequested || low >= high) return;

            // Partycjonowanie tablicy (wybór pivota i przestawienie elementów)
            int pivotIdx = await Partition(low, high, t);

            // Warunek progowy: jeśli podtablice są duże, sortuj je równolegle na osobnych rdzeniach
            if (high - low > 15)
            {
                await Task.WhenAll(
                    ParallelQuickSort(low, pivotIdx - 1, t),
                    ParallelQuickSort(pivotIdx + 1, high, t)
                );
            }
            else
            {
                // Dla małych fragmentów sortuj sekwencyjnie krok po kroku
                await ParallelQuickSort(low, pivotIdx - 1, t);
                await ParallelQuickSort(pivotIdx + 1, high, t);
            }
        }

        private async Task<int> Partition(int low, int high, CancellationToken t)
        {
            double pivot = _items[high].Value;
            int i = low - 1;

            // Wizualizacja: zaznacz pivot kolorem (np. WhiteSmoke)
            Application.Current.Dispatcher.Invoke(() => _items[high].Color = Brushes.WhiteSmoke);

            for (int j = low; j < high; j++)
            {
                if (t.IsCancellationRequested) return low;

                _updateColor(j, Math.Max(0, i), Brushes.White);
                await Task.Delay(_delay, t);
                await _checkPause();

                if (_items[j].Value < pivot)
                {
                    i++;
                    // Zamiana wartości w wątku UI
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        (_items[i].Value, _items[j].Value) = (_items[j].Value, _items[i].Value);
                    });
                    _playTone(_items[i].Value);
                }

                _updateColor(j, Math.Max(0, i), _selectedColor);
            }

            // Umieszczenie pivota na właściwym miejscu
            Application.Current.Dispatcher.Invoke(() =>
            {
                (_items[i + 1].Value, _items[high].Value) = (_items[high].Value, _items[i + 1].Value);
                _items[high].Color = _selectedColor;
            });

            _playTone(_items[i + 1].Value);
            await Task.Delay(_delay, t);

            return i + 1;
        }
    }
}