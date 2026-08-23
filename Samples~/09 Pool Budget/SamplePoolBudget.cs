using System.Threading;
using Cysharp.Threading.Tasks;
using HP.Framework.Pooling;
using UnityEngine;
using VContainer;

namespace HP.Framework.Samples.PoolBudget
{
    public sealed class SamplePoolBudget : MonoBehaviour
    {
        [SerializeField] private GameObject prefab;
        private IPoolService pools;

        [Inject]
        public void Construct(IPoolService pools)
        {
            this.pools = pools;
        }

        public async UniTask PrepareAsync(CancellationToken cancellationToken)
        {
            pools.SetMaxInactive(prefab, 24);
            await pools.PrewarmAsync(
                prefab,
                12,
                new PoolPrewarmOptions(maxInstancesPerFrame: 2),
                cancellationToken);
        }

        public void TrimAfterFeature()
        {
            pools.Trim(prefab, targetInactiveCount: 4);
        }
    }
}
