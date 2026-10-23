using System.Collections;
using LilyEnglish.Growth;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Idle
{
    /// <summary>
    /// 本編のステージ制（拠点 → ステージ選択 → 戦闘 → ボス → クリア → 拠点）の司令塔。
    /// 進行（解放・クリア）は StageProgress、敵の出現と報酬は GameManager、強化は GrowthService が持ち、
    /// ここは「いまどの画面か」と、背景・ステージ名・演出・画面の出し入れだけを担当する。
    /// シーンへの配置と参照の設定は StageUIBuilder（Assets/Editor）が行う。
    /// </summary>
    public sealed class StageDirector : MonoBehaviour
    {
        public enum Phase { Base, Battle, Cleared, Defeated }

        public static StageDirector Instance { get; private set; }

        [Header("本編")]
        [SerializeField] private GameManager gameManager;
        [Tooltip("本編の全画面背景（BackgroundScroller の付いた Image）")]
        [SerializeField] private Image background;

        [Header("戦闘中の表示（戦闘中だけ出す）")]
        [SerializeField] private GameObject battleHud;
        [SerializeField] private TextMeshProUGUI plateNameText;
        [SerializeField] private TextMeshProUGUI plateProgressText;
        [SerializeField] private Button retreatButton;
        [SerializeField] private TextMeshProUGUI retreatLabel;

        [Header("開始・ボス出現時のバナー")]
        [SerializeField] private CanvasGroup banner;
        [SerializeField] private TextMeshProUGUI bannerTitleText;
        [SerializeField] private TextMeshProUGUI bannerSubText;
        [SerializeField] private float fadeInSeconds = 0.35f;
        [SerializeField] private float holdSeconds = 1.6f;
        [SerializeField] private float fadeOutSeconds = 0.5f;

        [Header("拠点・クリア画面（StageUIRoot）")]
        [SerializeField] private StageSelectView selectView;
        [SerializeField] private StageResultView resultView;
        [SerializeField] private StageDefeatView defeatView;

        private const float RetreatConfirmSeconds = 2.5f;
        private const string RetreatLabelNormal = "拠点へ";
        private const string RetreatLabelConfirm = "もう一度で退却";

        private Sprite defaultBackground;
        private bool captured;
        private Coroutine bannerRoutine;
        private float retreatConfirmUntil;
        private bool clearHandled;

        public Phase CurrentPhase { get; private set; } = Phase.Base;

        public bool IsBattling => CurrentPhase == Phase.Battle;

        /// <summary>起動時の拠点の用意ができた（Start を終えた）</summary>
        public bool IsReady { get; private set; }

        /// <summary>いま遊んでいる（または直前に遊んだ）ステージ番号。0 は未選択</summary>
        public int ShownStage { get; private set; }

        private void Awake()
        {
            Instance = this;
            if (retreatButton != null) retreatButton.onClick.AddListener(OnRetreatPressed);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            if (gameManager == null) gameManager = FindAnyObjectByType<GameManager>();
            Capture();
            // 起動したら拠点から始める（遊ぶステージはプレイヤーが選ぶ）
            EnterBase();
            IsReady = true;
        }

        private void Update()
        {
            if (CurrentPhase != Phase.Battle || gameManager == null) return;

            if (plateProgressText != null)
            {
                plateProgressText.text = gameManager.BossPhase
                    ? "BOSS BATTLE!"
                    : $"撃破 {Mathf.Min(gameManager.currentScore, gameManager.targetScore)} / {gameManager.targetScore}";
            }

            if (retreatLabel != null && retreatConfirmUntil > 0f && Time.unscaledTime > retreatConfirmUntil)
            {
                retreatConfirmUntil = 0f;
                retreatLabel.text = RetreatLabelNormal;
            }
        }

        // ─── 画面の切り替え ──────────────────────────────────────

        /// <summary>拠点（ステージ選択・強化へ進める画面）を出す。戦闘は止めるが、ゴールド・EXP は保存済みのまま</summary>
        public void EnterBase()
        {
            CurrentPhase = Phase.Base;
            clearHandled = false;
            StageProgress.End();
            if (gameManager != null) gameManager.EndBattle();
            StopBanner();
            SetBackground(null);
            if (battleHud != null) battleHud.SetActive(false);
            if (resultView != null) resultView.Hide();
            if (defeatView != null) defeatView.Hide();
            if (selectView != null) selectView.Show();
        }

        /// <summary>選んだステージの戦闘を始める。解放されていないステージは始められない</summary>
        public bool StartStage(int number)
        {
            if (global::QuestManager.IsQuestActive) return false;
            if (!StageProgress.Begin(number)) return false;

            StageInfo info = StageProgress.ActiveInfo;
            ShownStage = number;
            CurrentPhase = Phase.Battle;
            clearHandled = false;
            retreatConfirmUntil = 0f;
            if (retreatLabel != null) retreatLabel.text = RetreatLabelNormal;

            SetBackground(StageProgress.BackgroundFor(number));
            if (selectView != null) selectView.Hide();
            if (resultView != null) resultView.Hide();
            if (defeatView != null) defeatView.Hide();
            if (battleHud != null) battleHud.SetActive(true);
            if (plateNameText != null) plateNameText.text = $"STAGE {info.number}　{info.displayName}";
            if (plateProgressText != null) plateProgressText.text = $"撃破 0 / {StageCatalog.NormalEnemyCount}";

            var growth = GrowthService.Instance;
            if (growth != null) growth.ResetStageExp();

            if (gameManager != null) gameManager.BeginStage();
            ShowBanner($"STAGE {info.number}", info.displayName, holdSeconds);
            return true;
        }

        /// <summary>クリア画面の「もう一度挑戦」。同じステージを最初からやり直す</summary>
        public void RetryStage()
        {
            StartStage(ShownStage);
        }

        /// <summary>クリア画面の「拠点に戻る」</summary>
        public void ReturnToBase()
        {
            EnterBase();
        }

        private void OnRetreatPressed()
        {
            if (CurrentPhase != Phase.Battle || global::QuestManager.IsQuestActive) return;

            // タップ攻撃の最中に押し間違えても戻らないよう、2回押して退却する
            if (retreatConfirmUntil > 0f && Time.unscaledTime <= retreatConfirmUntil)
            {
                EnterBase();
                return;
            }
            retreatConfirmUntil = Time.unscaledTime + RetreatConfirmSeconds;
            if (retreatLabel != null) retreatLabel.text = RetreatLabelConfirm;
        }

        // ─── 戦闘の進行（GameManager から呼ばれる） ───────────────

        /// <summary>本編のHPが尽きた時だけ呼ばれる。クリアや報酬加算は行わない。</summary>
        public void OnPlayerDefeated()
        {
            if (CurrentPhase != Phase.Battle) return;
            CurrentPhase = Phase.Defeated;
            gameManager?.EndBattle();
            StopBanner();
            if (battleHud != null) battleHud.SetActive(false);
            if (defeatView != null) defeatView.Show(ShownStage);
            else { Debug.LogError("Game over view is missing; returning to base."); EnterBase(); }
        }

        public void OnBossAppear()
        {
            ShowBanner("WARNING!", "ボスが現れた！", 1.4f);
        }

        /// <summary>ボスを倒した。クリア画面が出るまで、攻撃を受け付けない</summary>
        public void OnBossDefeated()
        {
            if (CurrentPhase != Phase.Battle) return;
            CurrentPhase = Phase.Cleared;
            if (plateProgressText != null) plateProgressText.text = "STAGE CLEAR!";
        }

        /// <summary>
        /// 飛んでいたコインが所持金へ届いた後に呼ばれる。クリアを記録して（次のステージを解放）、クリア画面を出す。
        /// ゴールドは撃破のたびに所持金へ加算済みなので表示だけ。EXP も撃破ごとに加算済みで、
        /// ここで足すのはクリアボーナスだけ（二重加算しない）
        /// </summary>
        public void OnStageCleared(long goldEarned)
        {
            if (clearHandled || CurrentPhase != Phase.Cleared) return;
            clearHandled = true;

            StageInfo info = StageProgress.ActiveInfo ?? StageCatalog.Get(ShownStage);
            if (info == null) { EnterBase(); return; }

            bool firstClear = StageProgress.Complete(info.number);
            var growth = GrowthService.Instance;
            long bonus = info.clearExpBonus;
            long expTotal = 0;
            if (growth != null)
            {
                growth.GrantStageClearExp(bonus);
                expTotal = growth.StageExpEarned;
            }

            StageInfo next = StageCatalog.Get(info.number + 1);
            bool unlockedNext = firstClear && next != null;
            if (battleHud != null) battleHud.SetActive(false);
            if (resultView != null) resultView.Show(info, goldEarned, expTotal, bonus, unlockedNext ? next : null);
        }

        // ─── 背景・バナー ────────────────────────────────────────

        private void Capture()
        {
            if (captured) return;
            if (background != null) defaultBackground = background.sprite;
            captured = true;
        }

        /// <summary>sprite が null（拠点・画像なし）の時は本編の既定背景に戻す</summary>
        private void SetBackground(Sprite sprite)
        {
            Capture();
            if (background != null) background.sprite = sprite != null ? sprite : defaultBackground;
        }

        public Sprite CurrentBackgroundSprite => background != null ? background.sprite : null;

        private void ShowBanner(string title, string sub, float hold)
        {
            if (banner == null) return;
            if (bannerTitleText != null) bannerTitleText.text = title;
            if (bannerSubText != null) bannerSubText.text = sub;
            if (bannerRoutine != null) StopCoroutine(bannerRoutine);
            bannerRoutine = StartCoroutine(BannerRoutine(hold));
        }

        private void StopBanner()
        {
            if (bannerRoutine != null) { StopCoroutine(bannerRoutine); bannerRoutine = null; }
            if (banner != null) { banner.alpha = 0f; banner.gameObject.SetActive(false); }
        }

        private IEnumerator BannerRoutine(float hold)
        {
            banner.gameObject.SetActive(true);
            yield return Fade(banner.alpha, 1f, fadeInSeconds);
            yield return new WaitForSecondsRealtime(hold);
            yield return Fade(1f, 0f, fadeOutSeconds);
            banner.gameObject.SetActive(false);
            bannerRoutine = null;
        }

        private IEnumerator Fade(float from, float to, float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                banner.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / seconds));
                yield return null;
            }
            banner.alpha = to;
        }

        private void OnDisable()
        {
            bannerRoutine = null;
            if (banner != null)
            {
                banner.alpha = 0f;
                banner.gameObject.SetActive(false);
            }
        }
    }
}
