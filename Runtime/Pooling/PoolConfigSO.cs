using System;
using System.Collections.Generic;
using UnityEngine;

namespace HP.Framework.Pooling
{
    [Serializable]
    public sealed class PoolDefinition
    {
        [SerializeField] private GameObject prefab;
        [SerializeField, Min(0)] private int prewarmCount;
        [SerializeField, Min(0)] private int maxInactiveCount = 64;
        [SerializeField, Min(0)] private int trimTargetInactiveCount;

        public GameObject Prefab => prefab;
        public int PrewarmCount => Mathf.Max(0, prewarmCount);
        public int MaxInactiveCount => Mathf.Max(0, maxInactiveCount);
        public int TrimTargetInactiveCount => Mathf.Max(0, trimTargetInactiveCount);
    }

    [CreateAssetMenu(fileName = "PoolConfig", menuName = "HP Framework/Pooling/Pool Config")]
    public sealed class PoolConfigSO : ScriptableObject
    {
        [SerializeField] private List<PoolDefinition> definitions = new List<PoolDefinition>();

        public IReadOnlyList<PoolDefinition> Definitions => definitions;

        public bool TryValidate(out string errorMessage)
        {
            var errors = new List<string>();
            var prefabs = new HashSet<GameObject>();
            for (int i = 0; i < definitions.Count; i++)
            {
                PoolDefinition definition = definitions[i];
                if (definition == null || definition.Prefab == null)
                {
                    errors.Add($"Pool definition at index {i} has no prefab.");
                    continue;
                }

                if (!prefabs.Add(definition.Prefab))
                {
                    errors.Add($"Duplicate pool prefab '{definition.Prefab.name}'.");
                }

                if (definition.PrewarmCount > definition.MaxInactiveCount)
                {
                    errors.Add(
                        $"Pool '{definition.Prefab.name}' prewarms {definition.PrewarmCount} but retains at most {definition.MaxInactiveCount} inactive instances.");
                }

                if (definition.TrimTargetInactiveCount > definition.MaxInactiveCount)
                {
                    errors.Add(
                        $"Pool '{definition.Prefab.name}' trim target exceeds its inactive capacity.");
                }
            }

            errorMessage = string.Join("\n", errors);
            return errors.Count == 0;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!TryValidate(out string errorMessage))
            {
                Debug.LogWarning(errorMessage, this);
            }
        }
#endif
    }
}
