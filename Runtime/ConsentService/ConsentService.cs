using System;
using UnityEngine;
using Kaddumi.UnityTools.Services;
using Kaddumi.UnityTools.Consent.Providers;

namespace Kaddumi.UnityTools.Consent
{
    /// <summary>
    /// Central privacy-consent orchestrator. It owns the resolved <see cref="ConsentStatus"/>,
    /// raises <see cref="OnConsentChanged"/> when it changes, and delegates the actual gathering
    /// of consent to a pluggable <see cref="IConsentProvider"/> chosen in the inspector:
    /// <list type="bullet">
    ///   <item><b>Google UMP</b> — server-backed GDPR/CCPA form (needs the Google Mobile Ads SDK).</item>
    ///   <item><b>Manual</b> — a built-in dialog for projects that don't use Google services.</item>
    /// </list>
    /// Ads and analytics subscribe to <see cref="OnConsentChanged"/> and read
    /// <see cref="IsConsentGranted"/>, so nothing personalized runs until consent is granted.
    /// </summary>
    public class ConsentService : MonoBehaviour, IService
    {
        public static ConsentService Instance;

        [Header("Configuration")]
        [Tooltip("Assign a consent provider ScriptableObject (Google UMP or Manual). " +
                 "This decides how the user is asked for consent.")]
        [SerializeField] private ConsentProviderSO consentProvider;

        private IConsentProvider _provider;

        public ConsentStatus CurrentStatus { get; private set; }

        public bool IsConsentGranted => CurrentStatus == ConsentStatus.Granted;
        public bool IsConsentUnknown => CurrentStatus == ConsentStatus.Unknown;

        /// <summary>Raised whenever consent is resolved or changed. The bool is <c>IsConsentGranted</c>.</summary>
        public event Action<bool> OnConsentChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void Initialize(Action onComplete)
        {
            if (consentProvider == null)
            {
                Debug.LogError("[ConsentService] No consent provider assigned. Assign a ConsentProviderSO " +
                               "(Google UMP or Manual) in the inspector. Consent stays Unknown.");
                onComplete?.Invoke();
                return;
            }

            _provider = consentProvider.CreateProvider();

            // The provider resolves synchronously when a decision is already stored or not required
            // in the user's region, and asynchronously when it has to show a form/dialog. In the
            // async case we return control to the ServiceLocator immediately (below) so app startup
            // isn't blocked on user input; ads/analytics stay gated until OnConsentChanged fires.
            _provider.RequestConsent(status =>
            {
                UpdateStatus(status);
                Debug.Log($"[ConsentService] Consent resolved: {status}");
            });

            Debug.Log($"{nameof(ConsentService)} Initialized (status: {CurrentStatus})");
            onComplete?.Invoke();
        }

        /// <summary>
        /// Re-opens the consent UI so the user can review or change their previous choice.
        /// Hook this to a "Privacy settings" / "Manage consent" button.
        /// </summary>
        public void ShowPrivacyOptions(Action onComplete = null)
        {
            if (_provider == null)
            {
                Debug.LogWarning("[ConsentService] ShowPrivacyOptions called before initialization.");
                onComplete?.Invoke();
                return;
            }

            _provider.ShowPrivacyOptions(status =>
            {
                UpdateStatus(status);
                onComplete?.Invoke();
            });
        }

        public void GrantConsent() => UpdateStatus(ConsentStatus.Granted);

        public void DenyConsent() => UpdateStatus(ConsentStatus.Denied);

        /// <summary>Clears the stored decision (and provider state) and returns to Unknown.</summary>
        public void ResetConsent()
        {
            _provider?.Reset();
            UpdateStatus(ConsentStatus.Unknown);
        }

        /// <summary>
        /// Handles a GDPR "right to erasure" request: revokes consent and clears stored state so
        /// downstream services stop processing personal data.
        /// </summary>
        public void RequestRightToErasure()
        {
            _provider?.Reset();
            UpdateStatus(ConsentStatus.Unknown);
        }

        private void UpdateStatus(ConsentStatus newStatus)
        {
            CurrentStatus = newStatus;
            OnConsentChanged?.Invoke(newStatus == ConsentStatus.Granted);
        }
    }
}
