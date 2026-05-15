# Parallel, Distributed & Sequential Algorithm Visualizer

Kompleksowy system wizualizacji i analizy algorytmów sortowania, pozwalający na bezpośrednie porównanie wydajności oraz mechaniki działania procesów sekwencyjnych, równoległych i rozproszonych. Aplikacja służy jako interaktywne narzędzie do demonstracji nowoczesnych metod przetwarzania danych przy wykorzystaniu mocy obliczeniowej wielu jednostek.

## 📋 O projekcie

Głównym celem projektu jest stworzenie środowiska umożliwiającego analizę algorytmów sortowania w różnych modelach wykonawczych:

* **Sekwencyjnym:** Tradycyjne podejście wykonywane krok po kroku na jednej jednostce.
* **Równoległym:** Wykorzystanie wielowątkowości lokalnego procesora do jednoczesnego przetwarzania danych.
* **Rozproszonym:** Delegowanie zadań obliczeniowych do zewnętrznych węzłów ("Robotników") w architekturze Master-Worker.

System wizualizuje nie tylko sam proces porządkowania, ale także teoretyczne aspekty delegowania zadań, komunikacji sieciowej oraz ponownego scalania przetworzonych danych w spójną całość.

## ✨ Kluczowe funkcjonalności

* **Wizualizacja procesów:** Dynamiczny rendering graficzny (słupkowy) postępu sortowania w czasie rzeczywistym w środowisku WPF.
* **Interaktywne sterowanie:**
    * Konfiguracja liczby elementów w zbiorze w zakresie od 10 do 1000.
    * Regulacja opóźnienia operacji (speed/delay) w celu dokładnej obserwacji algorytmu.
    * Możliwość wyboru kolorystyki interfejsu użytkownika.
* **Obsługa multimediów:** Synchronizacja zdarzeń sortowania z sygnałami dźwiękowymi o częstotliwości zależnej od wartości elementu, co ułatwia percepcję zmian w zbiorze danych.
* **Monitoring wydajności:**
    * Licznik czasu rzeczywistego wykonania operacji.
    * Śledzenie liczby aktywnych jednostek obliczeniowych ("Robotników") aktualnie połączonych z systemem.

## 🏗 Architektura systemu

System opiera się na modelu **Master-Worker** komunikującym się za pośrednictwem protokołu TCP/IP.



1.  **Mistrz (Serwer):** Centralna jednostka zarządzająca interfejsem graficznym. Odpowiada za generowanie danych, podział zadań na pakiety oraz koordynację pracy podłączonych jednostek.
2.  **Robotnik (Klient):** Zewnętrzny proces obliczeniowy, który odbiera pakiety danych, wykonuje zadaną logikę algorytmu i przesyła wyniki cząstkowe lub potwierdzenia operacji z powrotem do Mistrza.
3.  **Responsywność:** Dzięki wykorzystaniu programowania asynchronicznego (`async/await`), interfejs użytkownika pozostaje w pełni responsywny nawet podczas intensywnej wymiany danych przez sieć.

## 🛠 Stack technologiczny

| Komponent | Technologia | Zastosowanie |
| :--- | :--- | :--- |
| **Język / Platforma** | .NET / C# | Fundament logiczny i środowisko uruchomieniowe. |
| **Interfejs Graficzny** | WPF (XAML) | Zaawansowane kontrolki użytkownika i płynny rendering. |
| **Komunikacja Sieciowa** | System.Net.Sockets | Obsługa połączeń TCP/IP i transmisja danych. |
| **Wielowątkowość** | Task Parallel Library | Zarządzanie zadaniami równoległymi i asynchroniczność. |
| **Multimedia** | System.Media | Generowanie i synchronizacja sygnałów dźwiękowych. |

## 📁 Struktura projektu

Projekt jest podzielony na logiczne moduły, co ułatwia jego rozwój i testowanie:

