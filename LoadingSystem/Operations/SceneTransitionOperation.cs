using LoadingSystem.Core;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LoadingSystem.Operations
{
    public class SceneTransitionOperation : ILoadingOperation
    {
        private readonly List<string> targetScenes;
        private readonly string emptySceneName;

        public float Progress { get; private set; }
        public bool IsDone { get; private set; }

        public SceneTransitionOperation(List<string> targetScenes, string emptySceneName = "Empty")
        {
            this.targetScenes = targetScenes;
            this.emptySceneName = emptySceneName;
            Progress = 0f;
            IsDone = false;
        }

        public IEnumerator Execute()
        {
            Progress = 0f;

            // 1. Load the Empty Scene if not present to clear memory safely
            if (!IsSceneLoaded(emptySceneName))
            {
                List<AsyncOperation> emptySceneOps = LoadScenesByName(new List<string> { emptySceneName });
                yield return ProcessOperations(emptySceneOps, 0f, 0.1f);

                Scene activeScene = SceneManager.GetSceneByName(emptySceneName);
                if (activeScene.isLoaded)
                {
                    SceneManager.SetActiveScene(activeScene);
                }
            }

            // 2. Unload existing scenes
            List<string> scenesToUnload = GetScenesToUnload();
            if (scenesToUnload.Count > 0)
            {
                List<AsyncOperation> unloadOps = UnloadScenesByName(scenesToUnload);
                yield return ProcessOperations(unloadOps, 0.1f, 0.5f);
            }
            else
            {
                Progress = 0.5f;
            }

            // 3. Load target scenes
            List<string> scenesToLoad = GetScenesToLoad();
            if (scenesToLoad.Count > 0)
            {
                List<AsyncOperation> loadOps = LoadScenesByName(scenesToLoad);
                yield return ProcessOperations(loadOps, 0.5f, 1f);


                Scene activeScene = SceneManager.GetSceneByName(scenesToLoad.First());
                if (activeScene.isLoaded)
                {
                    SceneManager.SetActiveScene(activeScene);
                }


            }
            else
            {
                Progress = 1f;
            }

            IsDone = true;
        }

        private bool IsSceneLoaded(string sceneName)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SceneManager.GetSceneAt(i).name == sceneName)
                {
                    return true;
                }
            }
            return false;
        }

        private List<string> GetScenesToUnload()
        {
            List<string> scenesToUnload = new List<string>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.name.Equals(emptySceneName))
                {
                    continue;
                }
                scenesToUnload.Add(scene.name);
            }
            return scenesToUnload;
        }

        private List<string> GetScenesToLoad()
        {
            List<string> scenesToLoad = new List<string>();
            foreach (var sceneName in targetScenes)
            {
                scenesToLoad.Add(sceneName);
            }
            return scenesToLoad;
        }

        private List<AsyncOperation> UnloadScenesByName(List<string> sceneNames)
        {
            List<AsyncOperation> operations = new List<AsyncOperation>();
            foreach (var name in sceneNames)
            {
                AsyncOperation operation = SceneManager.UnloadSceneAsync(name);
                if (operation != null)
                {
                    operations.Add(operation);
                }
            }
            return operations;
        }

        private List<AsyncOperation> LoadScenesByName(List<string> sceneNames)
        {
            List<AsyncOperation> operations = new List<AsyncOperation>();
            foreach (var name in sceneNames)
            {
                AsyncOperation operation = SceneManager.LoadSceneAsync(name, LoadSceneMode.Additive);
                if (operation != null)
                {
                    operations.Add(operation);
                }
            }
            return operations;
        }

        private IEnumerator ProcessOperations(List<AsyncOperation> operations, float startProgress, float endProgress)
        {
            int initialCount = operations.Count;
            if (initialCount == 0)
            {
                Progress = endProgress;
                yield break;
            }

            while (operations.Count > 0)
            {
                float totalProgress = 0f;

                foreach (var op in operations)
                {
                    totalProgress += op.progress;
                }

                totalProgress += initialCount - operations.Count;
                float normalizedProgress = totalProgress / initialCount;
                Progress = Mathf.Lerp(startProgress, endProgress, normalizedProgress);

                for (int i = operations.Count - 1; i >= 0; i--)
                {
                    if (operations[i].isDone)
                    {
                        operations.RemoveAt(i);
                    }
                }

                yield return null;
            }

            Progress = endProgress;
        }
    }
}