using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "AudioLibrary", menuName = "Audio/Audio Library")]
public class AudioLibrarySO : ScriptableObject
{
    [System.Serializable]
    public struct AudioEntry
    {
        public string key;
        public AudioClip clip;
    }

    [System.Serializable]
    public struct AudioClusterEntry
    {
        public string clusterId;
        public List<AudioClip> clips;
    }

    [Header("Single Audio Clips")]
    [SerializeField] private List<AudioEntry> audioEntries = new List<AudioEntry>();

    [Header("Audio Clusters")]
    [SerializeField] private List<AudioClusterEntry> audioClusters = new List<AudioClusterEntry>();

    private Dictionary<string, AudioClip> directClipLookup;
    private Dictionary<string, List<AudioClip>> clusterLookup;

    public bool ContainsKey(string key)
    {
        EnsureLookupInitialized();
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        return directClipLookup.ContainsKey(key) || clusterLookup.ContainsKey(key);
    }

    public bool TryValidate(out string errorMessage)
    {
        List<string> errors = new List<string>();
        HashSet<string> directKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        HashSet<string> clusterKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < audioEntries.Count; i++)
        {
            AudioEntry entry = audioEntries[i];
            string key = entry.key;
            if (string.IsNullOrWhiteSpace(key))
            {
                errors.Add($"Single entry at index {i} has an empty key.");
                continue;
            }

            if (entry.clip == null)
            {
                errors.Add($"Single entry '{key}' has no clip assigned.");
            }

            if (!directKeys.Add(key))
            {
                errors.Add($"Duplicate single audio key '{key}'.");
            }
        }

        for (int i = 0; i < audioClusters.Count; i++)
        {
            AudioClusterEntry cluster = audioClusters[i];
            string clusterId = cluster.clusterId;
            if (string.IsNullOrWhiteSpace(clusterId))
            {
                errors.Add($"Cluster entry at index {i} has an empty clusterId.");
                continue;
            }

            if (!clusterKeys.Add(clusterId))
            {
                errors.Add($"Duplicate audio clusterId '{clusterId}'.");
            }

            if (cluster.clips == null || cluster.clips.Count == 0)
            {
                errors.Add($"Cluster '{clusterId}' contains no audio clips.");
                continue;
            }

            for (int c = 0; c < cluster.clips.Count; c++)
            {
                if (cluster.clips[c] == null)
                {
                    errors.Add($"Cluster '{clusterId}' has a null clip at index {c}.");
                }
            }
        }

        foreach (string key in directKeys)
        {
            if (clusterKeys.Contains(key))
            {
                errors.Add($"Key '{key}' exists as both a single clip and a cluster ID.");
            }
        }

        if (errors.Count > 0)
        {
            errorMessage = string.Join("\n", errors);
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    public void InitializeLookup()
    {
        directClipLookup = new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < audioEntries.Count; i++)
        {
            AudioEntry entry = audioEntries[i];
            if (!string.IsNullOrWhiteSpace(entry.key) && entry.clip != null)
            {
                directClipLookup[entry.key] = entry.clip;
            }
        }

        clusterLookup = new Dictionary<string, List<AudioClip>>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < audioClusters.Count; i++)
        {
            AudioClusterEntry cluster = audioClusters[i];
            if (!string.IsNullOrWhiteSpace(cluster.clusterId) && cluster.clips != null)
            {
                List<AudioClip> validClips = new List<AudioClip>();
                for (int c = 0; c < cluster.clips.Count; c++)
                {
                    if (cluster.clips[c] != null)
                    {
                        validClips.Add(cluster.clips[c]);
                    }
                }

                if (validClips.Count > 0)
                {
                    clusterLookup[cluster.clusterId] = validClips;
                }
            }
        }
    }

    public bool TryGetClip(string key, out AudioClip clip)
    {
        EnsureLookupInitialized();

        if (string.IsNullOrWhiteSpace(key))
        {
            clip = null;
            return false;
        }

        if (directClipLookup.TryGetValue(key, out clip))
        {
            return true;
        }

        if (clusterLookup.TryGetValue(key, out List<AudioClip> clips) && clips.Count > 0)
        {
            int randomIndex = UnityEngine.Random.Range(0, clips.Count);
            clip = clips[randomIndex];
            return true;
        }

        clip = null;
        return false;
    }

    public bool TryGetSequentialClip(string key, ref int currentIndex, out AudioClip clip)
    {
        EnsureLookupInitialized();

        if (string.IsNullOrWhiteSpace(key))
        {
            clip = null;
            return false;
        }

        if (directClipLookup.TryGetValue(key, out clip))
        {
            return true;
        }

        if (clusterLookup.TryGetValue(key, out List<AudioClip> clips) && clips.Count > 0)
        {
            currentIndex = (currentIndex + 1) % clips.Count;
            clip = clips[currentIndex];
            return true;
        }

        clip = null;
        return false;
    }

    public void OnValidate()
    {
        InitializeLookup();
    }

    private void EnsureLookupInitialized()
    {
        if (directClipLookup == null || clusterLookup == null)
        {
            InitializeLookup();
        }
    }
}
