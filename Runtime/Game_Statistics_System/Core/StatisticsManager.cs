using Kaddumi.UnityTools.Statistics.Infrastructure;
using System.IO;
using UnityEngine;

namespace Kaddumi.UnityTools.Statistics.Core
{
    public class StatisticsManager : MonoBehaviour
    {
        private PlayerStatisticsController statisticsController;

        public static StatisticsManager Instance { get; private set; }

        private const string SaveDirectory = "Statistics";
        private const string SaveFileName = "player_stats.json";

        private string statisticsSavePath;


        private void Awake()
        {

            if (Instance != null && Instance != this)
            {
                Destroy(this.gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(this.gameObject);
            statisticsSavePath = Path.Combine(Application.persistentDataPath, SaveDirectory, SaveFileName);
            Debug.Log($"[Statistics] Save Path: {statisticsSavePath}");
            IMetricStorage storage = new JsonMetricStorage(statisticsSavePath);
            statisticsController = new PlayerStatisticsController(storage);
        }


        private void Update()
        {
            statisticsController.Tick(Time.deltaTime);
        }

        public void RegisterMetric(ITrackedMetric metric)
        {
            statisticsController.RegisterMetric(metric);
        }

        public void UnregisterMetric(string id)
        {
            statisticsController.ResetMetric(id);
        }

        public object GetMetricValue(string id)
        {
            return statisticsController.GetMetricValue(id);
        }


        public T GetMetric<T>(string metricId) where T : class, ITrackedMetric
        {
            return statisticsController.GetMetric<T>(metricId);
        }

        public void RecordPlayerAction(string metricId, object delta)
        {
            statisticsController.RecordAction(metricId, delta);
        }


        public object GetPlayerMetric(string metricId)
        {
            return statisticsController.GetMetricValue(metricId);
        }

        public void ResetPlayerMetric(string metricId)
        {
            statisticsController.ResetMetric(metricId);
        }

        public void AddObserver(IMetricObserver observer)
        {
            statisticsController.RegisterObserver(observer);
        }

        public void RemoveObserver(IMetricObserver observer)
        {
            statisticsController.RemoveObserver(observer);
        }



        private void OnApplicationQuit()
        {
            statisticsController.ForceSave();
        }


    }


}
