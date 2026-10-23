using System;
using System.Collections.Generic;
using UnityEngine;

namespace Idle
{
    /// <summary>
    /// 敵スプライトごとの「実際に描かれている範囲」（透明な余白を除いた範囲、0〜1 の正規化座標・下原点）。
    /// 画像ごとに余白の量が大きく違うため、この範囲の下端を地面に合わせて接地させる。
    /// EnemyEncounterBuilder（Editor）が PNG を解析して生成する。
    /// </summary>
    [CreateAssetMenu(fileName = "EnemySpriteBounds", menuName = "LilyClicker/Enemy Sprite Bounds")]
    public sealed class EnemySpriteBounds : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public Sprite sprite;
            public Rect visible;
        }

        public List<Entry> entries = new List<Entry>();

        public bool TryGet(Sprite sprite, out Rect visible)
        {
            foreach (Entry e in entries)
            {
                if (e.sprite == sprite && e.visible.width > 0f && e.visible.height > 0f)
                {
                    visible = e.visible;
                    return true;
                }
            }
            visible = new Rect(0f, 0f, 1f, 1f);
            return false;
        }
    }
}
