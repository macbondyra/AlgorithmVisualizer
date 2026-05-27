using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AlgorithmVisualizer.Services
{
    public interface ISortingStrategy
    {
        string Name { get; }
        Task SortAsync(CancellationToken token);
    }

    public class BubbleSortStrategy : ISortingStrategy
    {
        private readonly SortingAlgorithms _alg;
        public string Name => "Bubble Sort";

        public BubbleSortStrategy(SortingAlgorithms alg) => _alg = alg;

        public Task SortAsync(CancellationToken token) => _alg.BubbleSort(token);
    }

    public class ParallelMergeSortStrategy : ISortingStrategy
    {
        private readonly SortingAlgorithms _alg;
        public string Name => "Parallel Merge Sort";

        public ParallelMergeSortStrategy(SortingAlgorithms alg) => _alg = alg;

        public Task SortAsync(CancellationToken token) => _alg.ParallelMergeSort(0, _alg.ItemsCount - 1, token);
    }
}