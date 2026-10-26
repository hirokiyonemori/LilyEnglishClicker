using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>専用シーンだけで動く3連戦比較。セーブ・解放・報酬処理を呼ばない。</summary>
public sealed class WordMagicPlaytest : MonoBehaviour
{
    public QuestUIView questPrefab;
    [Min(1)] public int referenceAttack = 2;
    [Tooltip("全問を一発正解した場合の問題数。実際のゲームの敵HPには適用しない。")]
    public int[] targetQuestions = { 2, 3, 4 };
    [Min(0f)] public float spellTravelSeconds = .24f;
    [Min(0f)] public float turnPauseSeconds = .4f;
    [Min(0f)] public float resultPauseSeconds = .4f;

    private QuestUIView view;
    private BattlePanel battle;
    private QuestBattlePresentation quiz;
    private GameObject reportRoot;
    private TextMeshProUGUI title, report, nextLabel, tempoLabel;
    private Button tempoButton;
    private int round, correct, mistakes, critical, impacts, previousHp;
    private bool retry, running, fast;
    private float startedAt, impactSeconds;
    private readonly StringBuilder results = new StringBuilder();
    private TMP_FontAsset font;

    private void Start()
    {
        if (questPrefab == null) { Debug.LogError("WordMagicPlaytest: Quest prefab is missing."); return; }
        view = Instantiate(questPrefab);
        view.questRoot.SetActive(true);
        if (view.startCard != null) view.startCard.SetActive(false);
        if (view.entry != null) { view.entry.Close(); view.entry.enabled = false; }
        view.choicePanel.Hide(); view.villagerPanel.Hide(); view.chestPanel.Hide(); view.clearPanel.Hide();
        if (view.pipHint != null) view.pipHint.gameObject.SetActive(false);
        battle = view.battlePanel;
        battle.UseLegacyQuiz = true;
        battle.Hide();
        quiz = battle.GetComponent<QuestBattlePresentation>();
        quiz.AnswerEvaluated += OnAnswer;
        font = view.GetComponentInChildren<TextMeshProUGUI>(true).font;
        BuildReport();
        Intro();
    }

    private void Intro()
    {
        round = 0;
        results.Clear();
        title.text = "ことば魔法・3連戦テスト";
        report.text = "同じ攻撃力で、敵のHPを変えて比較します。\n全問を一発正解すると、2・3・4問で撃破。\n再回答の正解は通常ダメージです。\n\n見るところ\n回答から着弾まで待たされない？\n倒したとき、もう1問やりたい？\n正解の演出は気持ちいい？\n\nまず現在のテンポで試し、次に速めと比較。\n本編の育成・報酬は変わりません。";
        nextLabel.text = "3連戦を始める";
        reportRoot.SetActive(true);
        tempoButton.gameObject.SetActive(true);
        UpdateTempoLabel();
    }

    private void Next()
    {
        if (running) return;
        if (round >= targetQuestions.Length) { Intro(); return; }
        if (targetQuestions.Length == 0) return;
        correct = mistakes = critical = impacts = 0;
        retry = false; impactSeconds = 0;
        int attack = Mathf.Clamp(referenceAttack, 1, int.MaxValue / 16);
        int count = Mathf.Clamp(targetQuestions[round], 1, 8);
        int hp = attack * 2 * count;
        battle.ConfigureTiming(fast ? .16f : spellTravelSeconds, fast ? .25f : turnPauseSeconds, resultPauseSeconds);
        reportRoot.SetActive(false);
        startedAt = Time.realtimeSinceStartup;
        running = true;
        battle.Show(BattlePhase.MidPath, "森のまもの", QuestWordList.GetRandom(), hp, 1, 100, 100, attack, 0, OnBattleEnd);
        previousHp = hp;
    }

    private void OnAnswer(bool isCorrect)
    {
        if (!running) return;
        if (!isCorrect) { mistakes++; retry = true; return; }
        correct++;
        if (!retry) critical++;
        retry = false;
    }

    private void Update() { if (running) RecordImpact(); }
    private void RecordImpact()
    {
        if (battle.EnemyHp >= previousHp) return;
        previousHp = battle.EnemyHp;
        impacts++;
        impactSeconds += battle.LastSpellTravelSeconds;
    }

