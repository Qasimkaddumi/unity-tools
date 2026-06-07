using System.Collections;
using UnityEngine;

namespace LoadingSystem.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public class ScreenFader : MonoBehaviour
    {
        [SerializeField] private float fadeDuration = 0.5f;
        [SerializeField] private AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        private CanvasGroup canvasGroup;

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
        }

        public IEnumerator FadeOut()
        {
            canvasGroup.blocksRaycasts = true;

            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = fadeCurve.Evaluate(elapsed / fadeDuration);
                yield return null;
            }

            canvasGroup.alpha = 1f;
        }

        public IEnumerator FadeIn()
        {
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = fadeCurve.Evaluate(1f - elapsed / fadeDuration);
                yield return null;
            }

            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
        }
    }
}