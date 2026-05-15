using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using AlgorithmVisualizer.View;

namespace AlgorithmVisualizer.Services
{
    public class SortingService
    {
        private readonly MainViewModel _vm;
        private readonly ManualResetEventSlim _pauseEvent;

        public SortingService(MainViewModel vm, ManualResetEventSlim pauseEvent)
        {
            _vm = vm;
            _pauseEvent = pauseEvent;
        }

        private async Task CheckPause()
        {
            if (!_vm.IsPaused) return;
            await Task.Run(() => _pauseEvent.Wait());
        }

        private void UpdateColor(int i, int j, Brush c) => Application.Current.Dispatcher.Invoke(() => {
            if (i >= 0 && i < _vm.Items.Count) _vm.Items[i].Color = c;
            if (j >= 0 && j < _vm.Items.Count) _vm.Items[j].Color = c;
        });

        private SortingAlgorithms CreateAlgorithmsContext()
        {
            return new SortingAlgorithms(
                _vm.Items,
                _vm.Delay,
                _vm.SelectedColor,
                UpdateColor,
                (val) => SoundService.PlayTone(val, _vm.IsSoundEnabled, _vm.IsSorting, _vm.Delay),
                CheckPause
            );
        }

        public async Task BubbleSort(CancellationToken token)
        {
            var alg = CreateAlgorithmsContext();
            await alg.BubbleSort(token);
        }

        public async Task ParallelMergeSort(int l, int r, CancellationToken t)
        {
            var alg = CreateAlgorithmsContext();
            await alg.ParallelMergeSort(l, r, t);
        }

        public async Task DistributedSort(List<double> data, DistributedSortService distributedSortService, CancellationToken token)
        {
            var alg = CreateAlgorithmsContext();
            await alg.DistributedSort(data, distributedSortService, token);
        }
    }
}