using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

//monobehaviour to use unity's update function
public class MainThreadDispatcher : MonoBehaviour
{
    private static MainThreadDispatcher instance;

    //queue to store actions that is to be executed on the main thread
    private readonly Queue<Action> executionQueue = new Queue<Action>();

    //static property to get the singleton instance of the MainThreadDispatcher
    public static MainThreadDispatcher Instance
    {
        get
        {
            //if the instance is null, create a new game object with MainThreadDispatcher component
            if (instance == null)
            {
                instance = new GameObject("MainThreadDispatcher").AddComponent<MainThreadDispatcher>();
            }
            return instance;
        }
    }

    //enqueue an action to be executed on the main thread
    public void Enqueue(Action action)
    {
        //use lock to ensure thread safety when accessing the executionQueue
        lock (executionQueue)
        {
            executionQueue.Enqueue(action);
        }
    }

    private void Update()
    {
        //use lock to ensure thread safety when accessing the executionQueue
        lock (executionQueue)
        {
            //dequeue and invoke all actrions in the executionQueue
            while (executionQueue.Count > 0)
            {
                executionQueue.Dequeue().Invoke();
            }
        }
    }
}