using System;
using System.Collections.Generic;

namespace uPools
{
    public abstract class ObjectPoolBase<T> : IObjectPool<T>
        where T : class
    {
        protected T[] poolArray;
        protected int count; // Tracks the number of inactive items in pool
        private bool isDisposed;
        private int arrayIndex = 0;

        // Track active objects
        protected readonly HashSet<T> activeObjects = new HashSet<T>();

        // NEW: Stack for available objects beyond initial pool size
        protected readonly Stack<T> overflowStack = new Stack<T>();

        public int Count => count + overflowStack.Count;
        public int ActiveCount => activeObjects.Count;
        public IReadOnlyCollection<T> ActiveObjects => activeObjects;

        public bool IsDisposed => isDisposed;

        protected abstract T CreateInstance();
        protected virtual void OnDestroy(T instance) { }
        protected virtual void OnRent(T instance) { }
        protected virtual void OnReturn(T instance) { }

        public void Initialize(int size)
        {
            poolArray = new T[size];
            count = 0;
            activeObjects.Clear();
            overflowStack.Clear();
        }

        public T Rent()
        {
            ThrowIfDisposed();

            // First, try to find an available object in the fixed array
            for (int i = 0; i < poolArray.Length; i++)
            {
                if (poolArray[arrayIndex] != null)
                {
                    T result = poolArray[arrayIndex];
                    poolArray[arrayIndex] = null;
                    count--;
                    arrayIndex = (arrayIndex + 1) % poolArray.Length;

                    activeObjects.Add(result);
                    OnRent(result);
                    if (result is IPoolCallbackReceiver poolCallbackReceiver)
                    {
                        poolCallbackReceiver.OnRent();
                    }

                    return result;
                }
                arrayIndex = (arrayIndex + 1) % poolArray.Length;
            }

            // Second, try the overflow stack
            if (overflowStack.Count > 0)
            {
                T result = overflowStack.Pop();
                activeObjects.Add(result);
                OnRent(result);
                if (result is IPoolCallbackReceiver poolCallbackReceiver)
                {
                    poolCallbackReceiver.OnRent();
                }
                return result;
            }

            // If no available object, create a new one
            T newInstance = CreateInstance();
            activeObjects.Add(newInstance);
            return newInstance;
        }

        public void Return(T obj)
        {
            ThrowIfDisposed();
            if (obj == null)
            {
                throw new ArgumentNullException(nameof(obj));
            }

            activeObjects.Remove(obj);

            OnReturn(obj);
            if (obj is IPoolCallbackReceiver poolCallbackReceiver)
            {
                poolCallbackReceiver.OnReturn();
            }

            // First, try to find an empty spot in the fixed array
            for (int i = 0; i < poolArray.Length; i++)
            {
                if (poolArray[i] == null)
                {
                    poolArray[i] = obj;
                    count++;
                    return;
                }
            }

            // If the fixed array is full, use the overflow stack
            overflowStack.Push(obj);
        }

        public void ReturnAll()
        {
            ThrowIfDisposed();

            var activeCopy = new List<T>(activeObjects);
            foreach (T obj in activeCopy)
            {
                if (obj != null)
                {
                    Return(obj);
                }
            }
        }

        public void Return(int returnCount)
        {
            ThrowIfDisposed();

            int actualCount = Math.Min(returnCount, activeObjects.Count);
            var enumerator = activeObjects.GetEnumerator();

            var objectsToReturn = new List<T>();
            for (int i = 0; i < actualCount && enumerator.MoveNext(); i++)
            {
                objectsToReturn.Add(enumerator.Current);
            }
            enumerator.Dispose();

            foreach (T obj in objectsToReturn)
            {
                if (obj != null)
                {
                    Return(obj);
                }
            }
        }

        public void Clear()
        {
            ThrowIfDisposed();

            // Clear active objects
            foreach (T obj in activeObjects)
            {
                if (obj != null)
                {
                    OnDestroy(obj);
                }
            }
            activeObjects.Clear();

            // Clear fixed array
            for (int i = 0; i < poolArray.Length; i++)
            {
                if (poolArray[i] != null)
                {
                    OnDestroy(poolArray[i]);
                    poolArray[i] = null;
                }
            }
            count = 0;

            // Clear overflow stack
            foreach (T obj in overflowStack)
            {
                if (obj != null)
                {
                    OnDestroy(obj);
                }
            }
            overflowStack.Clear();
        }

        public void Prewarm(int prewarmCount)
        {
            ThrowIfDisposed();
            for (int i = 0; i < prewarmCount; i++)
            {
                T obj = CreateInstance();
                Return(obj);
            }
        }

        public void Dispose()
        {
            ThrowIfDisposed();
            Clear();
            isDisposed = true;
        }

        void ThrowIfDisposed()
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }
    }
}