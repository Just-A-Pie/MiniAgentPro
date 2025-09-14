// ÎÄ¼þ£ºUnityDispatcher.cs
using System;
using System.Collections.Concurrent;
using UnityEngine;

public class UnityDispatcher : MonoBehaviour
{
    private static UnityDispatcher _instance;
    private static readonly ConcurrentQueue<Action> _queue = new ConcurrentQueue<Action>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null) return;
        var go = new GameObject("[UnityDispatcher]");
        _instance = go.AddComponent<UnityDispatcher>();
        GameObject.DontDestroyOnLoad(go);
    }

    public static void Enqueue(Action a)
    {
        if (a != null) _queue.Enqueue(a);
    }

    private void Update()
    {
        while (_queue.TryDequeue(out var a))
        {
            try { a(); }
            catch (Exception ex) { Debug.LogError("[Dispatcher] " + ex); }
        }
    }
}
