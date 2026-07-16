using System.Collections.Generic;
using UnityEngine;

namespace Kaddumi.UnityTools.LoadingSystem.Core
{
    [CreateAssetMenu(fileName = "Scene Catalog", menuName = "Scene Management/Scene Catalog")]
    public class SceneCatalog : ScriptableObject
    {

        [Header("General Section")]
        [SerializeField] private List<SceneDefinition> mainMenuScenes;
        [SerializeField] private SceneDefinition loadingScene;

        [Space(10)]
        [Header("Gameplay Section")]
        [SerializeField] private List<SceneDefinition> gameplayScenes;


        [Space(10)]
        [Header("Others (for Later)")]
        [Header("Level Scenes")]
        [SerializeField] private List<LevelDefinition> levels = new List<LevelDefinition>();

        public IReadOnlyList<LevelDefinition> Levels => levels;
        public IReadOnlyList<SceneDefinition> MainMenuScenes => mainMenuScenes;
        public IReadOnlyList<SceneDefinition> GameplayScenes => gameplayScenes;
        public SceneDefinition LoadingScene => loadingScene;



    }
}