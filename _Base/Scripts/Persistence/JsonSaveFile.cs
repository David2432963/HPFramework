using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using Base;
using UnityEngine;

/// <summary>
/// Atomic JSON file helper. Unity JsonUtility stays on the main thread while disk I/O runs on a worker.
/// </summary>
public static class JsonSaveFile
{
    public static bool Save<T>(
        string fileName,
        T data,
        string relativeFolder = null,
        bool prettyPrint = true)
    {
        try
        {
            string json = JsonUtility.ToJson(data, prettyPrint);
            WriteAtomic(GetPath(fileName, relativeFolder), json);
            return true;
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
            string json = ReadWithBackup(GetPath(fileName, relativeFolder));
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
            bool deleted = false;
            foreach (string candidate in new[] { path, path + ".bak", path + ".tmp" })
            {
                if (!File.Exists(candidate)) continue;
                File.Delete(candidate);
                deleted = true;
            }

            return deleted;
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
        string rootPath = Application.persistentDataPath;

        if (!string.IsNullOrWhiteSpace(relativeFolder))
        {
            string safeFolder = relativeFolder
                .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
                .Trim(Path.DirectorySeparatorChar);
            rootPath = Path.Combine(rootPath, safeFolder);
        }

        return Path.Combine(rootPath, normalizedFileName);
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
