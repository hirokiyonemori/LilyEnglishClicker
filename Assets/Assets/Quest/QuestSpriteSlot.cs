using UnityEngine;
using UnityEngine.UI;

/// <summary>InspectorでImage.spriteを差し替えるだけで、代替線画と自動で切り替わる。</summary>
[ExecuteAlways]
public sealed class QuestSpriteSlot : MonoBehaviour
{
    public Image image;
    public GameObject fallback;
    private void OnEnable() { Refresh(); }
    private void LateUpdate() { Refresh(); }
    public void Refresh()
    {
        bool hasSprite = image != null && image.sprite != null;
        if (image != null && image.enabled != hasSprite) image.enabled = hasSprite;
        if (fallback != null && fallback.activeSelf == hasSprite) fallback.SetActive(!hasSprite);
    }
}
