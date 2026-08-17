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

    // ─── 公開 API ────────────────────────────────────────────────
    public void Show(QuestDataSO data, Action<QuestEventResult> callback)
    {
        questData  = data;
        onComplete = callback;

        if (chestImage != null && chestClosed != null) chestImage.sprite = chestClosed;
        if (openButtonText != null) openButtonText.text = "宝箱を開ける！";

        resultBox?.SetActive(false);
        sparkleEffect?.SetActive(false);

        openButton?.onClick.RemoveAllListeners();
        openButton?.onClick.AddListener(OnOpenClicked);
        openButton?.gameObject.SetActive(true);

        root?.SetActive(true);
    }

    public void Hide()
    {
        StopAllCoroutines();
        root?.SetActive(false);
    }

    // ─── 開封 ────────────────────────────────────────────────────
    private void OnOpenClicked()
    {
        openButton?.gameObject.SetActive(false);
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

        if (rarity == ChestRarity.Rare) sparkleEffect?.SetActive(true);

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
        int nMin    = questData?.normalGoldMin ?? 50;
        int nMax    = questData?.normalGoldMax ?? 150;
        int rMin    = questData?.rareGoldMin   ?? 300;
        int rMax    = questData?.rareGoldMax   ?? 600;
        string item = questData?.rareItemName  ?? "まほうのかぎ";

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
            rarityText.text = rarity == ChestRarity.Normal ? "　チェスト"
                            : rarity == ChestRarity.Miss   ? "　ハズレ…"
                                                           : "★ レアチェスト！";

        if (resultText != null) resultText.text = result.message;

        if (okButton != null)
        {
            okButton.onClick.RemoveAllListeners();
            okButton.onClick.AddListener(() =>
            {
                resultBox?.SetActive(false);
                sparkleEffect?.SetActive(false);
                onComplete?.Invoke(result);
            });
        }
        resultBox?.SetActive(true);
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

    private void Awake() => root?.SetActive(false);
}