    private void OnBattleEnd(QuestEventResult result)
    {
        if (!running) return;
        RecordImpact();
        float elapsed = Time.realtimeSinceStartup - startedAt;
        running = false;
        battle.Hide();
        int trial = round + 1;
        string summary = $"{trial}戦目：{correct}問 / ミス{mistakes}回 / CRITICAL {critical}回\n" +
            $"着弾平均 {(impacts > 0 ? impactSeconds / impacts : 0):F2}秒\n" +
            $"回答・結果確認込み {elapsed:F1}秒";
        results.AppendLine(summary).AppendLine();
        Debug.Log("[WordMagicPlaytest] " + (fast ? "速め" : "現在") + " " + summary.Replace('\n', ' '));
        round++;
        title.text = round >= targetQuestions.Length ? "3連戦の比較結果" : $"{trial}戦目 おつかれさま！";
        report.text = (round >= targetQuestions.Length ? results.ToString() : summary + "\n\n") +
            "テンポ：遅い / ちょうどいい / 急ぎすぎ\n問題数：少ない / ちょうどいい / 多い\n正解の演出：物足りない / 気持ちいい\n\nもう1戦、遊びたい？";
        nextLabel.text = round >= targetQuestions.Length ? "設定に戻って、もう一度" : "次の戦闘へ";
        tempoButton.gameObject.SetActive(false);
        reportRoot.SetActive(true);
    }

    private void UpdateTempoLabel() => tempoLabel.text = fast ? "テンポ：速め（タップで変更）" : "テンポ：現在（タップで変更）";

    private void BuildReport()
    {
        var canvasObject = new GameObject("Playtest report", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 500;
        var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080,1920); scaler.matchWidthOrHeight = 0;
        reportRoot = Box("Report", canvasObject.transform, 0,0,1,1).gameObject;
        var background = reportRoot.AddComponent<Image>(); background.color = new Color(.035f,.075f,.13f,.98f);
        var safe = Box("Safe area", reportRoot.transform,0,0,1,1); safe.gameObject.AddComponent<QuestSafeArea>();
        title = Label("Title",safe,.07f,.85f,.93f,.94f,46);
        report = Label("Report text",safe,.08f,.27f,.92f,.83f,32); report.alignment = TextAlignmentOptions.TopLeft;
        tempoButton = MakeButton("Tempo",safe,.08f,.17f,.92f,.24f,out tempoLabel);
        tempoButton.onClick.AddListener(()=>{ fast=!fast; UpdateTempoLabel(); });
        var next = MakeButton("Next",safe,.08f,.06f,.92f,.14f,out nextLabel); next.onClick.AddListener(Next);
    }
    private static RectTransform Box(string name,Transform parent,float x0,float y0,float x1,float y1)
    {
        var rt = new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>(); rt.SetParent(parent,false);
        rt.anchorMin = new Vector2(x0,y0); rt.anchorMax = new Vector2(x1,y1); rt.offsetMin=rt.offsetMax=Vector2.zero;
        return rt;
    }
    private TextMeshProUGUI Label(string name,Transform parent,float x0,float y0,float x1,float y1,int size)
    {
        var text = Box(name,parent,x0,y0,x1,y1).gameObject.AddComponent<TextMeshProUGUI>();
        text.font=font; text.fontSize=size; text.enableAutoSizing=true; text.fontSizeMin=24; text.fontSizeMax=size;
        text.color=new Color(.96f,.92f,.82f); text.alignment=TextAlignmentOptions.Center; text.raycastTarget=false;
        return text;
    }
    private Button MakeButton(string name,Transform parent,float x0,float y0,float x1,float y1,out TextMeshProUGUI label)
    {
        var rt=Box(name,parent,x0,y0,x1,y1);
        var graphic=rt.gameObject.AddComponent<Image>(); graphic.color=new Color(.22f,.34f,.46f);
        var button=rt.gameObject.AddComponent<Button>(); button.targetGraphic=graphic;
        label=Label(name+" label",rt,.02f,.04f,.98f,.96f,34);
        return button;
    }
    private void OnDestroy()
    {
        if (quiz != null) quiz.AnswerEvaluated -= OnAnswer;
        if (view != null) Destroy(view.gameObject);
    }
}
