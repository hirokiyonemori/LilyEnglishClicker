using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// クエスト画面の全画面背景（ForestBackground）を、進行中のイベントに合わせて差し替える。
///
/// 進行の制御には関与せず、QuestManager.CurrentState を読んで画像を変えるだけ。
/// 画像が Resources/Backgrounds に無い場合や、対応する状態が無い場合は、元の背景（森）のまま。
/// QuestOverlayController が questRoot に自動で追加するため、Prefab・シーンの変更は不要。
/// </summary>
public sealed class QuestBackgroundSwitcher : MonoBehaviour
{
    private const string BackdropName = "ForestBackground";

    // 明るい森向けに作られた紺色の文字（見出し・敵名・HPなど）。暗い背景では読みにくいので色を切り替える
    private static readonly Color InkColor   = new Color32(0x17, 0x2D, 0x43, 0xFF);
    private static readonly Color CreamColor = new Color32(0xF5, 0xED, 0xD8, 0xFF);

    private Image backdrop;
    private Sprite defaultSprite;
    private readonly List<TextMeshProUGUI> inkLabels = new List<TextMeshProUGUI>();
    private QuestState shownState = QuestState.Idle;
    private bool shownPathWasLeft = true;
    private bool initialized;

    private void OnEnable()
    {
        // クエストを開くたびに、前回の最後の背景（ボス戦など）を引き継がず既定へ戻す
        initialized = false;
        shownState = QuestState.Idle;
    }

    private void OnDisable()
    {
        Apply(null);
    }

    private void LateUpdate()
    {
        if (!Resolve()) return;

        QuestManager manager = QuestManager.Instance;
        if (manager == null) return;

        QuestState state = manager.CurrentState;
        bool left = manager.LastPathWasLeft;
        if (initialized && state == shownState && left == shownPathWasLeft) return;

        initialized = true;
        shownState = state;
        shownPathWasLeft = left;
        Apply(SpriteFor(state, left));
    }

    private bool Resolve()
    {
        if (backdrop != null) return true;

        Transform t = transform.Find(BackdropName);
        if (t == null) return false;

        backdrop = t.GetComponent<Image>();
        if (backdrop == null) return false;

        defaultSprite = backdrop.sprite;

        // 文字色は Prefab の初期値（紺）で見分ける。カード内のクリーム・金の文字は対象外
        foreach (TextMeshProUGUI label in GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (label != null && IsInk(label.color)) inkLabels.Add(label);
        }
        return true;
    }

    private static bool IsInk(Color c)
    {
        return Mathf.Abs(c.r - InkColor.r) < 0.02f && Mathf.Abs(c.g - InkColor.g) < 0.02f && Mathf.Abs(c.b - InkColor.b) < 0.02f;
    }

    private static Sprite SpriteFor(QuestState state, bool pathWasLeft)
    {
        switch (state)
        {
            case QuestState.VillagerEvent: return QuestBackgrounds.Get(QuestBackgrounds.Village);
            case QuestState.ChestEvent:    return QuestBackgrounds.Get(QuestBackgrounds.Ruins);
            case QuestState.EnemyBattle:   return QuestBackgrounds.Get(pathWasLeft ? QuestBackgrounds.PathForest : QuestBackgrounds.PathRoad);
            case QuestState.BossBattle:    return QuestBackgrounds.Get(QuestBackgrounds.Boss);
            default:                       return null;
        }
    }

    /// <summary>sprite が null（画像なし／対応状態なし）の時は既定の森に戻す</summary>
    private void Apply(Sprite sprite)
    {
        if (backdrop == null) return;
        backdrop.sprite = sprite != null ? sprite : defaultSprite;

        bool dark = sprite != null && QuestBackgrounds.IsDark(sprite.name);
        foreach (TextMeshProUGUI label in inkLabels)
        {
            if (label != null) label.color = dark ? CreamColor : InkColor;
        }
    }
}
