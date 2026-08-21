using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace HP.Framework.Assets
{
    /// <summary>
    /// Default provider backed by Unity Resources.
    ///
    /// Ownership-aware callers should use IAssetLeaseProvider. Released lease records remain
    /// cached at zero references until TrimUnused() so short-lived scene/feature churn can reuse
    /// the same loaded asset. The v3 IAssetProvider compatibility surface keeps one implicit
    /// logical reference per successful LoadAssetAsync/InstantiateAsync call and releases only
    /// provider-owned references.
    /// </summary>
    public sealed class ResourcesAssetProvider :
        IAssetLeaseProvider,
        IAssetMemoryService,
        IAssetDiagnostics,
        IDisposable
    {
        private readonly object syncRoot = new object();
        private readonly IObjectResolver objectResolver;
        private readonly IResourcesAssetLoader assetLoader;
        private readonly Dictionary<string, AssetRecord> records =
            new Dictionary<string, AssetRecord>(StringComparer.Ordinal);
        private readonly Dictionary<int, AssetRecord> recordsByAssetId =
            new Dictionary<int, AssetRecord>();
        private readonly Dictionary<int, CompatibilityInstanceRecord> compatibilityInstances =
            new Dictionary<int, CompatibilityInstanceRecord>();
        private readonly HashSet<InstanceLeaseBase> leasedInstances =
            new HashSet<InstanceLeaseBase>();

        private long accessSequence;
        private bool disposed;

        public ResourcesAssetProvider(IObjectResolver objectResolver = null)
            : this(new UnityResourcesAssetLoader(), objectResolver)
        {
        }

        internal ResourcesAssetProvider(
            IResourcesAssetLoader assetLoader,
            IObjectResolver objectResolver = null)
        {
            this.assetLoader = assetLoader ?? throw new ArgumentNullException(nameof(assetLoader));
            this.objectResolver = objectResolver;
        }

        public int LoadedAssetCount => GetStats().LoadedAssetCount;
        public int UnusedAssetCount => GetStats().UnusedAssetCount;

        public async UniTask<IAssetLease<T>> AcquireAsync<T>(
            string key,
            CancellationToken cancellationToken = default)
            where T : UnityEngine.Object
        {
            AcquiredAsset<T> acquired = await AcquireRecordAsync<T>(
                key,
                OwnershipKind.Lease,
                cancellationToken);
            return new AssetLease<T>(this, acquired.Record, acquired.Asset);
        }

        public async UniTask<T> LoadAssetAsync<T>(
            string key,
            CancellationToken cancellationToken = default)
            where T : UnityEngine.Object
        {
            AcquiredAsset<T> acquired = await AcquireRecordAsync<T>(
                key,
                OwnershipKind.Compatibility,
                cancellationToken);
            return acquired.Asset;
        }

        public async UniTask<GameObject> InstantiateAsync(
            string key,
            Transform parent = null,
            CancellationToken cancellationToken = default)
        {
            CompatibilityInstanceRecord record = await CreateCompatibilityInstanceAsync(
                key,
                parent,
                cancellationToken);
            return record.Instance;
        }

        public async UniTask<T> InstantiateAsync<T>(
            string key,
            Transform parent = null,
            CancellationToken cancellationToken = default)
            where T : Component
        {
            CompatibilityInstanceRecord record = await CreateCompatibilityInstanceAsync(
                key,
                parent,
                cancellationToken);
            T component = record.Instance.GetComponent<T>();
            if (component != null)
            {
                return component;
            }

            ReleaseInstance(record.Instance);
            throw new InvalidOperationException(
                $"Instantiated resource '{key}' does not contain component {typeof(T).Name}.");
        }

        public async UniTask<IInstanceLease<T>> InstantiateLeaseAsync<T>(
            string key,
            Transform parent = null,
            CancellationToken cancellationToken = default)
            where T : Component
        {
            IAssetLease<GameObject> sourceLease = await AcquireAsync<GameObject>(
                key,
                cancellationToken);
            GameObject instance = null;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                instance = InstantiatePrefab(sourceLease.Asset, parent, key);
                cancellationToken.ThrowIfCancellationRequested();

                T component = instance.GetComponent<T>();
                if (component == null)
                {
                    throw new InvalidOperationException(
                        $"Instantiated resource '{key}' does not contain component {typeof(T).Name}.");
                }

                var instanceLease = new InstanceLease<T>(this, instance, component, sourceLease);
                lock (syncRoot)
                {
                    ThrowIfDisposedLocked();
                    leasedInstances.Add(instanceLease);
                }

                return instanceLease;
            }
            catch
            {
                if (instance != null)
                {
                    UnityEngine.Object.Destroy(instance);
                }

                sourceLease.Dispose();
                throw;
            }
        }

        public void ReleaseAsset<T>(T asset) where T : UnityEngine.Object
        {
            if (ReferenceEquals(asset, null))
            {
                return;
            }

            AssetRecord recordToRelease = null;
            lock (syncRoot)
            {
                if (disposed)
                {
                    return;
                }

                int instanceId;
                try
                {
                    instanceId = asset.GetInstanceID();
                }
                catch
                {
                    return;
                }

                if (!recordsByAssetId.TryGetValue(instanceId, out AssetRecord record)
                    || record.CompatibilityReferenceCount <= 0)
                {
                    return;
                }

                record.CompatibilityReferenceCount--;
                record.LastAccessSequence = ++accessSequence;
                if (record.TotalReferenceCount == 0
                    && record.WaiterCount == 0
                    && !record.IsLoading)
                {
                    RemoveRecordLocked(record);
                    recordToRelease = record;
                }
            }

            ReleasePhysicalAsset(recordToRelease?.Asset);
        }

        public void ReleaseInstance(GameObject instance)
        {
            if (ReferenceEquals(instance, null))
            {
                return;
            }

            CompatibilityInstanceRecord ownedInstance = null;
            lock (syncRoot)
            {
                if (disposed)
                {
                    return;
                }

                int instanceId;
                try
                {
                    instanceId = instance.GetInstanceID();
                }
                catch
                {
                    return;
                }

                if (compatibilityInstances.TryGetValue(instanceId, out ownedInstance))
                {
                    compatibilityInstances.Remove(instanceId);
                }
            }

            if (ownedInstance == null)
            {
                return;
            }

            if (instance != null)
            {
                UnityEngine.Object.Destroy(instance);
            }

            ReleaseCompatibilityReference(ownedInstance.AssetRecord, releaseImmediatelyWhenUnused: true);
        }

        public void TrimUnused()
        {
            List<AssetRecord> recordsToRelease = null;
            lock (syncRoot)
            {
                if (disposed)
                {
                    return;
                }

                foreach (AssetRecord record in records.Values)
                {
                    if (record.Asset == null
                        || record.IsLoading
                        || record.WaiterCount != 0
                        || record.TotalReferenceCount != 0)
                    {
                        continue;
                    }

                    recordsToRelease ??= new List<AssetRecord>();
                    recordsToRelease.Add(record);
                }

                if (recordsToRelease != null)
                {
                    for (int i = 0; i < recordsToRelease.Count; i++)
                    {
                        RemoveRecordLocked(recordsToRelease[i]);
                    }
                }
            }

            if (recordsToRelease == null)
            {
                return;
            }

            for (int i = 0; i < recordsToRelease.Count; i++)
            {
                ReleasePhysicalAsset(recordsToRelease[i].Asset);
            }
        }

        public AssetProviderStats GetStats()
        {
            lock (syncRoot)
            {
                int loaded = 0;
                int unused = 0;
                int references = 0;
                int pending = 0;

                foreach (AssetRecord record in records.Values)
                {
                    if (record.IsLoading)
                    {
                        pending++;
                    }

                    if (record.Asset == null)
                    {
                        continue;
                    }

                    loaded++;
                    references += record.TotalReferenceCount;
                    if (record.TotalReferenceCount == 0)
                    {
                        unused++;
                    }
                }

                return new AssetProviderStats(
                    loaded,
                    unused,
                    references,
                    pending,
                    compatibilityInstances.Count + leasedInstances.Count);
            }
        }

        public void Dispose()
        {
            AssetRecord[] ownedRecords;
            CompatibilityInstanceRecord[] rawInstances;
            InstanceLeaseBase[] ownedLeases;

            lock (syncRoot)
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                ownedRecords = new AssetRecord[records.Count];
                records.Values.CopyTo(ownedRecords, 0);
                rawInstances = new CompatibilityInstanceRecord[compatibilityInstances.Count];
                compatibilityInstances.Values.CopyTo(rawInstances, 0);
                ownedLeases = new InstanceLeaseBase[leasedInstances.Count];
                leasedInstances.CopyTo(ownedLeases);

                records.Clear();
                recordsByAssetId.Clear();
                compatibilityInstances.Clear();
                leasedInstances.Clear();
            }

            for (int i = 0; i < rawInstances.Length; i++)
            {
                GameObject instance = rawInstances[i].Instance;
                if (instance != null)
                {
                    UnityEngine.Object.Destroy(instance);
                }
            }

            for (int i = 0; i < ownedLeases.Length; i++)
            {
                ownedLeases[i]?.DisposeOwnedResources();
            }

            for (int i = 0; i < ownedRecords.Length; i++)
            {
                ReleasePhysicalAsset(ownedRecords[i].Asset);
            }
        }

        private async UniTask<AcquiredAsset<T>> AcquireRecordAsync<T>(
            string key,
            OwnershipKind ownershipKind,
            CancellationToken cancellationToken)
            where T : UnityEngine.Object
        {
            ValidateKey(key);
            cancellationToken.ThrowIfCancellationRequested();

            AssetRecord record;
            Task<UnityEngine.Object> loadTask;
            Type requestedType = typeof(T);
            lock (syncRoot)
            {
                ThrowIfDisposedLocked();
                record = GetOrCreateRecordLocked(key, requestedType);
                record.WaiterCount++;
                record.LastAccessSequence = ++accessSequence;
                loadTask = record.LoadTask;
            }

            try
            {
                UnityEngine.Object loaded = await loadTask
                    .AsUniTask()
                    .AttachExternalCancellation(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                lock (syncRoot)
                {
                    ThrowIfDisposedLocked();
                    if (!records.TryGetValue(key, out AssetRecord current)
                        || !ReferenceEquals(current, record)
                        || record.Asset == null)
                    {
                        throw new InvalidOperationException(
                            $"Asset record '{key}' is no longer owned by this provider.");
                    }

                    if (!requestedType.IsAssignableFrom(record.CanonicalType))
                    {
                        throw CreateIncompatibleTypeException(
                            key,
                            record.CanonicalType,
                            requestedType);
                    }

                    if (!(loaded is T typedAsset))
                    {
                        throw CreateIncompatibleTypeException(
                            key,
                            loaded != null ? loaded.GetType() : record.CanonicalType,
                            requestedType);
                    }

                    if (ownershipKind == OwnershipKind.Lease)
                    {
                        record.LeaseReferenceCount++;
                    }
                    else
                    {
                        record.CompatibilityReferenceCount++;
                    }

                    record.LastAccessSequence = ++accessSequence;
                    return new AcquiredAsset<T>(record, typedAsset);
                }
            }
            finally
            {
                lock (syncRoot)
                {
                    if (record.WaiterCount > 0)
                    {
                        record.WaiterCount--;
                    }
                }
            }
        }

        private AssetRecord GetOrCreateRecordLocked(string key, Type requestedType)
        {
            if (records.TryGetValue(key, out AssetRecord record))
            {
                if (record.Asset != null)
                {
                    if (!requestedType.IsAssignableFrom(record.CanonicalType))
                    {
                        throw CreateIncompatibleTypeException(
                            key,
                            record.CanonicalType,
                            requestedType);
                    }

                    return record;
                }

                if (record.IsLoading)
                {
                    if (!ArePendingTypesCompatible(record.RequestedType, requestedType))
                    {
                        throw CreateIncompatibleTypeException(
                            key,
                            record.RequestedType,
                            requestedType);
                    }

                    return record;
                }

                records.Remove(key);
            }

            record = new AssetRecord(key, requestedType)
            {
                IsLoading = true,
                LastAccessSequence = ++accessSequence
            };
            records.Add(key, record);
            record.LoadTask = LoadRecordAsync(record, requestedType).AsTask();
            return record;
        }

        private async UniTask<UnityEngine.Object> LoadRecordAsync(
            AssetRecord record,
            Type requestedType)
        {
            UnityEngine.Object asset = null;
            try
            {
                asset = await assetLoader.LoadAsync(record.Key, requestedType);
                if (asset == null)
                {
                    throw new InvalidOperationException(
                        $"Resource '{record.Key}' could not be loaded as {requestedType.Name}.");
                }

                Type actualType = asset.GetType();
                if (!requestedType.IsAssignableFrom(actualType))
                {
                    throw CreateIncompatibleTypeException(
                        record.Key,
                        actualType,
                        requestedType);
                }

                bool providerStillOwnsRecord;
                lock (syncRoot)
                {
                    record.IsLoading = false;
                    providerStillOwnsRecord = !disposed
                        && records.TryGetValue(record.Key, out AssetRecord current)
                        && ReferenceEquals(current, record);
                    if (providerStillOwnsRecord)
                    {
                        record.Asset = asset;
                        record.CanonicalType = actualType;
                        record.LastAccessSequence = ++accessSequence;
                        recordsByAssetId[asset.GetInstanceID()] = record;
                    }
                }

                if (!providerStillOwnsRecord)
                {
                    ReleasePhysicalAsset(asset);
                }

                return asset;
            }
            catch
            {
                lock (syncRoot)
                {
                    record.IsLoading = false;
                    if (records.TryGetValue(record.Key, out AssetRecord current)
                        && ReferenceEquals(current, record)
                        && record.TotalReferenceCount == 0)
                    {
                        RemoveRecordLocked(record);
                    }
                }

                if (asset != null)
                {
                    ReleasePhysicalAsset(asset);
                }

                throw;
            }
        }

        private async UniTask<CompatibilityInstanceRecord> CreateCompatibilityInstanceAsync(
            string key,
            Transform parent,
            CancellationToken cancellationToken)
        {
            AcquiredAsset<GameObject> acquired = await AcquireRecordAsync<GameObject>(
                key,
                OwnershipKind.Compatibility,
                cancellationToken);
            GameObject instance = null;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                instance = InstantiatePrefab(acquired.Asset, parent, key);
                cancellationToken.ThrowIfCancellationRequested();

                var instanceRecord = new CompatibilityInstanceRecord(instance, acquired.Record);
                lock (syncRoot)
                {
                    ThrowIfDisposedLocked();
                    compatibilityInstances[instance.GetInstanceID()] = instanceRecord;
                }

                return instanceRecord;
            }
            catch
            {
                if (instance != null)
                {
                    UnityEngine.Object.Destroy(instance);
                }

                ReleaseCompatibilityReference(acquired.Record, releaseImmediatelyWhenUnused: true);
                throw;
            }
        }

        private GameObject InstantiatePrefab(GameObject prefab, Transform parent, string key)
        {
            if (prefab == null)
            {
                throw new InvalidOperationException(
                    $"Resource '{key}' is not a valid GameObject prefab.");
            }

            GameObject instance = objectResolver != null
                ? objectResolver.Instantiate(prefab, parent)
                : UnityEngine.Object.Instantiate(prefab, parent, false);
            if (instance == null)
            {
                throw new InvalidOperationException($"Failed to instantiate resource '{key}'.");
            }

            return instance;
        }

        private void ReleaseLeaseReference(AssetRecord record)
        {
            if (record == null)
            {
                return;
            }

            lock (syncRoot)
            {
                if (disposed
                    || !records.TryGetValue(record.Key, out AssetRecord current)
                    || !ReferenceEquals(current, record)
                    || record.LeaseReferenceCount <= 0)
                {
                    return;
                }

                record.LeaseReferenceCount--;
                record.LastAccessSequence = ++accessSequence;
            }
        }

        private void ReleaseCompatibilityReference(
            AssetRecord record,
            bool releaseImmediatelyWhenUnused)
        {
            if (record == null)
            {
                return;
            }

            AssetRecord recordToRelease = null;
            lock (syncRoot)
            {
                if (disposed
                    || !records.TryGetValue(record.Key, out AssetRecord current)
                    || !ReferenceEquals(current, record)
                    || record.CompatibilityReferenceCount <= 0)
                {
                    return;
                }

                record.CompatibilityReferenceCount--;
                record.LastAccessSequence = ++accessSequence;
                if (releaseImmediatelyWhenUnused
                    && record.TotalReferenceCount == 0
                    && record.WaiterCount == 0
                    && !record.IsLoading)
                {
                    RemoveRecordLocked(record);
                    recordToRelease = record;
                }
            }

            ReleasePhysicalAsset(recordToRelease?.Asset);
        }

        private void ReleaseInstanceLease(InstanceLeaseBase instanceLease)
        {
            bool owned;
            lock (syncRoot)
            {
                owned = leasedInstances.Remove(instanceLease);
            }

            if (owned)
            {
                instanceLease.DisposeOwnedResources();
            }
        }

        private bool IsLeaseRecordValid(AssetRecord record, UnityEngine.Object asset)
        {
            if (record == null || asset == null)
            {
                return false;
            }

            lock (syncRoot)
            {
                return !disposed
                    && records.TryGetValue(record.Key, out AssetRecord current)
                    && ReferenceEquals(current, record)
                    && ReferenceEquals(record.Asset, asset);
            }
        }

        private void RemoveRecordLocked(AssetRecord record)
        {
            if (record == null)
            {
                return;
            }

            if (records.TryGetValue(record.Key, out AssetRecord current)
                && ReferenceEquals(current, record))
            {
                records.Remove(record.Key);
            }

            if (record.Asset != null)
            {
                recordsByAssetId.Remove(record.Asset.GetInstanceID());
            }
        }

        private void ReleasePhysicalAsset(UnityEngine.Object asset)
        {
            if (asset == null || asset is GameObject || asset is Component)
            {
                return;
            }

            assetLoader.Unload(asset);
        }

        private static void ValidateKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Asset key must not be empty.", nameof(key));
            }
        }

        private static bool ArePendingTypesCompatible(Type existing, Type requested)
        {
            return existing == requested
                || existing.IsAssignableFrom(requested)
                || requested.IsAssignableFrom(existing);
        }

        private static InvalidOperationException CreateIncompatibleTypeException(
            string key,
            Type canonicalType,
            Type requestedType)
        {
            string canonicalName = canonicalType != null ? canonicalType.Name : "unknown";
            return new InvalidOperationException(
                $"Asset key '{key}' is owned with canonical type {canonicalName} and cannot be requested as {requestedType.Name}.");
        }

        private void ThrowIfDisposedLocked()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(ResourcesAssetProvider));
            }
        }

        private enum OwnershipKind
        {
            Lease,
            Compatibility
        }

        private sealed class AssetRecord
        {
            public AssetRecord(string key, Type requestedType)
            {
                Key = key;
                RequestedType = requestedType;
            }

            public string Key { get; }
            public Type RequestedType { get; }
            public Type CanonicalType { get; set; }
            public UnityEngine.Object Asset { get; set; }
            public Task<UnityEngine.Object> LoadTask { get; set; }
            public int LeaseReferenceCount { get; set; }
            public int CompatibilityReferenceCount { get; set; }
            public int WaiterCount { get; set; }
            public bool IsLoading { get; set; }
            public long LastAccessSequence { get; set; }
            public int TotalReferenceCount => LeaseReferenceCount + CompatibilityReferenceCount;
        }

        private readonly struct AcquiredAsset<T> where T : UnityEngine.Object
        {
            public AcquiredAsset(AssetRecord record, T asset)
            {
                Record = record;
                Asset = asset;
            }

            public AssetRecord Record { get; }
            public T Asset { get; }
        }

        private sealed class CompatibilityInstanceRecord
        {
            public CompatibilityInstanceRecord(GameObject instance, AssetRecord assetRecord)
            {
                Instance = instance;
                AssetRecord = assetRecord;
            }

            public GameObject Instance { get; }
            public AssetRecord AssetRecord { get; }
        }

        private sealed class AssetLease<T> : IAssetLease<T> where T : UnityEngine.Object
        {
            private ResourcesAssetProvider provider;
            private AssetRecord record;
            private T asset;
            private bool leaseDisposed;

            public AssetLease(ResourcesAssetProvider provider, AssetRecord record, T asset)
            {
                this.provider = provider;
                this.record = record;
                this.asset = asset;
                Key = record.Key;
            }

            public string Key { get; }
            public T Asset => IsValid ? asset : null;
            public bool IsValid => !leaseDisposed
                && provider != null
                && provider.IsLeaseRecordValid(record, asset);

            public void Dispose()
            {
                if (leaseDisposed)
                {
                    return;
                }

                leaseDisposed = true;
                ResourcesAssetProvider owner = provider;
                AssetRecord ownedRecord = record;
                provider = null;
                record = null;
                asset = null;
                owner?.ReleaseLeaseReference(ownedRecord);
            }
        }

        private abstract class InstanceLeaseBase
        {
            internal abstract void DisposeOwnedResources();
        }

        private sealed class InstanceLease<T> : InstanceLeaseBase, IInstanceLease<T>
            where T : Component
        {
            private ResourcesAssetProvider provider;
            private IAssetLease<GameObject> sourceLease;
            private T instance;
            private GameObject gameObject;
            private bool instanceDisposed;

            public InstanceLease(
                ResourcesAssetProvider provider,
                GameObject gameObject,
                T instance,
                IAssetLease<GameObject> sourceLease)
            {
                this.provider = provider;
                this.gameObject = gameObject;
                this.instance = instance;
                this.sourceLease = sourceLease;
            }

            public T Instance => IsValid ? instance : null;
            public GameObject GameObject => IsValid ? gameObject : null;
            public bool IsValid => !instanceDisposed
                && provider != null
                && sourceLease != null
                && sourceLease.IsValid
                && gameObject != null
                && instance != null;

            public void Dispose()
            {
                if (instanceDisposed)
                {
                    return;
                }

                ResourcesAssetProvider owner = provider;
                if (owner == null)
                {
                    DisposeOwnedResources();
                    return;
                }

                owner.ReleaseInstanceLease(this);
            }

            internal override void DisposeOwnedResources()
            {
                if (instanceDisposed)
                {
                    return;
                }

                instanceDisposed = true;
                GameObject ownedGameObject = gameObject;
                IAssetLease<GameObject> ownedSourceLease = sourceLease;
                provider = null;
                gameObject = null;
                instance = null;
                sourceLease = null;

                if (ownedGameObject != null)
                {
                    UnityEngine.Object.Destroy(ownedGameObject);
                }

                ownedSourceLease?.Dispose();
            }
        }
    }

    internal interface IResourcesAssetLoader
    {
        UniTask<UnityEngine.Object> LoadAsync(string key, Type requestedType);
        void Unload(UnityEngine.Object asset);
    }

    internal sealed class UnityResourcesAssetLoader : IResourcesAssetLoader
    {
        public async UniTask<UnityEngine.Object> LoadAsync(string key, Type requestedType)
        {
            ResourceRequest request = Resources.LoadAsync(key, requestedType);
            while (!request.isDone)
            {
                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            return request.asset;
        }

        public void Unload(UnityEngine.Object asset)
        {
            if (asset != null)
            {
                Resources.UnloadAsset(asset);
            }
        }
    }
}
