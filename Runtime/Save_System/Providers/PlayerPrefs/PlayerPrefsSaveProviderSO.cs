using Kaddumi.UnityTools.Save.Interfaces;
using UnityEngine;

namespace Kaddumi.UnityTools.Save.Providers
{
    [CreateAssetMenu(fileName = "PlayerPrefsSaveProvider", menuName = "Kaddumi/Save/Providers/PlayerPrefs")]
    public class PlayerPrefsSaveProviderSO : SaveProviderSO
    {
        [Tooltip("Prefix applied to every PlayerPrefs key so saves don't collide with other data.")]
        [SerializeField] private string keyPrefix = "Kaddumi.Save.";

        public override ISaveProvider CreateProvider() => new PlayerPrefsSaveProvider(keyPrefix);
    }
}
