using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kaddumi.UnityTools.Statistics.Core
{

    public class PlayerStatisticsController
    {
        private IMetricStorage storage;
        private List<IMetricObserver> observers;
        private Dictionary<string, ITrackedMetric> activeMetrics;
        private Dictionary<string, string> pendingRestores;
        // Debouncing fields
        private bool isDirty;
        private float saveTimer;
        private float autoSaveInterval;


        public PlayerStatisticsController(IMetricStorage injectedStorage, float saveIntervalSeconds = 5f)
        {
            storage = injectedStorage;
            observers = new List<IMetricObserver>();
            activeMetrics = new Dictionary<string, ITrackedMetric>();
            pendingRestores = new Dictionary<string, string>();
            autoSaveInterval = saveIntervalSeconds;
            isDirty = false;
            saveTimer = 0f;

            LoadStateAsync();
        }

        public void RegisterObserver(IMetricObserver observer)
        {
            if (!observers.Contains(observer))
            {
                observers.Add(observer);
            }
        }

        public void RemoveObserver(IMetricObserver observer)
        {
            if (observers.Contains(observer))
            {
                observers.Remove(observer);
            }
        }

        public void RegisterMetric(ITrackedMetric metric)
        {
            if (!activeMetrics.ContainsKey(metric.MetricId))
            {
                activeMetrics[metric.MetricId] = metric;
                if (pendingRestores.TryGetValue(metric.MetricId, out string savedValue))
                {
                    metric.LoadFromSerializedValue(savedValue);
                    pendingRestores.Remove(metric.MetricId);
                }
            }
        }

        public void RecordAction(string metricId, object delta)
        {
            if (activeMetrics.TryGetValue(metricId, out ITrackedMetric metric))
            {
                metric.UpdateFromObject(delta);
                NotifyObservers(metricId, metric.GetValueAsObject());
                MarkDirty(); // Flag for debounced save
            }
            else
            {
                Debug.LogWarning($"Metric {metricId} not registered! Please register metrics before recording actions.");
            }
        }

        public object GetMetricValue(string metricId)
        {
            if (activeMetrics.TryGetValue(metricId, out ITrackedMetric metric))
            {
                return metric.GetValueAsObject();
            }
            return null;
        }

        public T GetMetric<T>(string metricId) where T : class, ITrackedMetric
        {
            if (activeMetrics.TryGetValue(metricId, out ITrackedMetric metric))
            {
                return metric as T;
            }
            return default;
        }

        public void ResetMetric(string metricId)
        {
            if (activeMetrics.TryGetValue(metricId, out ITrackedMetric metric))
            {
                metric.Reset();
                NotifyObservers(metricId, metric.GetValueAsObject());
                MarkDirty();
            }
        }

        public void Tick(float deltaTime)
        {
            if (!isDirty) return;

            saveTimer += deltaTime;
            if (saveTimer >= autoSaveInterval)
            {
                ForceSave();
            }
        }

        public void ForceSave()
        {
            if (!isDirty) return;

            var payloadData = new PsmSavePayload
            {
                LastUpdated = DateTime.UtcNow.ToString("o"),
                Metrics = new List<MetricSaveData>()
            };

            foreach (var kvp in activeMetrics)
            {
                payloadData.Metrics.Add(new MetricSaveData
                {
                    MetricId = kvp.Value.MetricId,
                    Type = kvp.Value.GetType().Name,
                    Value = kvp.Value.GetSerializedValue()
                });
            }

            string payload = JsonUtility.ToJson(payloadData);
            //storage.SaveSnapshot(payload);
            storage.SaveSnapshotAsync(payload);

            isDirty = false;
            saveTimer = 0f;
        }

        private void MarkDirty()
        {
            isDirty = true;
        }

        private void NotifyObservers(string metricId, object newValue)
        {
            foreach (IMetricObserver observer in observers)
            {
                observer.OnMetricUpdated(metricId, newValue);
            }
        }




        private async void LoadStateAsync()
        {

            try
            {
                string payload = await storage.LoadSnapshotAsync();
                RestorePayload(payload);
            }
            catch (Exception)
            {
                // Wrap in try-catch to prevent app crashes on corrupted local files (per SDD)
                pendingRestores.Clear();
            }
        }
        private void LoadState()
        {
            try
            {
                string payload = storage.LoadSnapshot();
                RestorePayload(payload);
            }
            catch (Exception)
            {
                // Wrap in try-catch to prevent app crashes on corrupted local files (per SDD)
                pendingRestores.Clear();
            }
        }
        private void RestorePayload(string payload)
        {
            if (string.IsNullOrEmpty(payload)) return;

            var payloadData = JsonUtility.FromJson<PsmSavePayload>(payload);
            if (payloadData?.Metrics == null) return;

            foreach (var metricData in payloadData.Metrics)
            {
                // Store the loaded data string. When the metric registers itself, it will be deserialized.
                pendingRestores[metricData.MetricId] = metricData.Value;

                // If the metric is ALREADY registered (due to Unity execution order), load it immediately.
                if (activeMetrics.TryGetValue(metricData.MetricId, out ITrackedMetric activeMetric))
                {
                    activeMetric.LoadFromSerializedValue(metricData.Value);
                    pendingRestores.Remove(metricData.MetricId);
                }
            }
        }


    }
    [Serializable]
    public class MetricSaveData
    {
        public string MetricId;
        public string Type;
        public string Value;
    }

    [Serializable]
    public class PsmSavePayload
    {
        public string LastUpdated;
        public List<MetricSaveData> Metrics;
    }

}



