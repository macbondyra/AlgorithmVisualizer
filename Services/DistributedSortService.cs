using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AlgorithmVisualizer.Services
{
    public class DistributedSortService : IDisposable
    {
        public class SortMessage
        {
            public bool IsFinal { get; set; }
            public List<double> Data { get; set; }
        }

        private TcpListener _listener;
        private readonly List<TcpClient> _workers = new();
        private readonly CancellationTokenSource _cts = new();
        public int ConnectedWorkers => _workers.Count;
        public event Action WorkersChanged;

        public DistributedSortService()
        {
            _listener = new TcpListener(IPAddress.Any, 8888);
        }

        public void StartListening()
        {
            try
            {
                _listener.Start();
                Task.Run(() => AcceptWorkers(), _cts.Token);
            }
            catch (Exception ex)
            {
                // Log or handle exception (e.g., port already in use)
                Console.WriteLine($"Failed to start listener: {ex.Message}");
            }
        }

        private async Task AcceptWorkers()
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    // Usunięto token dla kompatybilności z .NET < 6
                    var worker = await _listener.AcceptTcpClientAsync();
                    lock (_workers)
                    {
                        _workers.Add(worker);
                    }
                    WorkersChanged?.Invoke();
                }
                catch (OperationCanceledException)
                {
                    // Listener was stopped, exit loop
                    break;
                }
                catch (Exception ex)
                {
                     Console.WriteLine($"Error accepting worker: {ex.Message}");
                }
            }
        }

        public async Task<List<List<double>>> DistributeAndSortAsync(List<double> data, Action<int, List<double>> onProgress, CancellationToken token)
        {
            List<TcpClient> currentWorkers;
            lock (_workers)
            {
                // Usuń rozłączonych workerów
                _workers.RemoveAll(w => !w.Connected);
                currentWorkers = new List<TcpClient>(_workers);
            }
            WorkersChanged?.Invoke();

            if (currentWorkers.Count == 0)
            {
                throw new InvalidOperationException("Brak podłączonych robotników (workerów).");
            }

            var chunks = Split(data, currentWorkers.Count);
            var tasks = new List<Task<List<double>>>();

            int currentOffset = 0;
            for (int i = 0; i < currentWorkers.Count; i++)
            {
                int offset = currentOffset;
                tasks.Add(ProcessChunk(currentWorkers[i], chunks[i], progressData => onProgress?.Invoke(offset, progressData), token));
                currentOffset += chunks[i].Count;
            }

            var sortedChunks = await Task.WhenAll(tasks);
            return sortedChunks.ToList();
        }

        private async Task<List<double>> ProcessChunk(TcpClient worker, List<double> chunk, Action<List<double>> onProgress, CancellationToken token)
        {
            using var registration = token.Register(() => worker.Close());

            var stream = worker.GetStream();
            
            // Wyślij dane
            string json = JsonSerializer.Serialize(chunk);
            byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
            byte[] lengthPrefix = BitConverter.GetBytes(jsonBytes.Length);
            await stream.WriteAsync(lengthPrefix, 0, 4, token);
            await stream.WriteAsync(jsonBytes, 0, jsonBytes.Length, token);

            // Pętla nasłuchująca odpowiedzi (postępy + wynik końcowy)
            while (true)
            {
                byte[] lengthBuffer = new byte[4];
                int totalRead = 0;
                while (totalRead < lengthBuffer.Length)
                {
                    int bytesRead = await stream.ReadAsync(lengthBuffer, totalRead, lengthBuffer.Length - totalRead, token);
                    if (bytesRead == 0) throw new EndOfStreamException("Utracono połączenie z workerem podczas odczytu długości wiadomości.");
                    totalRead += bytesRead;
                }

                int messageLength = BitConverter.ToInt32(lengthBuffer, 0);

                byte[] messageBuffer = new byte[messageLength];
                totalRead = 0;
                while (totalRead < messageBuffer.Length)
                {
                    int bytesRead = await stream.ReadAsync(messageBuffer, totalRead, messageBuffer.Length - totalRead, token);
                    if (bytesRead == 0) throw new EndOfStreamException("Utracono połączenie z workerem podczas odczytu danych.");
                    totalRead += bytesRead;
                }
                
                string responseJson = Encoding.UTF8.GetString(messageBuffer);
                var message = JsonSerializer.Deserialize<SortMessage>(responseJson);
                
                if (message.IsFinal)
                    return message.Data;
                else
                    onProgress?.Invoke(message.Data);
            }
        }

        private static List<List<double>> Split(List<double> source, int parts)
        {
            return source
                .Select((x, i) => new { Index = i, Value = x })
                .GroupBy(x => x.Index % parts)
                .Select(g => g.Select(x => x.Value).ToList())
                .ToList();
        }

        public void Dispose()
        {
            _cts.Cancel();
            _listener.Stop();
            lock (_workers)
            {
                foreach (var worker in _workers)
                {
                    worker.Close();
                }
                _workers.Clear();
            }
            WorkersChanged?.Invoke();
        }
    }
}
