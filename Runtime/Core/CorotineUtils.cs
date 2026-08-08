using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace HP.Framework.Common
{
    public static class CoroutineExtension
    {
        /// <summary>
        /// Runs a coroutine on an explicitly owned MonoBehaviour. The owner's lifetime controls
        /// the coroutine; Base does not create a hidden global coroutine runner.
        /// </summary>
        public static LazyCoroutine Run(this IEnumerator enumerator, MonoBehaviour runner)
        {
            var handler = new LazyCoroutine(enumerator, runner);
            handler.Start();
            return handler;
        }

        public static LazyCoroutine RunDelay(
            this IEnumerator enumerator,
            MonoBehaviour runner,
            float delay)
        {
            var handler = new LazyCoroutine(DelayedCoroutine(enumerator, delay), runner);
            handler.Start();
            return handler;
        }

        public static LazyCoroutine RunUtil(
            this IEnumerator enumerator,
            MonoBehaviour runner,
            Func<bool> condition)
        {
            var handler = new LazyCoroutine(UntilCoroutine(enumerator, condition), runner);
            handler.Start();
            return handler;
        }

        [Obsolete("Global CoroutineDriver was removed in Base 2.0. Use enumerator.Run(owner).", true)]
        public static LazyCoroutine Run(this IEnumerator enumerator)
            => throw new InvalidOperationException(
                "An explicit MonoBehaviour coroutine owner is required.");

        [Obsolete("Global CoroutineDriver was removed in Base 2.0. Use enumerator.RunDelay(owner, delay).", true)]
        public static LazyCoroutine RunDelay(this IEnumerator enumerator, float delay)
            => throw new InvalidOperationException(
                "An explicit MonoBehaviour coroutine owner is required.");

        [Obsolete("Global CoroutineDriver was removed in Base 2.0. Use enumerator.RunUtil(owner, condition).", true)]
        public static LazyCoroutine RunUtil(this IEnumerator enumerator, Func<bool> condition)
            => throw new InvalidOperationException(
                "An explicit MonoBehaviour coroutine owner is required.");

        private static IEnumerator UntilCoroutine(
            IEnumerator enumerator,
            Func<bool> condition)
        {
            if (condition == null)
            {
                throw new ArgumentNullException(nameof(condition));
            }

            while (!condition())
            {
                yield return null;
            }

            yield return enumerator;
        }

        private static IEnumerator DelayedCoroutine(IEnumerator enumerator, float delay)
        {
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            yield return enumerator;
        }
    }

    public sealed class LazyCoroutine
    {
        private IEnumerator coroutine;
        private readonly MonoBehaviour runner;
        private bool paused;
        private bool running;
        private bool stopped;

        private readonly UnityEvent<bool> completed = new UnityEvent<bool>();

        public LazyCoroutine(IEnumerator coroutine, MonoBehaviour runner)
        {
            this.coroutine = coroutine ?? throw new ArgumentNullException(nameof(coroutine));
            this.runner = runner
                ? runner
                : throw new ArgumentNullException(nameof(runner));
        }

        [Obsolete("Global CoroutineDriver was removed in Base 2.0. Pass an explicit MonoBehaviour owner.", true)]
        public LazyCoroutine(IEnumerator coroutine)
            => throw new InvalidOperationException(
                "An explicit MonoBehaviour coroutine owner is required.");

        public bool IsRunning => running;
        public bool IsPaused => paused;

        public void Start()
        {
            if (running || coroutine == null)
            {
                return;
            }

            running = true;
            stopped = false;
            runner.StartCoroutine(CallWrapper());
        }

        public void Stop()
        {
            stopped = true;
            running = false;
        }

        public void Pause()
        {
            paused = true;
        }

        public void Resume()
        {
            paused = false;
        }

        public void OnComplete(UnityAction<bool> action)
        {
            if (action != null)
            {
                completed.AddListener(action);
            }
        }

        private void Finish()
        {
            completed.Invoke(stopped);
            completed.RemoveAllListeners();
            coroutine = null;
        }

        private IEnumerator CallWrapper()
        {
            yield return null;

            while (running)
            {
                if (runner == null)
                {
                    stopped = true;
                    running = false;
                    break;
                }

                if (paused)
                {
                    yield return null;
                }
                else if (coroutine?.MoveNext() == true)
                {
                    yield return coroutine.Current;
                }
                else
                {
                    running = false;
                }
            }

            Finish();
        }
    }
}


