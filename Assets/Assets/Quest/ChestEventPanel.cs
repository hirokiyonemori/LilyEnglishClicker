using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 宝箱イベントパネル  Normal 60% / ハズレ 25% / Rare 15%
/// </summary>
public class ChestEventPanel : MonoBehaviour
{
    [Header("=== UI参照 ===")]
    [SerializeField] private GameObject      root;
    [SerializeField] private Image           chestImage;
    [SerializeField] private Button          openButton;
    [SerializeField] private TextMeshProUGUI openButtonText;

    [Header("宝箱スプライト")]
    [SerializeField] private Sprite chestClosed;
    [SerializeField] private Sprite chestOpenNormal;
    [SerializeField] private Sprite chestOpenMiss;
    [SerializeField] private Sprite chestOpenRare;

    [Header("結果")]
    [SerializeField] private GameObject      resultBox;
    [SerializeField] private TextMeshProUGUI rarityText;
    [SerializeField] private TextMeshProUGUI itemNameText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI resultText;
    [SerializeField] private Button          okButton;

    [Header("レアエフェクト（任意）")]
    [SerializeField] private GameObject sparkleEffect;

    [Header("出現率")]
    [SerializeField] private int normalWeight = 60;
    [SerializeField] private int missWeight   = 25;
    [SerializeField] private int rareWeight   = 15;

    private QuestDataSO               questData;
    private Action<QuestEventResult>  onComplete;
    private bool isShowing;
    private bool opened;
    private bool chestPositionCaptured;
    private Vector2 chestPosition;

    // ─── 公開 API ────────────────────────────────────────────────
    public void Show(QuestDataSO data, Action<QuestEventResult> callback)
    {
        StopAllCoroutines();
        LoadDefaultSprites();
        CaptureChestPosition();
        RestoreChestPosition();
        isShowing = true;
        opened = false;
        questData  = data;
        onComplete = callback;

        if (chestImage != null && chestClosed != null) chestImage.sprite = chestClosed;
        if (openButtonText != null) openButtonText.text = "宝箱を開ける";
        if (itemNameText != null) itemNameText.text = "道ばたの宝箱";
        if (descriptionText != null) descriptionText.text = "何が入っているだろう？";
        if (rarityText != null) rarityText.text = "TREASURE";

        if (resultBox != null) resultBox.SetActive(false);
        if (sparkleEffect != null) sparkleEffect.SetActive(false);

        if (openButton != null)
        {
            openButton.onClick.RemoveAllListeners();
            openButton.onClick.AddListener(OnOpenClicked);
            openButton.gameObject.SetActive(true);
        }

        if (root != null) root.SetActive(true);
    }

    public void Hide()
    {
        StopAllCoroutines();
        isShowing = false;
        RestoreChestPosition();
        if (root != null) root.SetActive(false);
    }

    // ─── 開封 ────────────────────────────────────────────────────
    private void OnOpenClicked()
    {
        if (!isShowing || opened) return;
        opened = true;
        if (openButton != null) openButton.gameObject.SetActive(false);
        StartCoroutine(OpenChest());
    }

    private IEnumerator OpenChest()
    {
        yield return StartCoroutine(ShakeChest());

        ChestRarity      rarity = RollRarity();
        QuestEventResult result = BuildResult(rarity);

        if (chestImage != null)
        {
            Sprite sp = rarity == ChestRarity.Normal ? chestOpenNormal
                      : rarity == ChestRarity.Miss   ? chestOpenMiss
                                                     : chestOpenRare;
            if (sp != null) chestImage.sprite = sp;
        }

        if (rarity == ChestRarity.Rare && sparkleEffect != null) sparkleEffect.SetActive(true);

        // 蓋が開く表示と合わせて一度だけ再生する。結果や報酬の確定には関与しない。
        SfxDirector.Play(SfxDirector.ChestOpen);

        yield return new WaitForSeconds(0.3f);
        ShowResult(rarity, result);
    }

    private ChestRarity RollRarity()
    {
        int total = normalWeight + missWeight + rareWeight;
        int roll  = UnityEngine.Random.Range(0, total);
        if (roll < normalWeight)                    return ChestRarity.Normal;
        if (roll < normalWeight + missWeight)       return ChestRarity.Miss;
        return ChestRarity.Rare;
    }

