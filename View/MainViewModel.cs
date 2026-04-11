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

                    await Task.Delay(50); 

                    if (Items[j].Value > Items[j + 1].Value)
                    {
                        (Items[j].Value, Items[j + 1].Value) = (Items[j + 1].Value, Items[j].Value);
                    }

                    Items[j].Color = Brushes.SkyBlue;
                    Items[j + 1].Color = Brushes.SkyBlue;
                }
            }
        }
    }
}
