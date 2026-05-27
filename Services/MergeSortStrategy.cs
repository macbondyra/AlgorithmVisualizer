using AlgorithmVisualizer.Services;

public class MergeSortStrategy : ISortingStrategy
{
    private readonly SortingAlgorithms _alg;
    public string Name => "Merge Sort";

    public MergeSortStrategy(SortingAlgorithms alg) => _alg = alg;

    public Task SortAsync(CancellationToken token) => _alg.ParallelMergeSort(0, _alg.ItemsCount - 1, token);
}