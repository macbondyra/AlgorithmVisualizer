using System.Threading;
using System.Threading.Tasks;

namespace AlgorithmVisualizer.Services
{
    public class DistributedSortDecorator : ISortingStrategy
    {
        private readonly ISortingStrategy _localStrategy;
        private readonly DistributedSortService _networkService;
        private readonly SortingAlgorithms _alg;

        public string Name => _localStrategy.Name;

        public DistributedSortDecorator(ISortingStrategy localStrategy, DistributedSortService networkService, SortingAlgorithms alg)
        {
            _localStrategy = localStrategy;
            _networkService = networkService;
            _alg = alg;
        }

        public async Task SortAsync(CancellationToken token)
        {
            var data = _alg.GetCurrentData();

            var sortedChunks = await _networkService.DistributeAndSortAsync(data, Name, (offset, currentData) =>
            {
                _alg.UpdateValuesFromNetwork(offset, currentData);
            }, token);

            await _alg.MergeSortedChunks(sortedChunks, token);
        }
    }
}