namespace HP.Framework.Persistence
{
    using System;
    using System.Collections.Concurrent;
    using System.IO;
    using System.Threading;
    using Cysharp.Threading.Tasks;
    using HP.Framework;
    using UnityEngine;

    /// <summary>
    /// Atomic JSON file helper. Unity JsonUtility stays on the main thread while disk I/O runs on a worker.
    /// </summary>
    public static class JsonSaveFile
    {
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> PathLocks =
            new ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.Ordinal);
        public static bool Save<T>(
            string fileName,
            T data,
            string relativeFolder = null,
            bool prettyPrint = true)
        {
            try
            {
                string json = JsonUtility.ToJson(data, prettyPrint);
                string path = GetPath(fileName, relativeFolder);
                SemaphoreSlim pathLock = GetPathLock(path);
                pathLock.Wait();
                try
                {
                    WriteAtomic(path, json);
                    return true;
                }
                finally
                {
                    pathLock.Release();
                }
            }
            catch (Exception exception)
            {
                BaseLog.LogWarning(
                    $"[JsonSaveFile] Could not save '{fileName}': {exception.Message}");
                return false;
            }
        }

        public static async UniTask<bool> SaveAsync<T>(
            string fileName,
            T data,
            string relativeFolder = null,
            bool prettyPrint = true,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string json = JsonUtility.ToJson(data, prettyPrint);
            string path = GetPath(fileName, relativeFolder);

            SemaphoreSlim pathLock = GetPathLock(path);
            try
            {
                await pathLock.WaitAsync(cancellationToken);
                try
                {
                    await UniTask.RunOnThreadPool(
                        () =>
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            WriteAtomic(path, json);
                        },
                        cancellationToken: cancellationToken);
                    return true;
                }
                finally
                {
                    pathLock.Release();
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                BaseLog.LogWarning(
                    $"[JsonSaveFile] Could not save '{fileName}': {exception.Message}");
                return false;
            }
        }

        public static bool TryLoad<T>(
            string fileName,
            out T data,
            string relativeFolder = null)
        {
            try
            {
                string path = GetPath(fileName, relativeFolder);
                SemaphoreSlim pathLock = GetPathLock(path);
                string json;
                pathLock.Wait();
                try
                {
                    json = ReadWithBackup(path);
                }
                finally
                {
                    pathLock.Release();
                }

                if (string.IsNullOrEmpty(json))
                {
                    data = default;
                    return false;
                }

                data = JsonUtility.FromJson<T>(json);
                return true;
            }
            catch (Exception exception)
            {
                BaseLog.LogWarning(
                    $"[JsonSaveFile] Could not load '{fileName}': {exception.Message}");
                data = default;
                return false;
            }
        }

        public static async UniTask<(bool success, T data)> TryLoadAsync<T>(
            string fileName,
            string relativeFolder = null,
            CancellationToken cancellationToken = default)
        {
            string path = GetPath(fileName, relativeFolder);
            string json;

            SemaphoreSlim pathLock = GetPathLock(path);
            try
            {
                await pathLock.WaitAsync(cancellationToken);
                try
                {
                    json = await UniTask.RunOnThreadPool(
                        () =>
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            return ReadWithBackup(path);
                        },
                        cancellationToken: cancellationToken);
                }
                finally
                {
                    pathLock.Release();
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                BaseLog.LogWarning(
                    $"[JsonSaveFile] Could not read '{fileName}': {exception.Message}");
                return (false, default);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(json))
            {
                return (false, default);
            }

            try
            {
                return (true, JsonUtility.FromJson<T>(json));
            }
            catch (Exception exception)
            {
                BaseLog.LogWarning(
                    $"[JsonSaveFile] Could not deserialize '{fileName}': {exception.Message}");
                return (false, default);
            }
        }

        public static T LoadOrDefault<T>(
            string fileName,
            T defaultValue = default,
            string relativeFolder = null)
        {
            return TryLoad(fileName, out T data, relativeFolder) ? data : defaultValue;
        }

        public static bool Exists(string fileName, string relativeFolder = null)
        {
            return File.Exists(GetPath(fileName, relativeFolder));
        }

        public static bool Delete(string fileName, string relativeFolder = null)
        {
            try
            {
                string path = GetPath(fileName, relativeFolder);
                SemaphoreSlim pathLock = GetPathLock(path);
                pathLock.Wait();
                try
                {
                    bool deleted = false;
                    string[] candidates = { path, path + ".bak", path + ".tmp" };
                    for (int i = 0; i < candidates.Length; i++)
                    {
                        string candidate = candidates[i];
                        if (!File.Exists(candidate)) continue;
                        File.Delete(candidate);
                        deleted = true;
                    }

                    return deleted;
                }
                finally
                {
                    pathLock.Release();
                }
            }
            catch (Exception exception)
            {
                BaseLog.LogWarning(
                    $"[JsonSaveFile] Could not delete '{fileName}': {exception.Message}");
                return false;
            }
        }

        public static string GetPath(string fileName, string relativeFolder = null)
        {
            string normalizedFileName = NormalizeFileName(fileName);
            string persistentRoot = Path.GetFullPath(Application.persistentDataPath);
            string combinedPath = persistentRoot;

            if (!string.IsNullOrWhiteSpace(relativeFolder))
            {
                if (Path.IsPathRooted(relativeFolder))
                {
                    throw new ArgumentException(
                        "Relative folder must not be an absolute path.",
                        nameof(relativeFolder));
                }

                string safeFolder = relativeFolder
                    .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
                    .Trim(Path.DirectorySeparatorChar);
                combinedPath = Path.Combine(persistentRoot, safeFolder);
            }

            string finalPath = Path.GetFullPath(Path.Combine(combinedPath, normalizedFileName));
            string rootWithSeparator = persistentRoot.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            StringComparison comparison = Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

            if (!finalPath.StartsWith(rootWithSeparator, comparison))
            {
                throw new ArgumentException(
                    "Relative folder must stay inside Application.persistentDataPath.",
                    nameof(relativeFolder));
            }

            return finalPath;
        }

        private static void WriteAtomic(string path, string json)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string tempPath = path + ".tmp";
            string backupPath = path + ".bak";
            File.WriteAllText(tempPath, json ?? string.Empty);

            if (!File.Exists(path))
            {
                File.Move(tempPath, path);
                return;
            }

            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }

            try
            {
                File.Replace(tempPath, path, backupPath);
            }
            catch (PlatformNotSupportedException)
            {
                File.Copy(path, backupPath, overwrite: true);
                File.Delete(path);
                File.Move(tempPath, path);
            }
        }

        private static string ReadWithBackup(string path)
        {
            if (File.Exists(path))
            {
                return File.ReadAllText(path);
            }

            string backupPath = path + ".bak";
            return File.Exists(backupPath) ? File.ReadAllText(backupPath) : null;
        }

        private static SemaphoreSlim GetPathLock(string path)
        {
            return PathLocks.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));
        }

        private static string NormalizeFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException("File name must not be empty.", nameof(fileName));
            }

            string leafName = Path.GetFileName(fileName);
            if (!string.Equals(leafName, fileName, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "File name must not contain directory traversal or path separators.",
                    nameof(fileName));
            }

            string extension = Path.GetExtension(leafName);
            return string.Equals(
                    extension,
                    BaseConstants.JsonFileExtension,
                    StringComparison.OrdinalIgnoreCase)
                ? leafName
                : leafName + BaseConstants.JsonFileExtension;
        }
    }


}


