using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using AlgorithmVisualizer.Model;
using AlgorithmVisualizer.Services;

namespace AlgorithmVisualizer.View
{
    public class MainViewModel : INotifyPropertyChanged, IDisposable
    {
        public ObservableCollection<VisualElement> Items { get; set; } = new();
        private CancellationTokenSource _cts;
        private ManualResetEventSlim _pauseEvent = new ManualResetEventSlim(true);
        private Stopwatch _sw = new Stopwatch();

        private readonly DistributedSortService _distributedSortService;
        private readonly SortingService _sortingService;
        private readonly WorkerNetworkService _workerNetworkService;

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

        private int _connectedWorkersCount = 0;
        public int ConnectedWorkersCount { get => _connectedWorkersCount; set { _connectedWorkersCount = value; OnPropChanged(); } }

        // CZYSZCZENIE: Usunięto sztuczny "Distributed Merge Sort" z listy UI
        public List<string> Algorithms { get; } = new() { "Bubble Sort", "Parallel Merge Sort" };
        public string SelectedAlgorithm { get; set; } = "Parallel Merge Sort";

        private bool _isMaster = true;
        public bool IsMaster { get => _isMaster; set { _isMaster = value; OnPropChanged(); OnPropChanged(nameof(IsWorker)); } }
        public bool IsWorker => !_isMaster;

        private string _workerStatus = "Wybierz rolę...";
        public string WorkerStatus { get => _workerStatus; set { _workerStatus = value; OnPropChanged(); } }

        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropChanged([CallerMemberName] string p = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));

        public MainViewModel()
        {
            _distributedSortService = new DistributedSortService();
            _distributedSortService.WorkersChanged += () => ConnectedWorkersCount = _distributedSortService.ConnectedWorkers;

            _sortingService = new SortingService(this, _pauseEvent);
            _workerNetworkService = new WorkerNetworkService(this, _sortingService, _pauseEvent);
        }

        public void SetMasterRole()
        {
            IsMaster = true;
            _distributedSortService.StartListening();
            GenerateItems();
        }

        public void SetWorkerRole(string ip)
        {
            IsMaster = false;
            Items.Clear();
            _ = _workerNetworkService.StartWorkerLoop(ip, token => _cts = token);
        }

        public void TogglePause()
        {
            if (!IsSorting) return;
            IsPaused = !IsPaused;
            if (IsPaused) { _pauseEvent.Reset(); _sw.Stop(); }
            else { _pauseEvent.Set(); _sw.Start(); }
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

        public void StopSort()
        {
            _cts?.Cancel();
            _pauseEvent.Set();
            IsPaused = false;
            IsSorting = false;
            ResetItemsColor();
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

                // Wybór strategii (Automatycznie opakowuje w dekorator sieciowy, gdy są workerzy)
                ISortingStrategy algorithm = _sortingService.GetAlgorithm(SelectedAlgorithm, _distributedSortService);
                await Task.Run(async () => await algorithm.SortAsync(token), token);

                if (!token.IsCancellationRequested)
                {
                    _sw.Stop();
                    SortTime = _sw.Elapsed.ToString(@"ss\:fff") + " ms";
                    await SoundService.PlaySuccessMelody(Items, SelectedColor, token);
                }
            }
            catch (OperationCanceledException) { /* Ignored */ }
            catch (Exception ex)
            {
                MessageBox.Show($"Wystąpił błąd: {ex.Message}", "Błąd sortowania", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { StopSort(); }
        }

        public void ResetItemsColor() { foreach (var item in Items) item.Color = SelectedColor; }

        public void Dispose()
        {
            _distributedSortService.Dispose();
        }
    }
}