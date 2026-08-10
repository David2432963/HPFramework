using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace HP.Framework.UI
{
    /// <summary>
    /// Compile-time bridge for the optional DOTween integration.
    /// Projects without DOTween keep using the built-in animation path.
    /// </summary>
    internal static class DotweenBridge
    {
        private static readonly Lazy<Api> api = new Lazy<Api>(BuildApi, true);

        public static bool IsAvailable => api.Value.IsAvailable;

        public static bool TryCreateSequence(out object sequence)
        {
            sequence = Invoke(api.Value.CreateSequenceMethod);
            return sequence != null;
        }

        public static bool TryCreateFadeTween(
            CanvasGroup target,
            float endValue,
            float duration,
            out object tween)
        {
            if (!IsAvailable || target == null || duration <= 0f)
            {
                tween = null;
                return false;
            }

            tween = Invoke(
                api.Value.CanvasGroupFadeMethod,
                target,
                endValue,
                duration);
            return tween != null;
        }

        public static bool TryCreateScaleTween(
            Transform target,
            Vector3 endValue,
            float duration,
            out object tween)
        {
            if (!IsAvailable || target == null || duration <= 0f)
            {
                tween = null;
                return false;
            }

            tween = Invoke(
                api.Value.TransformScaleMethod,
                target,
                endValue,
                duration);
            return tween != null;
        }

        public static bool TryCreateAnchorPosTween(
            RectTransform target,
            Vector2 endValue,
            float duration,
            out object tween)
        {
            if (!IsAvailable || target == null || duration <= 0f)
            {
                tween = null;
                return false;
            }

            tween = Invoke(
                api.Value.RectTransformAnchorPosMethod,
                target,
                endValue,
                duration,
                false);
            return tween != null;
        }

        public static bool TrySetEase(object tween, UIAnimationPresetSO.Timing timing)
        {
            if (!IsTween(tween) || timing == null)
            {
                return false;
            }

            if (timing.UseCustomCurve)
            {
                AnimationCurve curve = timing.Curve
                    ?? AnimationCurve.Linear(0f, 0f, 1f, 1f);
                return TryInvokeGenericExtension(
                    api.Value.SetEaseCurveMethod,
                    tween,
                    curve);
            }

            UIEase uiEase = timing.Ease == UIEase.Unset
                ? UIEase.Linear
                : timing.Ease;
            object ease = Enum.ToObject(api.Value.EaseType, (int)uiEase);
            return TryInvokeGenericExtension(
                api.Value.SetEaseEnumMethod,
                tween,
                ease);
        }

        public static bool TrySetEase(object tween, AnimationCurve curve)
        {
            if (!IsTween(tween) || curve == null)
            {
                return false;
            }

            return TryInvokeGenericExtension(
                api.Value.SetEaseCurveMethod,
                tween,
                curve);
        }

        public static bool TrySetUpdateIndependent(object tween)
        {
            if (!IsTween(tween))
            {
                return false;
            }

            return TryInvokeGenericExtension(
                api.Value.SetUpdateMethod,
                tween,
                true);
        }

        public static bool TryOnComplete(object tween, Action onComplete)
        {
            if (!IsTween(tween))
            {
                return false;
            }

            if (onComplete == null)
            {
                return true;
            }

            Delegate callback = Delegate.CreateDelegate(
                api.Value.TweenCallbackType,
                onComplete.Target,
                onComplete.Method,
                false);
            return callback != null
                && TryInvokeGenericExtension(
                    api.Value.OnCompleteMethod,
                    tween,
                    callback);
        }

        public static bool TryKill(object tween)
        {
            if (!IsTween(tween))
            {
                return false;
            }

            Invoke(api.Value.KillMethod, tween, false);
            return true;
        }

        public static bool TryInsert(object sequence, float atPosition, object tween)
        {
            if (!IsSequence(sequence) || !IsTween(tween))
            {
                return false;
            }

            Invoke(
                api.Value.InsertMethod,
                sequence,
                Mathf.Max(0f, atPosition),
                tween);
            return true;
        }

        public static bool TryAppend(object sequence, object tween)
        {
            if (!IsSequence(sequence) || !IsTween(tween))
            {
                return false;
            }

            Invoke(api.Value.AppendMethod, sequence, tween);
            return true;
        }

        public static bool TryAppendInterval(object sequence, float interval)
        {
            if (!IsSequence(sequence))
            {
                return false;
            }

            Invoke(
                api.Value.AppendIntervalMethod,
                sequence,
                Mathf.Max(0f, interval));
            return true;
        }

        private static bool IsTween(object value)
        {
            return IsAvailable
                && value != null
                && api.Value.TweenType.IsInstanceOfType(value);
        }

        private static bool IsSequence(object value)
        {
            return IsAvailable
                && value != null
                && api.Value.SequenceType.IsInstanceOfType(value);
        }

        private static bool TryInvokeGenericExtension(
            MethodInfo method,
            object target,
            object argument)
        {
            if (method == null || target == null)
            {
                return false;
            }

            MethodInfo closedMethod = method.MakeGenericMethod(target.GetType());
            closedMethod.Invoke(null, new[] { target, argument });
            return true;
        }

        private static object Invoke(MethodInfo method, params object[] arguments)
        {
            return method?.Invoke(null, arguments);
        }

        private static Api BuildApi()
        {
            Type dotweenType = FindType("DG.Tweening.DOTween");
            Type shortcutExtensionsType = FindType("DG.Tweening.ShortcutExtensions");
            Type uiExtensionsType = FindFirstType(
                "DG.Tweening.DOTweenModuleUI",
                "DG.Tweening.ShortcutExtensions46",
                "DG.Tweening.ShortcutExtensions");
            Type tweenSettingsExtensionsType = FindType(
                "DG.Tweening.TweenSettingsExtensions");
            Type tweenExtensionsType = FindType("DG.Tweening.TweenExtensions");
            Type tweenType = FindType("DG.Tweening.Tween");
            Type sequenceType = FindType("DG.Tweening.Sequence");
            Type tweenCallbackType = FindType("DG.Tweening.TweenCallback");
            Type easeType = FindType("DG.Tweening.Ease");

            if (dotweenType == null
                || shortcutExtensionsType == null
                || uiExtensionsType == null
                || tweenSettingsExtensionsType == null
                || tweenExtensionsType == null
                || tweenType == null
                || sequenceType == null
                || tweenCallbackType == null
                || easeType == null)
            {
                return Api.Missing;
            }

            Api result = new Api
            {
                TweenType = tweenType,
                SequenceType = sequenceType,
                TweenCallbackType = tweenCallbackType,
                EaseType = easeType,
                CreateSequenceMethod = FindStaticMethod(
                    dotweenType,
                    "Sequence",
                    Type.EmptyTypes),
                CanvasGroupFadeMethod = FindStaticMethod(
                    uiExtensionsType,
                    "DOFade",
                    typeof(CanvasGroup),
                    typeof(float),
                    typeof(float)),
                TransformScaleMethod = FindStaticMethod(
                    shortcutExtensionsType,
                    "DOScale",
                    typeof(Transform),
                    typeof(Vector3),
                    typeof(float)),
                RectTransformAnchorPosMethod = FindStaticMethod(
                    uiExtensionsType,
                    "DOAnchorPos",
                    typeof(RectTransform),
                    typeof(Vector2),
                    typeof(float),
                    typeof(bool)),
                SetEaseCurveMethod = FindGenericExtension(
                    tweenSettingsExtensionsType,
                    "SetEase",
                    typeof(AnimationCurve)),
                SetEaseEnumMethod = FindGenericExtension(
                    tweenSettingsExtensionsType,
                    "SetEase",
                    easeType),
                SetUpdateMethod = FindGenericExtension(
                    tweenSettingsExtensionsType,
                    "SetUpdate",
                    typeof(bool)),
                OnCompleteMethod = FindGenericExtension(
                    tweenSettingsExtensionsType,
                    "OnComplete",
                    tweenCallbackType),
                InsertMethod = FindStaticMethod(
                    tweenSettingsExtensionsType,
                    "Insert",
                    sequenceType,
                    typeof(float),
                    tweenType),
                AppendMethod = FindStaticMethod(
                    tweenSettingsExtensionsType,
                    "Append",
                    sequenceType,
                    tweenType),
                AppendIntervalMethod = FindStaticMethod(
                    tweenSettingsExtensionsType,
                    "AppendInterval",
                    sequenceType,
                    typeof(float)),
                KillMethod = FindStaticMethod(
                    tweenExtensionsType,
                    "Kill",
                    tweenType,
                    typeof(bool))
            };

            result.IsAvailable = result.HasRequiredMethods;
            return result;
        }

        private static MethodInfo FindStaticMethod(
            Type type,
            string methodName,
            params Type[] parameterTypes)
        {
            return type?.GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.Static,
                null,
                parameterTypes,
                null);
        }

        private static MethodInfo FindGenericExtension(
            Type type,
            string methodName,
            Type argumentType)
        {
            if (type == null)
            {
                return null;
            }

            MethodInfo[] methods = type.GetMethods(
                BindingFlags.Public | BindingFlags.Static);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (!method.IsGenericMethodDefinition
                    || !string.Equals(
                        method.Name,
                        methodName,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length == 2
                    && parameters[1].ParameterType == argumentType)
                {
                    return method;
                }
            }

            return null;
        }

        private static Type FindFirstType(params string[] fullNames)
        {
            for (int i = 0; i < fullNames.Length; i++)
            {
                Type type = FindType(fullNames[i]);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private static Type FindType(string fullName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type type = assemblies[i].GetType(fullName, false);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private struct Api
        {
            public static readonly Api Missing = new Api { IsAvailable = false };

            public bool IsAvailable;
            public Type TweenType;
            public Type SequenceType;
            public Type TweenCallbackType;
            public Type EaseType;
            public MethodInfo CreateSequenceMethod;
            public MethodInfo CanvasGroupFadeMethod;
            public MethodInfo TransformScaleMethod;
            public MethodInfo RectTransformAnchorPosMethod;
            public MethodInfo SetEaseCurveMethod;
            public MethodInfo SetEaseEnumMethod;
            public MethodInfo SetUpdateMethod;
            public MethodInfo OnCompleteMethod;
            public MethodInfo InsertMethod;
            public MethodInfo AppendMethod;
            public MethodInfo AppendIntervalMethod;
            public MethodInfo KillMethod;

            public bool HasRequiredMethods =>
                CreateSequenceMethod != null
                && CanvasGroupFadeMethod != null
                && TransformScaleMethod != null
                && RectTransformAnchorPosMethod != null
                && SetEaseCurveMethod != null
                && SetEaseEnumMethod != null
                && SetUpdateMethod != null
                && OnCompleteMethod != null
                && InsertMethod != null
                && AppendMethod != null
                && AppendIntervalMethod != null
                && KillMethod != null;
        }
    }
}
