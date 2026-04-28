﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using AlgorithmVisualizer.Model;
using AlgorithmVisualizer.Helpers;
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

        public List<string> Algorithms { get; } = new() { "Bubble Sort", "Parallel Merge Sort", "Distributed Merge Sort" };
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
            _ = StartWorkerLoop(ip);
        }

        private async Task StartWorkerLoop(string ip)
        {
            WorkerStatus = $"Łączenie z {ip}:8888...";
            int port = 8888;
            while (true)
            {
                try
                {
                    using var client = new TcpClient();
                    await client.ConnectAsync(ip, port);
                    WorkerStatus = "Połączono! Oczekuję na dane...";

                    using var stream = client.GetStream();
                    while (client.Connected)
                    {
                        byte[] lengthBuffer = new byte[4];
                        int totalRead = 0;
                        while (totalRead < lengthBuffer.Length)
                        {
                            int r = await stream.ReadAsync(lengthBuffer, totalRead, lengthBuffer.Length - totalRead);
                            if (r == 0) break; totalRead += r;
                        }
                        if (totalRead < lengthBuffer.Length) break;

                        int msgLen = BitConverter.ToInt32(lengthBuffer, 0);
                        byte[] msgBuffer = new byte[msgLen];
                        totalRead = 0;
                        while (totalRead < msgBuffer.Length)
                        {
                            int r = await stream.ReadAsync(msgBuffer, totalRead, msgBuffer.Length - totalRead);
                            if (r == 0) break; totalRead += r;
                        }
                        if (totalRead < msgBuffer.Length) break;

                        string json = Encoding.UTF8.GetString(msgBuffer);
                        var data = JsonSerializer.Deserialize<List<double>>(json);

                        WorkerStatus = $"Sortowanie {data.Count} elementów...";

                        // Pokaż dane do posortowania
                        Application.Current.Dispatcher.Invoke(() => {
                            Items.Clear();
                            foreach (var d in data) Items.Add(new VisualElement { Value = d, Color = SelectedColor });
                        });

                        _cts = new CancellationTokenSource();
                        var token = _cts.Token;
                        IsSorting = true;
                        IsPaused = false;
                        _pauseEvent.Set();
                        _sw.Restart();

                        _ = Task.Run(async () => {
                            while (!token.IsCancellationRequested && IsSorting)
                            {
                                SortTime = _sw.Elapsed.ToString(@"ss\:fff") + " ms";
                                await Task.Delay(50);
                            }
                        }, token);

                        // Równoległe wysyłanie postępów do Mistrza
                        SemaphoreSlim writeLock = new SemaphoreSlim(1, 1);
                        var progressTask = Task.Run(async () => {
                            while (!token.IsCancellationRequested && IsSorting)
                            {
                                await Task.Delay(50);
                                if (!IsSorting) break;

                                List<double> currentData = null;
                                Application.Current.Dispatcher.Invoke(() => {
                                    currentData = Items.Select(i => i.Value).ToList();
                                });

                                var msg = new { IsFinal = false, Data = currentData };
                                string progJson = JsonSerializer.Serialize(msg);
                                byte[] progBytes = Encoding.UTF8.GetBytes(progJson);
                                byte[] progLen = BitConverter.GetBytes(progBytes.Length);

                                await writeLock.WaitAsync();
                                try {
                                    await stream.WriteAsync(progLen, 0, 4);
                                    await stream.WriteAsync(progBytes, 0, progBytes.Length);
                                } catch {
                                    // Błędy rozłączenia obsłuży główna pętla
                                } finally {
                                    writeLock.Release();
                                }
                            }
                        });

                        // Wizualne sortowanie po stronie pracownika!
                        if (SelectedAlgorithm == "Bubble Sort") await BubbleSort(token);
                        else await Task.Run(async () => await ParallelMergeSort(0, Items.Count - 1, token), token);

                        IsSorting = false;
                        await progressTask; // Czekamy aż reporter skończy wysyłanie postępów
                        ResetItemsColor();
                        WorkerStatus = "Wysyłanie wyników...";

                        var sortedData = Items.Select(i => i.Value).ToList();
                        var finalMsg = new { IsFinal = true, Data = sortedData };
                        string respJson = JsonSerializer.Serialize(finalMsg);
                        byte[] respBytes = Encoding.UTF8.GetBytes(respJson);
                        byte[] respLen = BitConverter.GetBytes(respBytes.Length);

                        await writeLock.WaitAsync();
                        try {
                            await stream.WriteAsync(respLen, 0, 4);
                            await stream.WriteAsync(respBytes, 0, respBytes.Length);
                        } finally { writeLock.Release(); }
                        WorkerStatus = "Oczekuję na kolejne dane...";
                    }
                }
                catch (Exception ex)
                {
                    WorkerStatus = $"Błąd: {ex.Message}. Próba ponownego połączenia...";
                    await Task.Delay(5000);
                }
            }
        }

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
            int toneDuration = Math.Max(10, Math.Min(50, Delay / 2));
            Task.Run(() => Helpers.SoundHelper.PlaySineTone(frequency, toneDuration, 0.1));
        }

        private async Task PlaySuccessMelody(CancellationToken token)
        {
            double[] successNotes = { 523.25, 659.25, 783.99, 1046.50 };
            foreach (var freq in successNotes)
            {
                if (token.IsCancellationRequested) return;
                _ = Task.Run(() => Helpers.SoundHelper.PlaySineTone(freq, 200, 0.2));
                Application.Current.Dispatcher.Invoke(() => { foreach (var item in Items) item.Color = Brushes.White; });
                await Task.Delay(70);
                Application.Current.Dispatcher.Invoke(() => { foreach (var item in Items) item.Color = SelectedColor; });
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

                if (SelectedAlgorithm == "Bubble Sort") await BubbleSort(token);
                else if (SelectedAlgorithm == "Parallel Merge Sort")
                {
                    await Task.Run(async () => await ParallelMergeSort(0, Items.Count - 1, token), token);
                }
                else if (SelectedAlgorithm == "Distributed Merge Sort")
                {
                    var data = Items.Select(i => i.Value).ToList();
                    await DistributedSort(data, token);
                }

                if (!token.IsCancellationRequested)
                {
                    _sw.Stop();
                    SortTime = _sw.Elapsed.ToString(@"ss\:fff") + " ms";
                    await PlaySuccessMelody(token);
                }
            }
            catch (OperationCanceledException) { /* Ignored */ }
            catch (Exception ex)
            {
                // Można tu wyświetlić MessageBox z błędem
                System.Windows.MessageBox.Show($"Wystąpił błąd: {ex.Message}", "Błąd sortowania", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally { StopSort(); }
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
                Application.Current.Dispatcher.Invoke(() => {
                    Items[idx].Value = temp[k];
                    Items[idx].Color = Brushes.WhiteSmoke;
                });
                PlayTone(temp[k]);
                await Task.Delay(Delay, t);
                await CheckPause();
                Application.Current.Dispatcher.Invoke(() => Items[idx].Color = SelectedColor);
            }
        }

        private async Task DistributedSort(List<double> data, CancellationToken token)
        {
            var sortedChunks = await _distributedSortService.DistributeAndSortAsync(data, (offset, currentData) => 
            {
                Application.Current.Dispatcher.InvokeAsync(() => 
                {
                    for (int i = 0; i < currentData.Count; i++)
                    {
                        int targetIndex = offset + i;
                        if (targetIndex < Items.Count)
                        {
                            Items[targetIndex].Value = currentData[i];
                        }
                    }
                });
            }, token);

            // Przygotuj dane do scalenia wizualnego
            var finalMergedList = new List<double>();
            int currentPos = 0;
            foreach (var chunk in sortedChunks)
            {
                for (int i = 0; i < chunk.Count; i++)
                {
                    int targetIndex = currentPos + i;
                    if (targetIndex < Items.Count)
                    {
                        Application.Current.Dispatcher.Invoke(() => Items[targetIndex].Value = chunk[i]);
                    }
                }
                currentPos += chunk.Count;
            }

            // Wizualizuj scalanie k-kierunkowe (uproszczone do serii scaleń 2-kierunkowych)
            int totalSize = 0;
            for (int i = 0; i < sortedChunks.Count - 1; i++)
            {
                int leftStart = 0;
                int rightStart = totalSize + sortedChunks[i].Count;
                int mid = rightStart - 1;
                int rightEnd = rightStart + sortedChunks[i+1].Count - 1;

                await Merge(leftStart, mid, rightEnd, token);
                totalSize += sortedChunks[i].Count;
            }
        }

        private void UpdateColor(int i, int j, Brush c) => Application.Current.Dispatcher.Invoke(() => {
            if (i >= 0 && i < Items.Count) Items[i].Color = c;
            if (j >= 0 && j < Items.Count) Items[j].Color = c;
        });

        private void ResetItemsColor() { foreach (var item in Items) item.Color = SelectedColor; }

        public void Dispose()
        {
            _distributedSortService.Dispose();
        }
    }
}
