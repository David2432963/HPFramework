using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using HP.Framework.Assets;
using UnityEngine;
using VContainer;

namespace HP.Framework.Samples.AssetLease
{
    public sealed class SampleAssetLeaseOwner : MonoBehaviour
    {
        private IAssetLeaseProvider assets;
        private IAssetLease<GameObject> lease;

        [Inject]
        public void Construct(IAssetLeaseProvider assets)
        {
            this.assets = assets;
        }

        public async UniTask LoadAsync(string key, CancellationToken cancellationToken)
        {
            lease?.Dispose();
            lease = await assets.AcquireAsync<GameObject>(key, cancellationToken);
        }

        private void OnDestroy()
        {
            lease?.Dispose();
            lease = null;
        }
    }
}
