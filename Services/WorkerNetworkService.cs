using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using AlgorithmVisualizer.Model;
using AlgorithmVisualizer.View;

namespace AlgorithmVisualizer.Services
{
    public class WorkerNetworkService
    {
        private readonly MainViewModel _vm;
        private readonly SortingService _sortingService;
        private readonly ManualResetEventSlim _pauseEvent;
        private readonly Stopwatch _sw = new Stopwatch();

        public WorkerNetworkService(MainViewModel vm, SortingService sortingService, ManualResetEventSlim pauseEvent)
        {
            _vm = vm;
            _sortingService = sortingService;
            _pauseEvent = pauseEvent;
        }

        public async Task StartWorkerLoop(string ip, Action<CancellationTokenSource> setCts)
        {
            _vm.WorkerStatus = $"Łączenie z {ip}:8888...";
            int port = 8888;
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            while (true)
            {
                try
                {
                    using var client = new TcpClient();
                    await client.ConnectAsync(ip, port);
                    _vm.WorkerStatus = "Połączono! Oczekuję na dane...";

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
                        var incomingMsg = JsonSerializer.Deserialize<SortMessage>(json, options);

                        _vm.WorkerStatus = $"Sortowanie {incomingMsg.Data.Count} elementów ({incomingMsg.AlgorithmName})...";

                        Application.Current.Dispatcher.Invoke(() => {
                            _vm.Items.Clear();
                            foreach (var d in incomingMsg.Data) _vm.Items.Add(new VisualElement { Value = d, Color = _vm.SelectedColor });
                        });

                        var cts = new CancellationTokenSource();
                        setCts(cts);
                        var token = cts.Token;
                        _vm.IsSorting = true;
                        _vm.IsPaused = false;
                        _pauseEvent.Set();
                        _sw.Restart();

                        _ = Task.Run(async () => {
                            while (!token.IsCancellationRequested && _vm.IsSorting)
                            {
                                _vm.SortTime = _sw.Elapsed.ToString(@"ss\:fff") + " ms";
                                await Task.Delay(50);
                            }
                        }, token);

                        SemaphoreSlim writeLock = new SemaphoreSlim(1, 1);
                        var progressTask = Task.Run(async () => {
                            while (!token.IsCancellationRequested && _vm.IsSorting)
                            {
                                await Task.Delay(50);
                                if (!_vm.IsSorting) break;

                                List<double> currentData = null;
                                Application.Current.Dispatcher.Invoke(() => {
                                    currentData = _vm.Items.Select(i => i.Value).ToList();
                                });

                                var msg = new SortMessage { IsFinal = false, AlgorithmName = incomingMsg.AlgorithmName, Data = currentData };
                                string progJson = JsonSerializer.Serialize(msg);
                                byte[] progBytes = Encoding.UTF8.GetBytes(progJson);
                                byte[] progLen = BitConverter.GetBytes(progBytes.Length);

                                await writeLock.WaitAsync();
                                try
                                {
                                    await stream.WriteAsync(progLen, 0, 4);
                                    await stream.WriteAsync(progBytes, 0, progBytes.Length);
                                }
                                catch { }
                                finally { writeLock.Release(); }
                            }
                        });

                        // Dynamiczne pobranie strategii lokalnej dla workera po nazwie tekstowej
                        var algorithm = _sortingService.GetAlgorithm(incomingMsg.AlgorithmName, null);
                        await algorithm.SortAsync(token);

                        _vm.IsSorting = false;
                        await progressTask;
                        _vm.ResetItemsColor();
                        _vm.WorkerStatus = "Wysyłanie wyników...";

                        var sortedData = _vm.Items.Select(i => i.Value).ToList();
                        var finalMsg = new SortMessage { IsFinal = true, AlgorithmName = incomingMsg.AlgorithmName, Data = sortedData };
                        string respJson = JsonSerializer.Serialize(finalMsg);
                        byte[] respBytes = Encoding.UTF8.GetBytes(respJson);
                        byte[] respLen = BitConverter.GetBytes(respBytes.Length);

                        await writeLock.WaitAsync();
                        try
                        {
                            await stream.WriteAsync(respLen, 0, 4);
                            await stream.WriteAsync(respBytes, 0, respBytes.Length);
                        }
                        finally { writeLock.Release(); }
                        _vm.WorkerStatus = "Oczekuję na kolejne dane...";
                    }
                }
                catch (Exception ex)
                {
                    _vm.WorkerStatus = $"Błąd: {ex.Message}. Próba ponownego połączenia...";
                    await Task.Delay(5000);
                }
            }
        }
    }
}