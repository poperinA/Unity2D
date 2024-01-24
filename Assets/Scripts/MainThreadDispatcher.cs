using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public class MainThreadDispatcher : MonoBehaviour
{
    private static MainThreadDispatcher instance;

    private readonly Queue<Action> executionQueue = new Queue<Action>();

    public static MainThreadDispatcher Instance
    {
        get
        {
            if (instance == null)
            {
                instance = new GameObject("MainThreadDispatcher").AddComponent<MainThreadDispatcher>();
            }
            return instance;
        }
    }

    public void Enqueue(Action action)
    {
        lock (executionQueue)
        {
            executionQueue.Enqueue(action);
        }
    }

    private void Update()
    {
        lock (executionQueue)
        {
            while (executionQueue.Count > 0)
            {
                executionQueue.Dequeue().Invoke();
            }
        }
    }
}