    private QuestEventResult BuildResult(ChestRarity rarity)
    {
        // questData は Unity のオブジェクトなので ?. / ?? は使わない
        // （未割り当て参照は「nullでない」と判定されてしまうため）
        bool hasData = questData != null;
        int nMin    = hasData ? questData.normalGoldMin : 50;
        int nMax    = hasData ? questData.normalGoldMax : 150;
        int rMin    = hasData ? questData.rareGoldMin   : 300;
        int rMax    = hasData ? questData.rareGoldMax   : 600;
        string item = hasData && !string.IsNullOrEmpty(questData.rareItemName)
                      ? questData.rareItemName
                      : "まほうのかぎ";

        var r = new QuestEventResult { success = true };
        switch (rarity)
        {
            case ChestRarity.Normal:
                r.goldGained = UnityEngine.Random.Range(nMin, nMax + 1);
                r.message    = $"金貨 {r.goldGained}G を手に入れた！";
                break;
            case ChestRarity.Miss:
                bool isTrap = UnityEngine.Random.value < 0.4f;
                if (isTrap) { r.hpChange = -10; r.message = "罠だった！\nHPが10減ってしまった！"; r.success = false; }
                else        { r.message = "箱は空だった…\n何も入っていない。"; r.success = false; }
                break;
            case ChestRarity.Rare:
                r.goldGained = UnityEngine.Random.Range(rMin, rMax + 1);
                r.itemGained = item;
                r.message    = $"★ レアアイテム「{item}」を発見！\n金貨 {r.goldGained}G もゲット！";
                break;
        }
        return r;
    }

    private void ShowResult(ChestRarity rarity, QuestEventResult result)
    {
        if (rarityText != null)
            rarityText.text = rarity == ChestRarity.Normal ? "NORMAL"
                            : rarity == ChestRarity.Miss   ? "ハズレ…"
                                                           : "RARE!";

        if (itemNameText != null)
            itemNameText.text = !string.IsNullOrEmpty(result.itemGained) ? result.itemGained
                              : result.goldGained > 0 ? "金貨"
                              : result.hpChange < 0 ? "仕掛けられた罠" : "空の宝箱";
        if (descriptionText != null)
            descriptionText.text = rarity == ChestRarity.Rare ? "レアアイテムを見つけた！"
                                 : rarity == ChestRarity.Normal ? "きらめく金貨が入っていた。"
                                 : result.hpChange < 0 ? "気をつけて、罠が隠されていた！"
                                                       : "残念。中には何もなかった。";

        if (resultText != null) resultText.text = result.message;

        if (okButton != null)
        {
            okButton.onClick.RemoveAllListeners();
            okButton.onClick.AddListener(() =>
            {
                if (resultBox != null) resultBox.SetActive(false);
                if (sparkleEffect != null) sparkleEffect.SetActive(false);
                onComplete?.Invoke(result);
            });
        }
        else
        {
            // OKボタンが無いとクエストが進行不能になるため、その場で完了扱いにする
            Debug.LogWarning("ChestEventPanel: okButton が未設定のため、結果表示をスキップして進行します。");
            onComplete?.Invoke(result);
            return;
        }

        if (resultBox != null) resultBox.SetActive(true);
    }

    private IEnumerator ShakeChest()
    {
        if (chestImage == null) yield break;
        RectTransform rt = chestImage.GetComponent<RectTransform>();
        if (rt == null) yield break;

        Vector2 origin = rt.anchoredPosition;
        float dur = 0.4f, elapsed = 0f;

        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float x = Mathf.Sin(elapsed * 40f) * 12f * (1f - elapsed / dur);
            rt.anchoredPosition = origin + new Vector2(x, 0f);
            yield return null;
        }
        rt.anchoredPosition = origin;
    }

    private void Awake()
    {
        LoadDefaultSprites();
        if (!isShowing && root != null) root.SetActive(false);
    }

    /// <summary>
    /// 宝箱の画像を Assets/Resources/Chest/ から読み込む。Inspector（Prefab）で割り当て済みならそちらを優先する。
    /// Prefab は QuestProductionUIBuilder で作り直されるため、画像をコード側で解決して設定が消えないようにしている。
    /// ファイル名: closed / open_normal / open_miss / open_rare
    /// </summary>
    private void LoadDefaultSprites()
    {
        // Sprite は UnityEngine.Object なので ?? は使わず、明示的に null 判定する
        if (chestClosed == null)     chestClosed     = Resources.Load<Sprite>("Chest/closed");
        if (chestOpenNormal == null) chestOpenNormal = Resources.Load<Sprite>("Chest/open_normal");
        if (chestOpenMiss == null)   chestOpenMiss   = Resources.Load<Sprite>("Chest/open_miss");
        if (chestOpenRare == null)   chestOpenRare   = Resources.Load<Sprite>("Chest/open_rare");
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        isShowing = false;
        RestoreChestPosition();
    }

    private void CaptureChestPosition()
    {
        if (chestPositionCaptured || chestImage == null) return;
        chestPosition = chestImage.rectTransform.anchoredPosition;
        chestPositionCaptured = true;
    }

    private void RestoreChestPosition()
    {
        if (chestPositionCaptured && chestImage != null)
            chestImage.rectTransform.anchoredPosition = chestPosition;
    }
}
