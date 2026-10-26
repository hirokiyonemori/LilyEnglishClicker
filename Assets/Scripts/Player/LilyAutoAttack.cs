using UnityEngine;

/// <summary>
/// リリィの自動攻撃 - 範囲内に入った敵に定期的にダメージを与える
/// ハイブリッド型：タップでも倒せるし、自動でも倒せる
/// </summary>
public class LilyAutoAttack : MonoBehaviour
{
    [Header("自動攻撃設定")]
    public float attackInterval = 1.0f; // 何秒ごとに攻撃するか
    public int autoDamage = 1; // 自動攻撃のダメージ量

    [Header("エフェクト設定")]
    public GameObject hitEffect; // 攻撃エフェクト（オプション）

    [Header("攻撃サウンド")]
    public AudioClip attackSound; // 攻撃音（オプション）

    private AudioSource audioSource;
    private WordMonster currentTarget; // 現在攻撃中の敵
    private float attackTimer;

    void Start()
    {
        // AudioSourceがあれば取得
        audioSource = GetComponent<AudioSource>();
    }

    void Update()
    {
        // ターゲットがいる場合、定期的に攻撃
        if (currentTarget != null)
        {
            attackTimer += Time.deltaTime;

            if (attackTimer >= attackInterval)
            {
                AutoAttack();
                attackTimer = 0f;
            }
        }
    }

    // 何かがエリアに入った瞬間
    void OnTriggerEnter2D(Collider2D other)
    {
        // ぶつかった相手が「WordMonster」スクリプトを持っていたら
        WordMonster monster = other.GetComponent<WordMonster>();

        if (monster != null && currentTarget == null)
        {
            // ターゲットをロック
            currentTarget = monster;
            attackTimer = 0f;
            Debug.Log("敵を自動攻撃ターゲットに設定");
        }
    }

    // 何かがエリアから出た瞬間
    void OnTriggerExit2D(Collider2D other)
    {
        WordMonster monster = other.GetComponent<WordMonster>();

        if (monster != null && monster == currentTarget)
        {
            // ターゲット解除
            currentTarget = null;
            Debug.Log("敵が範囲外へ");
        }
    }

    /// <summary>
    /// 自動攻撃を実行
    /// </summary>
    void AutoAttack()
    {
        if (currentTarget == null) return;

        // ダメージを与える
        currentTarget.TakeDamage(autoDamage);
        Debug.Log("自動攻撃でダメージ: " + autoDamage);

        // エフェクトを出す（設定されている場合）
        if (hitEffect != null)
        {
            Instantiate(hitEffect, currentTarget.transform.position, Quaternion.identity);
        }

        // サウンドを再生（設定されている場合）
        if (attackSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(attackSound);
        }
        else
        {
            SfxDirector.Play(SfxDirector.Attack);   // 未設定なら Resources/Sound/se_attack
        }
    }
}
