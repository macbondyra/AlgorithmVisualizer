﻿// Plik: AlgorithmVisualizer.Worker/Program.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

// Prosta aplikacja-robotnik, która łączy się z Mistrzem,
// odbiera dane, sortuje je i odsyła.
public class Worker
{
    public static async Task Main(string[] args)
    {
        Console.Title = "Sorting Worker";
        Console.Write("Podaj adres IP serwera (Mistrza) [domyślnie: 127.0.0.1]: ");
        string serverIp = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(serverIp))
        {
            serverIp = "127.0.0.1";
        }

        int port = 8888;

        while (true)
        {
            try
            {
                using var client = new TcpClient();
                Console.WriteLine($"Łączenie z {serverIp}:{port}...");
                await client.ConnectAsync(serverIp, port);
                Console.WriteLine("Połączono! Oczekiwanie na dane...");

                using var stream = client.GetStream();
                while (client.Connected)
                {
                    // Odczytaj długość wiadomości (4 bajty)
                    byte[] lengthBuffer = new byte[4];
                    int totalRead = 0;
                    while (totalRead < lengthBuffer.Length)
                    {
                        int bytesRead = await stream.ReadAsync(lengthBuffer, totalRead, lengthBuffer.Length - totalRead);
                        if (bytesRead == 0) break;
                        totalRead += bytesRead;
                    }
                    if (totalRead < lengthBuffer.Length) break; // Połączenie zamknięte

                    int messageLength = BitConverter.ToInt32(lengthBuffer, 0);

                    // Odczytaj właściwą wiadomość
                    byte[] messageBuffer = new byte[messageLength];
                    totalRead = 0;
                    while (totalRead < messageBuffer.Length)
                    {
                        int bytesRead = await stream.ReadAsync(messageBuffer, totalRead, messageBuffer.Length - totalRead);
                        if (bytesRead == 0) break;
                        totalRead += bytesRead;
                    }
                    if (totalRead < messageBuffer.Length) break; // Połączenie zamknięte
                    
                    string jsonMessage = Encoding.UTF8.GetString(messageBuffer);
                    var data = JsonSerializer.Deserialize<List<double>>(jsonMessage);

                    Console.WriteLine($"Otrzymano {data.Count} elementów. Rozpoczynam sortowanie...");
                    
                    // Sortowanie
                    data.Sort();
                    
                    Console.WriteLine("Sortowanie ukończone. Odsyłanie wyniku...");

                    // Serializuj i odeślij posortowane dane
                    var responseObj = new { IsFinal = true, Data = data };
                    string responseJson = JsonSerializer.Serialize(responseObj);        
                    byte[] responseBytes = Encoding.UTF8.GetBytes(responseJson);
                    byte[] responseLength = BitConverter.GetBytes(responseBytes.Length);

                    await stream.WriteAsync(responseLength, 0, 4);
                    await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
                    Console.WriteLine("Wynik odesłany. Oczekiwanie na kolejne zadanie...");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd: {ex.Message}. Próba ponownego połączenia za 5 sekund.");
                await Task.Delay(5000);
            }
        }
    }
}