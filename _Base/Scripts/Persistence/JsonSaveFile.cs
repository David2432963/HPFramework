using System;
using System.IO;
using UnityEngine;
using Base;

/// <summary>
/// JSON file helper for small reusable save data.
/// This is best for simple serializable data, not for complex object graphs.
/// </summary>
public static class JsonSaveFile
{
    /// <summary>
    /// Save a serializable object into <see cref="Application.persistentDataPath"/>.
    /// </summary>
    public static bool Save<T>(string fileName, T data, string relativeFolder = null, bool prettyPrint = true)
    {
        try
        {
            var path = GetPath(fileName, relativeFolder);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var tempPath = path + ".tmp";
            File.WriteAllText(tempPath, JsonUtility.ToJson(data, prettyPrint));

            if (File.Exists(path))
            {
                var backupPath = path + ".bak";
                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                }

                File.Move(path, backupPath);
                File.Move(tempPath, path);
                File.Delete(backupPath);
            }
            else
            {
                File.Move(tempPath, path);
            }

            return true;
        }
        catch (Exception exception)
        {
            BaseLog.LogWarning($"[JsonSaveFile] Khong the luu '{fileName}': {exception.Message}");
            return false;
        }
    }

    public static async Cysharp.Threading.Tasks.UniTask<bool> SaveAsync<T>(string fileName, T data, string relativeFolder = null, bool prettyPrint = true)
    {
        bool result = false;
        await Cysharp.Threading.Tasks.UniTask.RunOnThreadPool(() =>
        {
            result = Save(fileName, data, relativeFolder, prettyPrint);
        });
        return result;
    }

    public static async Cysharp.Threading.Tasks.UniTask<(bool success, T data)> TryLoadAsync<T>(string fileName, string relativeFolder = null)
    {
        bool success = false;
        T data = default;
        await Cysharp.Threading.Tasks.UniTask.RunOnThreadPool(() =>
        {
            success = TryLoad(fileName, out data, relativeFolder);
        });
        return (success, data);
    }

    /// <summary>
    /// Try to load a serializable object from disk.
    /// </summary>
    public static bool TryLoad<T>(string fileName, out T data, string relativeFolder = null)
    {
        try
        {
            var path = GetPath(fileName, relativeFolder);
            if (!File.Exists(path))
            {
                data = default;
                return false;
            }

            var json = File.ReadAllText(path);
            data = JsonUtility.FromJson<T>(json);
            return true;
        }
        catch (Exception exception)
        {
            BaseLog.LogWarning($"[JsonSaveFile] Khong the doc '{fileName}': {exception.Message}");
            data = default;
            return false;
        }
    }

    public static T LoadOrDefault<T>(string fileName, T defaultValue = default, string relativeFolder = null)
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
            var path = GetPath(fileName, relativeFolder);
            if (!File.Exists(path))
            {
                return false;
            }

            File.Delete(path);
            return true;
        }
        catch (Exception exception)
        {
            BaseLog.LogWarning($"[JsonSaveFile] Khong the xoa '{fileName}': {exception.Message}");
            return false;
        }
    }

    public static string GetPath(string fileName, string relativeFolder = null)
    {
        var normalizedFileName = NormalizeFileName(fileName);
        var rootPath = Application.persistentDataPath;

        if (!string.IsNullOrWhiteSpace(relativeFolder))
        {
            rootPath = Path.Combine(rootPath, relativeFolder);
        }

        return Path.Combine(rootPath, normalizedFileName);
    }

    private static string NormalizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("File name must not be empty.", nameof(fileName));
        }

        var extension = Path.GetExtension(fileName);
        return string.Equals(extension, BaseConstants.JsonFileExtension, StringComparison.OrdinalIgnoreCase)
            ? fileName
            : fileName + BaseConstants.JsonFileExtension;
    }
}
