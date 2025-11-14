# uPools
A lightweight and flexible object pooling system for Unity.
This project is a heavily-modified fork of uPools, featuring an entirely upgraded core pooling manager, advanced configuration options, and several new systems built on top of the original library.

📺 Watch the full tutorial on YouTube: https://youtu.be/kooOjK0K0bk

## Overview

A lightweight and highly flexible object pooling system for Unity with category support, weighted instantiation, callbacks, and advanced spawn behaviors.

Furthermore, it provides support for asynchronous object pooling using UniTask and object pooling with Addressables.

## Features

- Heavily upgraded core pooling manager built on top of uPools

- Scriptable Pool Configs for fully data-driven pooling setup

- Support for multiple prefabs per pool key

- Four instantiation modes: Sequential, Random, Weighted Random, First-Only

- Prefab categories with automatic grouping and filtering

- Customizable transform reset system

  - Prefab Defaults

  - Custom Defaults

  - Provided Runtime Values

  - Keep Current Transform

- Advanced overflow handling

  - Block

  - Reuse Oldest

  - Reuse Random

- Initial & Maximum pool size control with optional prewarm on start

- Built-in logging tools and automatic validation for broken configs

- Callback system with IPoolCallbackReceiver (OnRent, OnReturn, OnInitialize, OnDestroy)

   - Works on root and child objects

- Full support for uPools base features

- Generic ObjectPool<T>

- SharedGameObjectPool as an Instantiate/Destroy replacement

- UniTask async pooling

- Addressables pooling


## 🚀 Setup & Installation

**Highly recommend watching the tutorial: [Tutorial](https://youtu.be/kooOjK0K0bk)**

1. Download the package or drag the folders to the project.

2. Add the Pool System Manager: The PoolSystemManager is the core engine that handles all pooling logic.

3. Create Pool Configurations: Each pool is defined by a PoolConfig. 

You can create them through:

- Right-click in Project Window → Create → uPools → PoolConfig


## How to Use in Code

🟢 Get (Spawn)

```cs
GameObject bullet = PoolSystemManager.Instance.Get("Bullet");
```

With position/rotation:

```cs
PoolSystemManager.Instance.Get(
   "Bullet",
   position: new Vector3(0, 1, 0),
   rotation: Quaternion.identity
);
```


Get a component directly:

```cs
var enemy = PoolSystemManager.Instance.Get<Enemy>("Enemy");
```

🔴 Release (Despawn)

Release a single object:

```cs
PoolSystemManager.Instance.Release(gameObject);
```


Release by key:

```cs
PoolSystemManager.Instance.Release("Bullet");


Release multiple:

```cs
PoolSystemManager.Instance.Release("Bullet", 5);
```

Release all objects from a pool:

```cs
PoolSystemManager.Instance.ReleaseAll("Bullet");
```

Release all pools:

```cs
PoolSystemManager.Instance.ReleaseAll();
```

## 🔄 Callbacks

You can insert custom actions on Rent and Return by implementing `IPoolCallbackReceiver`. To run logic when the object is:

- Created

- Rented (spawned)

- Returned (despawned)

- Destroyed from pool (destroy array)

```cs
public class ExamplePoolable: MonoBehaviour, IPoolCallbackReceiver
{
    public void OnInitialize() { }
    public void OnRent() { }
    public void OnReturn() { }
    public void OnPoolDestroy() { }
}
```

## 📊 Tools & Debugging

**Log pool stats:**

```cs
PoolSystemManager.Instance.LogPoolStatistics();
```

Get active or inactive counts:

```cs
PoolSystemManager.Instance.GetActiveCount("Enemy");
PoolSystemManager.Instance.GetInactiveCount("Enemy");
```

Clear a pool:

```cs
PoolSystemManager.Instance.ClearPool("Enemy");
```

Get all keys:

```cs
PoolSystemManager.Instance.GetAllPoolKeys();
```

In the case of `GameObjectPool` or `SharedGameObjectPool`, this component will be retrieved from the object and its child objects, and the callbacks will be invoked accordingly. For other object pools like `ObjectPool<T>` or pools that inherit from `ObjectPoolBase<T>`, the callbacks are invoked for objects that implement `IPoolCallbackReceiver`.

If you create your own object pool by implementing `IObjectPool<T`, you will need to handle the `IPoolCallbackReceiver` calls yourself. Implement the necessary logic to invoke these callbacks as needed.


## 🔧 Transform Reset Modes

Every spawned object supports 4 reset strategies:

- UsePrefabDefaults – resets to prefab transform

- UseCustomDefaults – uses values defined in the PoolConfig

- UseProvidedValues – uses values passed into Get()

- KeepCurrent – leaves transform untouched

Set manually:
```cs
PoolSystemManager.Instance.SetPoolTransformMode("Bullet", TransformResetMode.KeepCurrent);
```

## UniTask

uPools supports asynchronous object pooling using UniTask. When you add UniTask to your project, you can use `AsyncObjectPool<T>`, `AsyncObjectPoolBase<T>`, and `IAsyncObjectPool<T>` for asynchronous object pooling. These pools provide asynchronous versions of Rent, Prewarm, and CreateInstance while behaving like regular `ObjectPool<T>` in other aspects.

## Addressables

When using Addressables to generate GameObjects, you need to manage the resources of the loaded Prefabs. uPools offers `AddressableGameObjectPool` for this purpose, which can be used similarly to `GameObjectPool`.

```cs
// Address of the Prefab
var key = "Address";
var pool = new AddressableGameObjectPool(key);

// Usage is the same as GameObjectPool
var instance1 = pool.Rent();
var instance2 = pool.Rent(new Vector3(1f, 2f, 3f), Quaternion.identity);

pool.Return(instance1);
pool.Return(instance2);

pool.Dispose();
```

## Summary
A complete, optimized, and fully customizable Unity Object Pooling System with auto-categorizing, transform settings, overflow behavior, and callback support. This is the system I use across multiple projects — stable, flexible, and easy to extend.

Original Upools: https://github.com/AnnulusGames/uPools

You can also use the asynchronous version `AsyncAddressableGameObjectPool` by introducing UniTask.

🔥follow my YouTube @GameDevBox to find more Tutorials and Tips: [GameDevBox](https://www.youtube.com/@GameDevBox)

🔥See the tutorial for how you can set it up: https://youtu.be/kooOjK0K0bk

## Social Media: 
• [X/Twitter](https://x.com/ArianKhatiban)
• [Instagram](https://www.instagram.com/arian.khatiban):
• [LinkedIn](https://www.linkedin.com/in/arian-khatiban-49b30017a/):
• [Discord Server](https://discord.gg/8hpGqBgXmz):
• [itch.io](https://cloudtears.itch.io/):
• [Youtube Subscribe](https://www.youtube.com/channel/UCgXs2PTiL19Rv1qOn1SI7XQ?sub_confirmation=1):
