using UnityEngine;
using TMPro;

/// <summary>
/// ダメージポップアップ - 上に移動しながらフェードアウトして消える数字演出
/// </summary>
public class DamagePopup : MonoBehaviour
{
    [Header("動きの設定")]
    public float moveSpeed = 3f;     // 上昇する速さ
    public float lifeTime = 1f;      // 表示される時間（秒）
    public float spreadRange = 0.5f; // 左右に散らばる範囲

    private TextMeshPro textMesh;
    private Color initialColor;
    private float timer;

    void Awake()
    {
        textMesh = GetComponent<TextMeshPro>();
        initialColor = textMesh.color;
    }

    /// <summary>
    /// 生成時に呼び出されるセットアップ関数
    /// </summary>
    public void Setup(int damageAmount)
    {
        // 数字をセット
        textMesh.text = damageAmount.ToString();

        // 出現位置を少しランダムに左右に散らす
        float randomX = Random.Range(-spreadRange, spreadRange);
        transform.position += new Vector3(randomX, 0, 0);
    }

    void Update()
    {
        timer += Time.deltaTime;

        // 1. 上に移動し続ける
        transform.position += Vector3.up * moveSpeed * Time.deltaTime;

        // 2. だんだん透明にする（フェードアウト）
        float alpha = 1f - (timer / lifeTime);
        textMesh.color = new Color(initialColor.r, initialColor.g, initialColor.b, alpha);

        // 3. 時間が来たら消滅
        if (timer >= lifeTime)
        {
            Destroy(gameObject);
        }
    }
}
