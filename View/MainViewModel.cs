using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AlgorithmVisualizer.Model;
using System.Windows.Media;

namespace AlgorithmVisualizer.View
{
    public class MainViewModel
    {
        public ObservableCollection<VisualElement> Items { get; set; } = new();

        public async Task BubbleSort()
        {
            for (int i = 0; i < Items.Count; i++)
            {
                for (int j = 0; j < Items.Count - 1; j++)
                {
                    Items[j].Color = Brushes.Red;
                    Items[j + 1].Color = Brushes.Red;

                    await Task.Delay(5); 

                    if (Items[j].Value > Items[j + 1].Value)
                    {
                        (Items[j].Value, Items[j + 1].Value) = (Items[j + 1].Value, Items[j].Value);
                    }

                    Items[j].Color = Brushes.SkyBlue;
                    Items[j + 1].Color = Brushes.SkyBlue;
                }
            }
        }
        public async Task SortParallel()
        {
            // Start algorytmu w wątku tła
            await Task.Run(async () => await ParallelMergeSort(0, Items.Count - 1));
        }

        private async Task ParallelMergeSort(int left, int right)
        {
            if (left >= right) return;

            int mid = (left + right) / 2;

            // Próg wydajności: dla małych podzbiorów nie twórz nowych wątków
            if (right - left > 10)
            {
                await Task.WhenAll(
                    Task.Run(() => ParallelMergeSort(left, mid)),
                    Task.Run(() => ParallelMergeSort(mid + 1, right))
                );
            }
            else
            {
                await ParallelMergeSort(left, mid);
                await ParallelMergeSort(mid + 1, right);
            }

            await Merge(left, mid, right);
        }

        private async Task Merge(int left, int mid, int right)
        {
            List<double> temp = new();
            int i = left, j = mid + 1;

            while (i <= mid && j <= right)
            {
                // Wizualizacja: zaznacz porównywane
                UpdateColor(i, j, Brushes.Red);
                await Task.Delay(10);

                if (Items[i].Value <= Items[j].Value) temp.Add(Items[i++].Value);
                else temp.Add(Items[j++].Value);

                ResetColor(i - 1, j - 1);
            }

            while (i <= mid) temp.Add(Items[i++].Value);
            while (j <= right) temp.Add(Items[j++].Value);

            // Kopiowanie do głównej kolekcji (UI)
            for (int k = 0; k < temp.Count; k++)
            {
                int targetIndex = left + k;
                App.Current.Dispatcher.Invoke(() => {
                    Items[targetIndex].Value = temp[k];
                    Items[targetIndex].Color = Brushes.Green;
                });
                await Task.Delay(10);
                App.Current.Dispatcher.Invoke(() => Items[targetIndex].Color = Brushes.SkyBlue);
            }
        }

        private void UpdateColor(int i, int j, Brush color)
        {
            App.Current.Dispatcher.Invoke(() => {
                if (i < Items.Count) Items[i].Color = color;
                if (j < Items.Count) Items[j].Color = color;
            });
        }

        private void ResetColor(int i, int j)
        {
            App.Current.Dispatcher.Invoke(() => {
                if (i >= 0 && i < Items.Count) Items[i].Color = Brushes.SkyBlue;
                if (j >= 0 && j < Items.Count) Items[j].Color = Brushes.SkyBlue;
            });
        }
    }
}
