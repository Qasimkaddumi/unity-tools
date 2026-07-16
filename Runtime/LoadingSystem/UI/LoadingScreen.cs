using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Kaddumi.UnityTools.LoadingSystem.UI
{
    public class LoadingScreen : MonoBehaviour
    {
        [SerializeField] private GameObject loadingPanel;
        [SerializeField] private Slider progressBar;
        [SerializeField] private TextMeshProUGUI progressText;

        public void Show()
        {
            if (loadingPanel != null)
            {
                loadingPanel.SetActive(true);
            }
            UpdateProgress(0f);
        }

        public void Hide()
        {
            if (loadingPanel != null)
            {
                loadingPanel.SetActive(false);
            }
            UpdateProgress(0f);
        }

        public void UpdateProgress(float progress)
        {
            progress = Mathf.Clamp01(progress);

            if (progressBar != null)
            {
                progressBar.value = progress;
            }

            if (progressText != null)
            {
                progressText.text = $"{Mathf.RoundToInt(progress * 100)}%";
            }
        }
    }
}