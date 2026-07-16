using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Kaddumi.UnityTools.LoadingSystem.Core
{

    [CreateAssetMenu(fileName = "New Scene Definition", menuName = "Scene Management/Scene Definition")]
    public class SceneDefinition : ScriptableObject
    {
#if UNITY_EDITOR
        [SerializeField] private SceneAsset sceneAsset;
#endif

        [SerializeField] private string scenePath;

        public string ScenePath => scenePath;

        [SerializeField] protected string sceneID;
        public string SceneID => sceneID;

        public bool IsValid() => !string.IsNullOrEmpty(scenePath);

#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            if (sceneAsset != null)
            {
                if (string.IsNullOrEmpty(scenePath))
                {
                    scenePath = AssetDatabase.GetAssetPath(sceneAsset);

                    sceneID = GetSceneIDFromPath(scenePath);


                }
                else if (string.IsNullOrEmpty(sceneID))
                {


                    sceneID = GetSceneIDFromPath(scenePath);
                }

            }
            else
            {
                scenePath = string.Empty;
            }

            static string GetSceneIDFromPath(string path)
            {
                int lastSlash = path.LastIndexOf('/');
                string nameWithExtension = lastSlash >= 0 ? path[(lastSlash + 1)..] : path;
                int lastDot = nameWithExtension.LastIndexOf('.');
                return lastDot >= 0 ? nameWithExtension[..lastDot] : nameWithExtension;
            }


        }
#endif


    }
}