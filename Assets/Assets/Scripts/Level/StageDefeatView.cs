using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Idle
{
    public sealed class StageDefeatView : MonoBehaviour
    {
        [SerializeField] private TMP_Text stageText;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button baseButton;
        private void Awake()
        {
            retryButton.onClick.AddListener(() => StageDirector.Instance?.RetryStage());
            baseButton.onClick.AddListener(() => StageDirector.Instance?.ReturnToBase());
        }
        public void Show(int stage)
        {
            if (stageText != null) stageText.text = $"STAGE {stage}\n獲得済みのGOLD・EXPは失われません。\nもう一度挑戦するとHPが全回復します。";
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }
        public void Hide() => gameObject.SetActive(false);
    }
}
