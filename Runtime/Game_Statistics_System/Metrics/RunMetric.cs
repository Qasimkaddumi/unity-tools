using UnityEngine;
using Kaddumi.UnityTools.Statistics.Core;
using System;
using Kaddumi.UnityTools.Statistics.Core.Metrics;

namespace Kaddumi.UnityTools.Statistics.Metrics
{
    public class RunMetric : ITrackedMetric<RunData>
    {
        public string MetricId { get; private set; }

        private RunData currentRunData;

        public RunMetric(string metricId)
        {
            MetricId = metricId;
            Reset();
        }

        public RunData GetValue() => currentRunData;

        public object GetValueAsObject() => currentRunData;

        public void Reset()
        {
            currentRunData = new RunData
            {
                runStartTime = 0,
                runEndTime = 0,
                revivals = 0,
                score = 0
            };
        }

        public void Update(RunData delta)
        {
            currentRunData = delta;
        }

        public void UpdateFromObject(object delta)
        {
            if (delta is RunData runData)
            {
                Update(runData);
            }
            else
            {
                Debug.LogWarning($"Expected RunData but received {delta.GetType()}");
            }
        }

        // Handles its own serialization format (Single Responsibility Principle)
        public string GetSerializedValue()
        {
            return JsonUtility.ToJson(currentRunData);
        }

        public void LoadFromSerializedValue(string jsonValue)
        {
            currentRunData = JsonUtility.FromJson<RunData>(jsonValue);
        }
    }
}

namespace Kaddumi.UnityTools.Statistics.Core.Metrics
{
    [Serializable]
    public struct RunData
    {
        public float runStartTime;
        public float runEndTime;
        public int revivals;
        public int score;
    }
}