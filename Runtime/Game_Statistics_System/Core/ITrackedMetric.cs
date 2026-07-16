using System;
using System.Threading.Tasks;

namespace Kaddumi.UnityTools.Statistics.Core
{
    /// <summary>
    /// Handles raw I/O operations for saving and loading metric data.
    /// Acts as the Repository Pattern interface.
    /// </summary>
    public interface IMetricStorage
    {
        void SaveSnapshot(string dataPayload);
        void SaveSnapshotAsync(string dataPayload);
        string LoadSnapshot();

        Task<string> LoadSnapshotAsync();
        void ClearSnapshot();
    }

    /// <summary>
    /// Defines how a specific data point is updated.
    /// Acts as the Strategy Pattern interface for different metric types.
    /// </summary>
    public interface ITrackedMetric
    {
        string MetricId { get; }
        void UpdateFromObject(object delta);
        object GetValueAsObject();
        string GetSerializedValue();
        void LoadFromSerializedValue(string jsonValue);
        void Reset();
    }

    public interface ITrackedMetric<T> : ITrackedMetric
    {
        T GetValue();
        void Update(T delta);
    }

    /// <summary>
    /// Allows external systems to listen to statistical changes.
    /// Acts as the Observer interface in the Pub/Sub pattern.
    /// </summary>
    public interface IMetricObserver
    {
        void OnMetricUpdated(string metricId, object newValue);
    }
}