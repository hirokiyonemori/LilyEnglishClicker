using UnityEngine;
using TMPro;

/// <summary>
/// ゲーム全体を管理するマネージャー - ゴールド、スコアなどを管理
/// シングルトンパターンで、どこからでもアクセス可能
/// </summary>
public class GameManager : MonoBehaviour
{
    // シングルトンインスタンス
    public static GameManager Instance { get; private set; }

    [Header("ゴールド設定")]
    public int currentGold = 0;
    public TextMeshProUGUI goldText; // UI上のゴールド表示

    [Header("スコア設定")]
    public int enemiesDefeated = 0;
    public TextMeshProUGUI scoreText; // UI上のスコア表示

    void Awake()
    {
        // シングルトンの設定
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // シーン遷移しても消えない
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        UpdateUI();
    }

    /// <summary>
    /// ゴールドを追加する
    /// </summary>
    public void AddGold(int amount)
    {
        currentGold += amount;
        enemiesDefeated++;
        UpdateUI();
    }

    /// <summary>
    /// ゴールドを使う（アップグレードなどで）
    /// </summary>
    public bool SpendGold(int amount)
    {
        if (currentGold >= amount)
        {
            currentGold -= amount;
            UpdateUI();
            return true;
        }
        return false;
    }

    /// <summary>
    /// UI表示を更新
    /// </summary>
    void UpdateUI()
    {
        if (goldText != null)
        {
            goldText.text = "Gold: " + currentGold.ToString();
        }

        if (scoreText != null)
        {
            scoreText.text = "Defeated: " + enemiesDefeated.ToString();
        }
    }
}
