
using Kaddumi.UnityTools.Statistics.Core;
using UnityEngine;

namespace Kaddumi.UnityTools.Statistics.Metrics
{
    public class FrameRateMetric : ITrackedMetric<FrameRateMetricData>
    {

        private string metricId;

        private FrameRateMetricData currentValue;

        public FrameRateMetric(string id)
        {
            metricId = id;
            currentValue = new FrameRateMetricData();
        }

        public string MetricId => metricId;

        public string GetSerializedValue()
        {
            return JsonUtility.ToJson(currentValue);


        }

        public FrameRateMetricData GetValue()
        {
            return currentValue;
        }

        public object GetValueAsObject()
        {
            return currentValue;
        }

        public void LoadFromSerializedValue(string jsonValue)
        {
            currentValue = JsonUtility.FromJson<FrameRateMetricData>(jsonValue);
        }

        public void Reset()
        {
            currentValue = new FrameRateMetricData();
        }

        public void Update(FrameRateMetricData delta)
        {
            currentValue = delta;
        }

        public void UpdateFromObject(object delta)
        {
            if (delta is FrameRateMetricData frameRateData)
            {
                Update(frameRateData);
            }
            else
            {
                Debug.LogWarning($"Expected FrameRateMetricData but received {delta.GetType()}");
            }

        }
    }

    [System.Serializable]
    public struct FrameRateMetricData
    {
        public float AverageFrameRate;
        public float OnePercentLowFrameRate;

        public FrameRateMetricData(float averageFrameRate, float onePercentLowFrameRate)
        {
            AverageFrameRate = averageFrameRate;
            OnePercentLowFrameRate = onePercentLowFrameRate;
        }
    }

}