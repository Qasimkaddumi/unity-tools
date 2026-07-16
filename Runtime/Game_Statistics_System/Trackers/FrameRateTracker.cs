using Kaddumi.UnityTools.Analytics;
using Kaddumi.UnityTools.Statistics.Core;
using Kaddumi.UnityTools.Statistics.Metrics;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Kaddumi.UnityTools.Statistics.Trackers
{

    public class FrameRateTracker : MonoBehaviour
    {
        private readonly List<float> frameTimes = new List<float>();

        private FrameRateMetric frameRateMetric;
        private float reportInterval = 60f;

        private float timer;


        private const string FRAME_RATE_METRIC_ID = "average_frame_rate";
        private const string FRAME_RATE_EVENT_NAME = "frame_rate_report";
        private void Start()
        {
            frameRateMetric = new FrameRateMetric(FRAME_RATE_METRIC_ID);
            StatisticsManager.Instance.RegisterMetric(frameRateMetric);
            reportInterval = Application.targetFrameRate;
        }


        public FrameRateMetricData CalculateMetrics()
        {
            if (frameTimes.Count == 0) return new FrameRateMetricData(0, 0);

            float totalTime = frameTimes.Sum();
            float avgFps = frameTimes.Count / totalTime;

            // Sort to find the slowest frames (highest delta times)
            var sortedTimes = frameTimes.OrderByDescending(t => t).ToList();
            int indexOnePercent = Mathf.Max(0, Mathf.CeilToInt(frameTimes.Count * 0.01f) - 1);
            float onePercentLow = 1f / sortedTimes[indexOnePercent];

            return new FrameRateMetricData(avgFps, onePercentLow);
        }


        private void Update()
        {
            frameTimes.Add(Time.unscaledDeltaTime);
            timer += Time.unscaledDeltaTime;

            if (timer >= reportInterval)
            {
                SendReport();
                timer = 0;
            }
        }

        private void SendReport()
        {
            FrameRateMetricData data = CalculateMetrics();

            StatisticsManager.Instance.RecordPlayerAction(FRAME_RATE_METRIC_ID, data);
            AnalyticsManager.Instance.Service.LogEvent(FRAME_RATE_EVENT_NAME, new Dictionary<string, object>
            {
                { "average_frame_rate", data.AverageFrameRate },
                { "one_percent_low_frame_rate", data.OnePercentLowFrameRate }
            });
            frameRateMetric.Reset();
        }


    }
}
