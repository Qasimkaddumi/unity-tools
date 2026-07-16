using Kaddumi.UnityTools.Statistics.Core;
using System.IO;

namespace Kaddumi.UnityTools.Statistics.Infrastructure
{

    public class JsonMetricStorage : IMetricStorage
    {
        private string filePath;

        public JsonMetricStorage(string injectedFilePath)
        {
            filePath = injectedFilePath;
        }

        public void SaveSnapshot(string dataPayload)
        {
            // Ensure the directory exists before writing
            string directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(filePath, dataPayload);
        }

        public async void SaveSnapshotAsync(string dataPayload)
        {
            // Ensure the directory exists before writing
            string directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            await File.WriteAllTextAsync(filePath, dataPayload);
        }



        public string LoadSnapshot()
        {
            if (File.Exists(filePath))
            {
                return File.ReadAllText(filePath);
            }
            return string.Empty;
        }

        public async System.Threading.Tasks.Task<string> LoadSnapshotAsync()
        {
            if (File.Exists(filePath))
            {
                return await File.ReadAllTextAsync(filePath);
            }
            return string.Empty;
        }


        public void ClearSnapshot()
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }
}