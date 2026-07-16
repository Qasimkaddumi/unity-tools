using System;
using System.Collections;
using UnityEngine;

namespace Kaddumi.UnityTools.Services
{
    public class ServiceLocator : MonoBehaviour
    {
        public static ServiceLocator Instance { get; private set; }


        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }


        public T GetService<T>() where T : class, IService
        {
            if (!TryGetComponent<T>(out var service))
            {
                Debug.LogError($"Service of type {typeof(T)} not found on ServiceLocator.");
            }

            // fall back to children if not found on the same GameObject
            if (service == null)
            {
                service = GetComponentInChildren<T>();
                if (service == null)
                {
                    Debug.LogError($"Service of type {typeof(T)} not found in ServiceLocator or its children.");
                }
            }


            return service;


        }


        public void InitializeAllServices(Action onComplete)
        {

            StartCoroutine(IntializeRoutine(onComplete));

        }

        private IEnumerator IntializeRoutine(Action onComplete)
        {
            var services = GetComponentsInChildren<IService>();
            foreach (var service in services)
            {
                bool initialized = false;
                try
                {
                    service.Initialize(() =>
                    {
                        initialized = true;
                    });
                }
                catch (Exception e)
                {
                    Debug.LogError($"[ServiceLocator] Service {service.GetType().Name} crashed during Init: {e.Message}");
                    // Optional: Mark as initialized so the game continues anyway
                    initialized = true;
                }

                // If it finished synchronously don't wait at all
                if (!initialized)
                {
                    float timeout = 8f; // 8 seconds safety timeout
                    float timer = 0f;
                    
                    while (!initialized && timer < timeout)
                    {
                        timer += Time.unscaledDeltaTime;
                        yield return null;
                    }

                    if (!initialized)
                    {
                        Debug.LogWarning($"[ServiceLocator] Service {service.GetType().Name} initialization timed out after {timeout}s. Continuing...");
                    }
                }
            }


            onComplete?.Invoke();





        }

    }
}

namespace Kaddumi.UnityTools.Services
{
    public interface IService
    {
        void Initialize(Action onComplete);
    }
}