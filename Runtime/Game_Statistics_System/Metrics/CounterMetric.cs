using Kaddumi.UnityTools.Statistics.Core;
using System;
using UnityEngine;

namespace Kaddumi.UnityTools.Statistics.Metrics
{
    /// <summary>
    /// Tracks a running total (e.g., total enemies defeated, coins collected).
    /// </summary>
    public class CounterMetric : ITrackedMetric<long>
    {
        public string MetricId { get; private set; }
        private long currentValue;

        public CounterMetric(string id, long initialValue = 0)
        {
            MetricId = id;
            currentValue = initialValue;
        }

        public long GetValue() => currentValue;

        public object GetValueAsObject() => currentValue;

        public void Update(long delta)
        {
            currentValue += delta;
        }

        public void UpdateFromObject(object delta)
        {
            if (delta is long longDelta)
            {
                Update(longDelta);
            }
            else if (delta is int intDelta) // Safely handle standard integer additions
            {
                Update(intDelta);
            }
            else
            {
                Debug.LogWarning($"CounterMetric {MetricId} expected long but received {delta.GetType()}");
            }
        }

        public string GetSerializedValue()
        {
            return currentValue.ToString();
        }

        public void LoadFromSerializedValue(string jsonValue)
        {
            if (long.TryParse(jsonValue, out long parsedValue))
            {
                currentValue = parsedValue;
            }
            else
            {
                Debug.LogWarning($"Failed to parse {jsonValue} to long for CounterMetric {MetricId}.");
            }
        }

        public void Reset()
        {
            currentValue = 0;
        }
    }

    public class DurationMetric : ITrackedMetric<double>
    {
        public string MetricId { get; private set; }
        private double totalSeconds;

        public DurationMetric(string id, double initialSeconds = 0)
        {
            MetricId = id;
            totalSeconds = initialSeconds;
        }

        public double GetValue() => totalSeconds;

        public object GetValueAsObject() => totalSeconds;

        public void Update(double delta)
        {
            totalSeconds += delta;
        }

        public void UpdateFromObject(object delta)
        {
            if (delta is double doubleDelta)
            {
                Update(doubleDelta);
            }
            else if (delta is float floatDelta) // Common when passing Time.deltaTime
            {
                Update((double)floatDelta);
            }
            else if (delta is int intDelta)
            {
                Update(intDelta);
            }
            else
            {
                Debug.LogWarning($"DurationMetric {MetricId} expected double/float but received {delta.GetType()}");
            }
        }

        public string GetSerializedValue()
        {
            return totalSeconds.ToString();
        }

        public void LoadFromSerializedValue(string jsonValue)
        {
            if (double.TryParse(jsonValue, out double parsedValue))
            {
                totalSeconds = parsedValue;
            }
            else
            {
                Debug.LogWarning($"Failed to parse {jsonValue} to double for DurationMetric {MetricId}.");
            }
        }

        public void Reset()
        {
            totalSeconds = 0;
        }
    }

    public class MaxRecordMetric : ITrackedMetric<double>
    {
        public string MetricId { get; private set; }
        private double maxValue;

        public MaxRecordMetric(string id, double initialValue = 0)
        {
            MetricId = id;
            maxValue = initialValue;
        }

        public double GetValue() => maxValue;

        public object GetValueAsObject() => maxValue;

        public void Update(double delta)
        {
            if (delta > maxValue)
            {
                maxValue = delta;
            }
        }

        public void UpdateFromObject(object delta)
        {
            if (delta is double doubleDelta)
            {
                Update(doubleDelta);
            }
            else if (delta is float floatDelta)
            {
                Update((double)floatDelta);
            }
            else if (delta is int intDelta)
            {
                Update(intDelta);
            }
            else
            {
                Debug.LogWarning($"MaxRecordMetric {MetricId} expected numeric type but received {delta.GetType()}");
            }
        }

        public string GetSerializedValue()
        {
            return maxValue.ToString();
        }

        public void LoadFromSerializedValue(string jsonValue)
        {
            if (double.TryParse(jsonValue, out double parsedValue))
            {
                maxValue = parsedValue;
            }
            else
            {
                Debug.LogWarning($"Failed to parse {jsonValue} to double for MaxRecordMetric {MetricId}.");
            }
        }

        public void Reset()
        {
            maxValue = 0;
        }
    }
}