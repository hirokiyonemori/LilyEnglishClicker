using UnityEngine;
using UnityEngine.UI;

/// <summary>Prefabの表示参照。進行と報酬は既存QuestManagerに任せる。</summary>
public sealed class QuestUIView : MonoBehaviour
{
    public GameObject questRoot;
    /// <summary>クエスト中に隠す入口（常設の「冒険クエスト」ボタン）。QuestOverlayController の hideWhileQuesting に入る</summary>
    public GameObject startCard;
    public QuestEntryController entry;
    /// <summary>ダイアログの「冒険に出る」ボタン。QuestOverlayController.openButton として配線する</summary>
    public Button openButton;
    public QuestChoicePanel choicePanel;
    public VillagerEventPanel villagerPanel;
    public ChestEventPanel chestPanel;
    public BattlePanel battlePanel;
    public QuestClearPanel clearPanel;
    public PipHintController pipHint;
}
