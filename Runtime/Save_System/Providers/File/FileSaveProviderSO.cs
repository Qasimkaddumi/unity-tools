using Kaddumi.UnityTools.Save.Interfaces;
using UnityEngine;

namespace Kaddumi.UnityTools.Save.Providers
{
    [CreateAssetMenu(fileName = "FileSaveProvider", menuName = "Kaddumi/Save/Providers/File (JSON)")]
    public class FileSaveProviderSO : SaveProviderSO
    {
        [Tooltip("Sub-folder under Application.persistentDataPath. Leave empty to write in the root.")]
        [SerializeField] private string subFolder = "Saves";

        [Tooltip("File extension for each slot file (a leading dot is optional).")]
        [SerializeField] private string extension = ".save";

        public override ISaveProvider CreateProvider() => new FileSaveProvider(subFolder, extension);
    }
}
