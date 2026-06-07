using LoadingSystem.Core;
using LoadingSystem.Operations;
using LoadingSystem.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LoadingSystem
{
    public class LoadingManager : MonoBehaviour
    {
        public static LoadingManager Instance { get; private set; }

        public static event Action OnLoadBegin;
        public static event Action<float> OnLoadProgress;
        public static event Action OnLoadComplete;

        [SerializeField] private SceneCatalog sceneCatalog;
        [SerializeField] private LoadingScreen loadingScreen;
        [SerializeField] private ScreenFader screenFader;
        [SerializeField] private Camera loadingCamera;

        private string EmptySceneName => sceneCatalog.LoadingScene.SceneID;
        private bool isLoading;

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



        public void LoadMainMenu(List<ILoadingOperation> additionalJobs = null)
        {
            if (isLoading) return;

            List<string> scenesToLoad = new List<string>();
            foreach (var sceneData in sceneCatalog.MainMenuScenes)
            {
                if (sceneData != null && !string.IsNullOrEmpty(sceneData.SceneID))
                {
                    scenesToLoad.Add(sceneData.SceneID);
                }
            }

            ILoadingOperation transitionOp = new SceneTransitionOperation(scenesToLoad, EmptySceneName);
            StartCoroutine(PerformLoadingRoutine(transitionOp, additionalJobs));
        }

        public void LoadGamePlay(List<ILoadingOperation> additionalJobs = null)
        {
            if (isLoading) return;

            List<string> scenesToLoad = new List<string>();
            foreach (var sceneData in sceneCatalog.GameplayScenes)
            {
                if (sceneData != null && !string.IsNullOrEmpty(sceneData.SceneID))
                {
                    scenesToLoad.Add(sceneData.SceneID);
                }
            }

            ILoadingOperation transitionOp = new SceneTransitionOperation(scenesToLoad, EmptySceneName);

            StartCoroutine(PerformLoadingRoutine(transitionOp, additionalJobs));
        }

        private IEnumerator PerformLoadingRoutine(ILoadingOperation sceneTransitionOperation, List<ILoadingOperation> additionalJobs)
        {
            isLoading = true;
            loadingCamera.gameObject.SetActive(true);
            loadingScreen.gameObject.SetActive(true);

            OnLoadBegin?.Invoke();

            if (screenFader != null)
            {
                yield return screenFader.FadeOut();
            }

            if (loadingScreen != null)
            {
                loadingScreen.Show();
            }

            List<ILoadingOperation> operations = new List<ILoadingOperation>();

            if (additionalJobs != null)
            {
                operations.AddRange(additionalJobs);
            }

            // Append the dynamically resolved scene loading strategy
            operations.Add(sceneTransitionOperation);

            int totalOperations = operations.Count;
            float currentOperationIndex = 0;

            foreach (var operation in operations)
            {
                StartCoroutine(operation.Execute());

                while (!operation.IsDone)
                {
                    float baseProgress = currentOperationIndex / totalOperations;
                    float operationProgress = operation.Progress / totalOperations;
                    float totalProgress = baseProgress + operationProgress;

                    if (loadingScreen != null)
                    {
                        loadingScreen.UpdateProgress(totalProgress);
                    }

                    OnLoadProgress?.Invoke(totalProgress);
                    yield return null;
                }

                currentOperationIndex++;
            }

            if (loadingScreen != null)
            {
                loadingScreen.UpdateProgress(1f);
            }

            OnLoadProgress?.Invoke(1f);

            if (loadingScreen != null)
            {
                loadingScreen.Hide();
            }

            if (screenFader != null)
            {
                yield return screenFader.FadeIn();
            }

            isLoading = false;

            loadingScreen.gameObject.SetActive(false);
            loadingCamera.gameObject.SetActive(false);
            OnLoadComplete?.Invoke();
        }


    }
}