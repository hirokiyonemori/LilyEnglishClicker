using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Idle
{
    // Presentation only: never changes damage, turn timing, or saved data.
    public sealed class MainHitFeedback : MonoBehaviour
    {
        Image portrait;
        Color restingColor;
        RectTransform enemyVisual;
        Vector2 enemyOrigin;
        TextMeshProUGUI label;

        public void Show(EnemyController enemy, int damage)
        {
            Clear();
            portrait = GetComponent<Image>();
            if (portrait != null) restingColor = portrait.color;
            enemyVisual = enemy != null ? enemy.AttackVisual : null;
            if (enemyVisual != null) enemyOrigin = enemyVisual.anchoredPosition;
            var rect = transform as RectTransform;
            if (rect != null)
            {
                var go = new GameObject("IncomingDamage", typeof(RectTransform), typeof(TextMeshProUGUI));
                go.transform.SetParent(transform, false);
                label = go.GetComponent<TextMeshProUGUI>();
                var sample = GetComponentInParent<Canvas>()?.GetComponentInChildren<TMP_Text>();
                if (sample != null) label.font = sample.font;
                label.text = $"−{damage}";
                label.fontSize = 58;
                label.fontStyle = FontStyles.Bold;
                label.alignment = TextAlignmentOptions.Center;
                label.color = new Color(1f, .3f, .25f);
                label.outlineWidth = .2f;
                label.outlineColor = new Color32(35, 15, 25, 255);
                label.raycastTarget = false;
                label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(.5f, .8f);
                label.rectTransform.sizeDelta = new Vector2(260, 100);
            }
            StartCoroutine(Animate());
        }

        IEnumerator Animate()
        {
            for (float t = 0; t < .7f; t += Time.deltaTime)
            {
                // Quick step toward Lily and back. Her own tap animation remains independent.
                if (enemyVisual != null)
                    enemyVisual.anchoredPosition = enemyOrigin + Vector2.left * (48f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / .24f)));
                if (portrait != null)
                    portrait.color = t < .28f && (int)(t / .07f) % 2 == 0
                        ? new Color(1f, .35f, .35f, restingColor.a) : restingColor;
                if (label != null)
                {
                    label.rectTransform.anchoredPosition = Vector2.up * (t * 70f);
                    label.alpha = 1f - Mathf.InverseLerp(.4f, .7f, t);
                }
                yield return null;
            }
            Clear();
        }

        public void Clear()
        {
            StopAllCoroutines();
            if (enemyVisual != null) enemyVisual.anchoredPosition = enemyOrigin;
            enemyVisual = null;
            if (portrait != null) portrait.color = restingColor;
            portrait = null;
            if (label != null) Destroy(label.gameObject);
            label = null;
        }

        void OnDisable() => Clear();
    }
}
