using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>コマンド選択と魔法一覧の表示。画面切替だけでは戦闘を進めない。</summary>
public sealed class BattleCommandMenu : MonoBehaviour
{
    public GameObject commandRoot, mainCommands, spellCommands, mpRoot;
    public Button magicButton, defendButton, fireButton, backButton;
    public TMP_Text messageText, mpText, fireText;
    public Slider mpSlider;
    private BattlePanel battle;
    private bool choosingSpell;
    public bool IsChoosingSpell => choosingSpell;

    private void Awake() => Refresh();

    public void Bind(BattlePanel target)
    {
        battle = target;
        choosingSpell = false;
        magicButton.onClick.RemoveListener(OpenSpells); magicButton.onClick.AddListener(OpenSpells);
        defendButton.onClick.RemoveListener(Defend); defendButton.onClick.AddListener(Defend);
        fireButton.onClick.RemoveListener(CastFire); fireButton.onClick.AddListener(CastFire);
        backButton.onClick.RemoveListener(Back); backButton.onClick.AddListener(Back);
        fireText.text = BattleActions.Fire.Name + "　MP " + BattleActions.Fire.MpCost;
        Refresh();
    }
    private bool CanChoose => battle != null && battle.isActiveAndEnabled && !battle.UseLegacyQuiz && battle.CurrentTurn == BattleTurnState.PlayerTurn;
    public void OpenSpells() { if (!CanChoose) return; choosingSpell = true; Refresh(); }
    public void Back() { if (!CanChoose) return; choosingSpell = false; Refresh(); }
    private void Defend() { if (CanChoose) battle.TryDefend(); }
    private void CastFire() { if (CanChoose) battle.TryCastSpell(BattleActions.Fire); }
    public void Refresh()
    {
        if (battle == null)
        {
            if (commandRoot != null) commandRoot.SetActive(false);
            if (mpRoot != null) mpRoot.SetActive(false);
            return;
        }
        bool normal = !battle.UseLegacyQuiz;
        bool active = normal && battle.CurrentTurn != BattleTurnState.Hidden;
        bool terminal = battle.CurrentTurn == BattleTurnState.Victory || battle.CurrentTurn == BattleTurnState.Defeat;
        if (!CanChoose) choosingSpell = false;
        commandRoot.SetActive(active && !terminal);
        mpRoot.SetActive(active);
        mainCommands.SetActive(!choosingSpell);
        spellCommands.SetActive(choosingSpell);
        magicButton.interactable = defendButton.interactable = backButton.interactable = CanChoose;
        fireButton.interactable = CanChoose && battle.CanCastSpell(BattleActions.Fire);
        mpText.text = $"MP {battle.PlayerMp} / {battle.PlayerMaxMp}";
        mpSlider.maxValue = battle.PlayerMaxMp; mpSlider.value = battle.PlayerMp;
        messageText.text = choosingSpell
            ? (battle.CanCastSpell(BattleActions.Fire) ? "魔法を選んでください" : "MPが足りません。防御でMPを回復できます")
            : battle.ActionMessage;
    }
}
