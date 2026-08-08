namespace HP.Framework.Pooling
{
    /// <summary>
    /// Optional lifecycle hooks invoked by PoolManager for reusable instances.
    /// </summary>
    public interface IPoolable
    {
        void OnSpawn();
        void OnDespawn();
    }
}


