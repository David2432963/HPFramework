using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
/// Safe conversion helpers for loosely typed argument arrays.
/// </summary>
public static class ObjectArrayExtensions
{
    public static T Get<T>(this object[] data, int index, T defaultValue = default)
    {
        return TryGet(data, index, out T value) ? value : defaultValue;
    }

    public static bool TryGet<T>(this object[] data, int index, out T value)
    {
        value = default;
        if (data == null || index < 0 || index >= data.Length)
        {
            return false;
        }

        return TryConvert(data[index], out value);
    }

    private static bool TryConvert<T>(object rawValue, out T result)
    {
        result = default;
        if (rawValue == null)
        {
            return false;
        }

        if (rawValue is T typedValue)
        {
            result = typedValue;
            return true;
        }

        Type targetType = typeof(T);
        Type nullableType = Nullable.GetUnderlyingType(targetType);
        Type effectiveType = nullableType ?? targetType;

        try
        {
            if (effectiveType.IsEnum)
            {
                if (rawValue is string enumText
                    && Enum.TryParse(effectiveType, enumText, true, out object enumValue))
                {
                    result = (T)enumValue;
                    return true;
                }

                object underlying = Convert.ChangeType(
                    rawValue,
                    Enum.GetUnderlyingType(effectiveType),
                    CultureInfo.InvariantCulture);
                result = (T)Enum.ToObject(effectiveType, underlying);
                return true;
            }

            if (TryConvertUnityValue(rawValue, effectiveType, out object unityValue))
            {
                result = (T)unityValue;
                return true;
            }

            if (effectiveType.IsPrimitive
                || effectiveType == typeof(string)
                || effectiveType == typeof(decimal)
                || effectiveType == typeof(DateTime)
                || effectiveType == typeof(Guid))
            {
                object converted;
                if (effectiveType == typeof(Guid))
                {
                    if (!(rawValue is string guidText)
                        || !Guid.TryParse(guidText, out Guid guid))
                    {
                        return false;
                    }
                    converted = guid;
                }
                else if (effectiveType == typeof(DateTime))
                {
                    if (rawValue is DateTime dateTime)
                    {
                        converted = dateTime;
                    }
                    else if (rawValue is string dateText
                             && DateTime.TryParse(
                                 dateText,
                                 CultureInfo.InvariantCulture,
                                 DateTimeStyles.RoundtripKind,
                                 out DateTime parsedDate))
                    {
                        converted = parsedDate;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    converted = Convert.ChangeType(
                        rawValue,
                        effectiveType,
                        CultureInfo.InvariantCulture);
                }

                result = (T)converted;
                return true;
            }

            if (targetType.IsGenericType
                && targetType.GetGenericTypeDefinition() == typeof(List<>)
                && rawValue is IList sourceList)
            {
                Type elementType = targetType.GetGenericArguments()[0];
                IList targetList = (IList)Activator.CreateInstance(targetType);
                foreach (object item in sourceList)
                {
                    if (!TryConvertToType(item, elementType, out object convertedItem))
                    {
                        return false;
                    }
                    targetList.Add(convertedItem);
                }

                result = (T)targetList;
                return true;
            }

            if (targetType.IsGenericType
                && targetType.GetGenericTypeDefinition() == typeof(Dictionary<,>)
                && rawValue is IDictionary sourceDictionary)
            {
                Type keyType = targetType.GetGenericArguments()[0];
                Type valueType = targetType.GetGenericArguments()[1];
                IDictionary targetDictionary = (IDictionary)Activator.CreateInstance(targetType);

                foreach (DictionaryEntry entry in sourceDictionary)
                {
                    if (!TryConvertToType(entry.Key, keyType, out object convertedKey)
                        || !TryConvertToType(entry.Value, valueType, out object convertedValue))
                    {
                        return false;
                    }
                    targetDictionary.Add(convertedKey, convertedValue);
                }

                result = (T)targetDictionary;
                return true;
            }

            if (rawValue is string json)
            {
                T deserialized = JsonConvert.DeserializeObject<T>(json);
                if (deserialized == null && !targetType.IsValueType)
                {
                    return false;
                }

                result = deserialized;
                return true;
            }

            string serialized = JsonConvert.SerializeObject(rawValue);
            T convertedObject = JsonConvert.DeserializeObject<T>(serialized);
            if (convertedObject == null && !targetType.IsValueType)
            {
                return false;
            }

            result = convertedObject;
            return true;
        }
        catch (Exception)
        {
            result = default;
            return false;
        }
    }

    private static bool TryConvertToType(
        object value,
        Type targetType,
        out object converted)
    {
        converted = null;
        if (value == null)
        {
            return !targetType.IsValueType || Nullable.GetUnderlyingType(targetType) != null;
        }

        if (targetType.IsInstanceOfType(value))
        {
            converted = value;
            return true;
        }

        try
        {
            if (targetType.IsEnum)
            {
                if (value is string enumText)
                {
                    converted = Enum.Parse(targetType, enumText, true);
                }
                else
                {
                    object underlying = Convert.ChangeType(
                        value,
                        Enum.GetUnderlyingType(targetType),
                        CultureInfo.InvariantCulture);
                    converted = Enum.ToObject(targetType, underlying);
                }
                return true;
            }

            if (targetType.IsPrimitive
                || targetType == typeof(string)
                || targetType == typeof(decimal))
            {
                converted = Convert.ChangeType(
                    value,
                    targetType,
                    CultureInfo.InvariantCulture);
                return true;
            }

            converted = JsonConvert.DeserializeObject(
                JsonConvert.SerializeObject(value),
                targetType);
            return converted != null || targetType.IsValueType;
        }
        catch (Exception)
        {
            converted = null;
            return false;
        }
    }

    private static bool TryConvertUnityValue(
        object rawValue,
        Type targetType,
        out object result)
    {
        result = null;
        if (!(rawValue is string text))
        {
            return false;
        }

        if (targetType == typeof(Color))
        {
            if (ColorUtility.TryParseHtmlString(text, out Color color))
            {
                result = color;
                return true;
            }
            return false;
        }

        string[] parts = text.Trim('(', ')').Split(',');
        if (targetType == typeof(Vector2) && parts.Length == 2
            && TryParseFloat(parts[0], out float x2)
            && TryParseFloat(parts[1], out float y2))
        {
            result = new Vector2(x2, y2);
            return true;
        }

        if (targetType == typeof(Vector3) && parts.Length == 3
            && TryParseFloat(parts[0], out float x3)
            && TryParseFloat(parts[1], out float y3)
            && TryParseFloat(parts[2], out float z3))
        {
            result = new Vector3(x3, y3, z3);
            return true;
        }

        if (targetType == typeof(Quaternion) && parts.Length == 4
            && TryParseFloat(parts[0], out float x4)
            && TryParseFloat(parts[1], out float y4)
            && TryParseFloat(parts[2], out float z4)
            && TryParseFloat(parts[3], out float w4))
        {
            result = new Quaternion(x4, y4, z4, w4);
            return true;
        }

        return false;
    }

    private static bool TryParseFloat(string text, out float value)
    {
        return float.TryParse(
            text.Trim(),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out value);
    }
}
