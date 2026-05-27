using System.Collections.Generic;

namespace AlgorithmVisualizer.Model
{
    public class SortMessage
    {
        public bool IsFinal { get; set; }
        public string AlgorithmName { get; set; }
        public List<double> Data { get; set; }
    }
}