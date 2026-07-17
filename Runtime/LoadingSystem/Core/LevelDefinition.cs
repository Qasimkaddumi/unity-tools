using UnityEngine;

namespace Kaddumi.UnityTools.LoadingSystem.Core
{
    [CreateAssetMenu(fileName = "New Level Definition", menuName = "Kaddumi/Scene Management/Level Definition")]
    public class LevelDefinition : SceneDefinition
    {
        [SerializeField] private int levelID;
        [SerializeField] private LevelDefinition nextLevel;

        public int LevelID => levelID;
        public LevelDefinition NextLevel => nextLevel;

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();

            if (IsValid() && levelID == 0)
            {
                string sceneName = sceneID;
                if (sceneName.StartsWith("Level_") && int.TryParse(sceneName[6..], out int id))
                    levelID = id;
            }
        }
#endif
    }
}