```text
AlgorithmVisualizer/
├── AlgorithmVisualizer/          # Główna aplikacja (Mistrz / UI)
│   ├── Model/                    # Reprezentacja danych (np. VisualElement.cs)
│   ├── View/                     # Definicje interfejsu (XAML)
│   ├── ViewModel/                # Logika powiązań danych (MVVM)
│   ├── Services/                 # Serwisy sieciowe (np. DistributedSortService.cs)
│   └── helpers/                  # Moduły pomocnicze (np. SoundHelper.cs)
├── AlgorithmVisualizer.Worker/   # Jednostka obliczeniowa (Robotnik)
│   └── Program.cs                # Logika klienta sieciowego
└── AlgorithmVisualizer.sln       # Plik rozwiązania Visual Studio text
```

## 🚀 Jak uruchomić projekt

Aplikacja składa się z dwóch głównych komponentów: **Mistrza (Master)**, który pełni rolę serwera i interfejsu graficznego, oraz **Robotnika (Worker)**, będącego zewnętrzną jednostką obliczeniową.

### Wymagania wstępne
* Środowisko **.NET 6.0 SDK** (lub nowsze).
* Środowisko **Visual Studio 2022** (zalecane) lub narzędzia **.NET CLI**.

### Instrukcja krok po kroku

#### 1. Pobranie i kompilacja
Sklonuj repozytorium i zbuduj całe rozwiązanie, aby przywrócić wymagane pakiety:
```bash
git clone <url-repozytorium>
cd AlgorithmVisualizer
dotnet build AlgorithmVisualizer.sln
```
#### 2. Uruchomienie jednostki centralnej (Mistrz)
Aplikacja Mistrza musi zostać uruchomiona jako pierwsza, aby otworzyć serwer nasłuchujący na połączenia przychodzące[cite: 8, 17]. Możesz to zrobić z poziomu Visual Studio lub terminala:
```bash
# Wejdź do folderu projektu Mistrza
cd AlgorithmVisualizer
# Uruchom aplikację GUI
dotnet run
```
#### 3. Uruchomienie jednostek obliczeniowych (Robotnik)
Robotnik łączy się z Mistrzem przez protokół TCP/IP w celu odbierania zadań sortowania. Aby uruchomić jedną lub więcej instancji obliczeniowych, otwórz nowy terminal i wykonaj:
```bash
# Wejdź do folderu projektu Robotnika
cd AlgorithmVisualizer.Worker
# Uruchom instancję obliczeniową
dotnet run
```
### ⚙️ Konfiguracja i połączenie

* **Adres IP i Port**: Domyślnie Robotnik łączy się z Mistrzem na adresie `localhost`. Jeśli uruchamiasz aplikacje na różnych maszynach w sieci lokalnej, upewnij się, że w kodzie Robotnika wskazano właściwy adres IP serwera.
* **Weryfikacja**: Po poprawnym połączeniu, w głównym oknie aplikacji Mistrza licznik aktywnych jednostek obliczeniowych ("Robotników") powinien zostać zaktualizowany w czasie rzeczywistym.
* **Zapora systemowa**: Upewnij się, że port TCP wykorzystywany przez aplikację nie jest blokowany przez Firewall, co jest niezbędne do stabilnej komunikacji między modułami "Mistrz" i "Robotnik".

### 🎮 Pierwsze kroki w aplikacji

* **Wybór Algorytmu**: Wybierz z listy interesujący Cię algorytm sortowania (np. metodę zamiany, wstawiania lub dzielenia).
* **Konfiguracja Danych**: Ustaw liczbę elementów w zbiorze (od 10 do 1000) oraz dostosuj prędkość wizualizacji za pomocą suwaka opóźnienia (Speed/Delay).
* **Wybór Trybu**: Zdecyduj, czy chcesz przeprowadzić tradycyjne sortowanie sekwencyjne, czy nowoczesne sortowanie rozproszone z udziałem zewnętrznych jednostek obliczeniowych.
* **Start**: Kliknij przycisk rozpoczęcia, aby uruchomić dynamiczną wizualizację słupkową z pełną synchronizacją dźwiękową.
