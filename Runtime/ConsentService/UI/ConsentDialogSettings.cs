using System;
using UnityEngine;

namespace Kaddumi.UnityTools.Consent.UI
{
    /// <summary>
    /// Text and links shown in the built-in <see cref="ConsentDialog"/>. Edit these on the
    /// Manual consent provider asset so you can localize / rebrand the prompt without code.
    /// </summary>
    [Serializable]
    public class ConsentDialogSettings
    {
        public string Title = "Your Privacy";

        [TextArea(3, 8)]
        public string Message =
            "We and our partners use cookies and similar technologies to personalize ads and " +
            "measure app performance. You can accept or decline. You can change this choice at " +
            "any time in Settings.";

        public string AcceptLabel = "Accept";
        public string DeclineLabel = "Decline";

        [Tooltip("Optional. When set, a link button opens this URL in the browser.")]
        public string PrivacyPolicyUrl = "";
        public string PrivacyPolicyLabel = "Privacy Policy";
    }
}
