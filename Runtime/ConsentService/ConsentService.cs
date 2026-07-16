using System;
using UnityEngine;
using Kaddumi.UnityTools.Services;





#if AdMob_SDK_INSTALLED
using GoogleMobileAds.Ump.Api;
#endif


namespace Kaddumi.UnityTools.Consent
{
    public enum ConsentStatus
    {
        Unknown = 0, // First time user, show popup
        Granted = 1, // User accepted
        Denied = 2   // User rejected
    }


    public class ConsentService : MonoBehaviour, IService
    {

        public static ConsentService Instance;


        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;


        }


        public ConsentStatus CurrentStatus { get; private set; }


        public bool IsConsentGranted => CurrentStatus == ConsentStatus.Granted;
        public bool IsConsentUnknown => CurrentStatus == ConsentStatus.Unknown;


        public event Action<bool> OnConsentChanged;


        public void Initialize(Action onComplete)
        {

#if AdMob_SDK_INSTALLED
            // 1. Set up the request parameters (optional: add test device IDs here)
            var request = new ConsentRequestParameters();

            // 2. Update consent information
            ConsentInformation.Update(request, (FormError error) =>
            {
                if (error != null)
                {
                    UpdateStatus(ConsentStatus.Unknown);
                    Debug.LogError("Consent Update Failed: " + error.Message);
                    onComplete?.Invoke(); // Ensure flow continues
                    return;
                }

                // 3. Load and show the form if required (GDPR/CCPA regions)
                ConsentForm.LoadAndShowConsentFormIfRequired((FormError showError) =>
                {
                    if (showError != null)
                    {
                        UpdateStatus(ConsentStatus.Unknown);
                        Debug.LogError("Form Show Failed: " + showError.Message);
                    }
                    else
                    {
                        UpdateStatus(ConsentStatus.Granted);
                    }


            Debug.Log($"{nameof(ConsentService)} Initialized");
                    onComplete?.Invoke();
                });

            });
#else
            Debug.Log($"{nameof(ConsentService)} Initialized");
            onComplete?.Invoke();

#endif

        }


        public void GrantConsent()
        {
            UpdateStatus(ConsentStatus.Granted);
        }

        public void DenyConsent()
        {
            UpdateStatus(ConsentStatus.Denied);
        }

        public void ResetConsent()
        {
            UpdateStatus(ConsentStatus.Unknown);
        }
        public void RequestRightToErasure()
        {
            CurrentStatus = ConsentStatus.Unknown;

            // Treat erasure as a revocation of consent
            OnConsentChanged?.Invoke(false);
        }
        private void UpdateStatus(ConsentStatus newStatus)
        {
            CurrentStatus = newStatus;


            OnConsentChanged?.Invoke(newStatus == ConsentStatus.Granted);
        }



    }
}