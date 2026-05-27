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
    }
}