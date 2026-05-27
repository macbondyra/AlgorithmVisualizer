using AlgorithmVisualizer.Services;

public class BubbleSortStrategy : ISortingStrategy
{
    private readonly SortingAlgorithms _alg;
    public string Name => "Bubble Sort";

    public BubbleSortStrategy(SortingAlgorithms alg) => _alg = alg;

    public Task SortAsync(CancellationToken token) => _alg.BubbleSort(token);
}