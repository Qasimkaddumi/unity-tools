using System;
using System.Collections.Generic;

namespace Kaddumi.UnityTools.GlobalEvents
{

public static class GlobalEventSystem
{
    private static readonly Dictionary<Type, List<Delegate>> subscribers = new Dictionary<Type, List<Delegate>>();

    public static void Listen<T>(Action<T> handler)
    {
        Type type = typeof(T);
        if (!subscribers.ContainsKey(type))
        {
            subscribers[type] = new List<Delegate>();
        }
        subscribers[type].Add(handler);
    }

    public static void Unlisten<T>(Action<T> handler)
    {
        Type type = typeof(T);
        if (subscribers.ContainsKey(type))
        {
            subscribers[type].Remove(handler);
        }
    }

    public static void Raise<T>(T eventData)
    {
        Type type = typeof(T);
        if (subscribers.ContainsKey(type))
        {
            // Create a copy to avoid modification during iteration
            var handlers = new List<Delegate>(subscribers[type]);
            foreach (var handler in handlers)
            {
                (handler as Action<T>)?.Invoke(eventData);
            }
        }
    }
}

}
