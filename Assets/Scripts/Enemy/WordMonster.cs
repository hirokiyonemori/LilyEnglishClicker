using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// 単語モンスター - 右から左へ移動し、リリィに倒される敵
/// タップでも攻撃できる（ハイブリッド型）
/// </summary>
public class WordMonster : MonoBehaviour
{
    [Header("移動速度")]
    public float moveSpeed = 3.0f;

    [Header("HP設定")]
    public int maxHp = 3; // 倒すのに必要なタップ数
    private int currentHp;

    [Header("獲得ゴールド")]
    public int goldAmount = 100;

    [Header("UI参照")]
    public TextMeshPro wordText; // ここにTMPをドラッグ＆ドロップ
    public Slider hpSlider; // HPバー（オプション）

    [Header("エフェクト")]
    public GameObject damageEffect; // ダメージエフェクト（オプション）
    public GameObject damagePopupPrefab; // ダメージ数字ポップアップ

    private bool isDead = false;

    void Start()
    {
        // HP初期化
        currentHp = maxHp;
        UpdateHPBar();

        // 登場時にランダムな単語をセット（後でリスト化しましょう）
        if (wordText != null)
        {
            wordText.text = "Apple";
        }
    }

    void Update()
    {
        if (isDead) return;

        // ずっと左へ進む
        transform.Translate(Vector3.left * moveSpeed * Time.deltaTime);

        // 画面の左端（-10あたり）を超えたら消す（メモリ節約）
        if (transform.position.x < -10f)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// タップでダメージを受ける
    /// </summary>
    public void TakeDamage(int damage = 1)
    {
        if (isDead) return;

        currentHp -= damage;
        UpdateHPBar();

        // ダメージエフェクト
        if (damageEffect != null)
        {
            Instantiate(damageEffect, transform.position, Quaternion.identity);
        }

        // 倒れたか確認
        if (currentHp <= 0)
        {
            Die();
        }
    }

    /// <summary>
    /// 敵が倒れた時の処理
    /// </summary>
    void Die()
    {
        if (isDead) return;
        isDead = true;

        // ゴールド獲得
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddGold(goldAmount);
        }

        // 消滅
        Destroy(gameObject);
    }

    /// <summary>
    /// HPバーの更新
    /// </summary>
    void UpdateHPBar()
    {
        if (hpSlider != null)
        {
            hpSlider.value = (float)currentHp / maxHp;
        }
    }

    /// <summary>
    /// マウス/タップクリックで呼ばれる
    /// </summary>
    void OnMouseDown()
    {
        TakeDamage(1);
        ShowDamagePopup(1);
        Debug.Log("敵をタップ！残りHP: " + currentHp);
    }

    /// <summary>
    /// ダメージポップアップを生成する
    /// </summary>
    void ShowDamagePopup(int damage)
    {
        if (damagePopupPrefab == null) return;

        // タップした位置をワールド座標に変換
        Vector3 spawnPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        spawnPos.z = 0;

        GameObject popup = Instantiate(damagePopupPrefab, spawnPos, Quaternion.identity);
        popup.GetComponent<DamagePopup>().Setup(damage);
    }
}
