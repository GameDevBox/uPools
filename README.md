# uPools
A lightweight and flexible object pooling system for Unity.
This project is a heavily-modified fork of uPools, featuring an entirely upgraded core pooling manager, advanced configuration options, and several new systems built on top of the original library.

## Overview

A lightweight and highly flexible object pooling system for Unity with category support, weighted instantiation, callbacks, and advanced spawn behaviors.

Furthermore, it provides support for asynchronous object pooling using UniTask and object pooling with Addressables.

##Features

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

= Full support for uPools base features

- Generic ObjectPool<T>

- SharedGameObjectPool as an Instantiate/Destroy replacement

- UniTask async pooling

- Addressables pooling

🚀 Setup & Installation
Download the package or drag the folders to project.

## Callbacks

You can insert custom actions on Rent and Return by implementing `IPoolCallbackReceiver`.

```cs
public class CallbackExample : MonoBehaviour, IPoolCallbackReceiver
{
    public void OnRent()
    {
        Debug.Log("Rented");
    }

    public void OnReturn()
    {
        Debug.Log("Returned");
    }
}
```

In the case of `GameObjectPool` or `SharedGameObjectPool`, this component will be retrieved from the object and its child objects, and the callbacks will be invoked accordingly. For other object pools like `ObjectPool<T>` or pools that inherit from `ObjectPoolBase<T>`, the callbacks are invoked for objects that implement `IPoolCallbackReceiver`.

If you create your own object pool by implementing `IObjectPool<T`, you will need to handle the `IPoolCallbackReceiver` calls yourself. Implement the necessary logic to invoke these callbacks as needed.

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

You can also use the asynchronous version `AsyncAddressableGameObjectPool` by introducing UniTask.

## License

[MIT License](LICENSE)
