using System.Collections;
using UnityEngine;

namespace LilyEnglish.Growth.UI
{
    /// <summary>レベルアップ時のポンッと弾む演出（任意。SE/パーティクルもここで鳴らす）。</summary>
    public class LevelUpFeedback : MonoBehaviour
    {
        [SerializeField] private RectTransform target;
        [SerializeField] private float punchScale = 1.12f;
        [SerializeField] private float duration = 0.18f;
        [SerializeField] private AudioSource se;
        [SerializeField] private ParticleSystem sparkle;

        private Coroutine running;

        public void Play()
        {
            if (se) se.Play();
            if (sparkle) sparkle.Play();
            if (target == null) return;
            if (running != null) StopCoroutine(running);
            running = StartCoroutine(Punch());
        }

        private IEnumerator Punch()
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Sin(Mathf.Clamp01(t / duration) * Mathf.PI);
                target.localScale = Vector3.one * Mathf.Lerp(1f, punchScale, k);
                yield return null;
            }
            target.localScale = Vector3.one;
            running = null;
        }
    }
}
