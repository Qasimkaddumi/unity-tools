using Kaddumi.UnityTools.Save.Interfaces;
using UnityEngine;

namespace Kaddumi.UnityTools.Save.Providers
{
    [CreateAssetMenu(fileName = "EncryptedFileSaveProvider", menuName = "Kaddumi/Save/Providers/Encrypted File")]
    public class EncryptedFileSaveProviderSO : SaveProviderSO
    {
        [Tooltip("Sub-folder under Application.persistentDataPath. Leave empty to write in the root.")]
        [SerializeField] private string subFolder = "Saves";

        [Tooltip("File extension for each slot file (a leading dot is optional).")]
        [SerializeField] private string extension = ".sav";

        [Tooltip("Password used to derive the AES key. NOTE: this ships inside the build, so it " +
                 "only deters casual tampering. For real protection derive it from a device/server secret.")]
        [SerializeField] private string password = "change-me";

        public override ISaveProvider CreateProvider() =>
            new EncryptedFileSaveProvider(password, subFolder, extension);
    }
}
