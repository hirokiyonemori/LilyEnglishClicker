using UnityEngine;

/// <summary>
/// リリィをふわふわと浮遊させるスクリプト
/// デザイナー調整用パラメータを公開
/// </summary>
public class LilyFloating : MonoBehaviour
{
    [Header("浮遊の設定")]
    [Tooltip("上下の揺れ幅（大きいほど激しく動く）")]
    public float amplitude = 0.5f;

    [Tooltip("揺れる速さ（大きいほど速く動く）")]
    public float frequency = 1.0f;

    [Header("回転の設定（オプション）")]
    [Tooltip("揺れに合わせて角度を変える")]
    public bool enableRotation = true;

    [Tooltip("最大回転角度")]
    public float maxRotationAngle = 5.0f;

    private Vector3 startPos;
    private Quaternion startRotation;

    void Start()
    {
        startPos = transform.position;
        startRotation = transform.rotation;
    }

    void Update()
    {
        // 時間経過に合わせて上下に座標を計算
        float wave = Mathf.Sin(Time.time * frequency);
        float newY = startPos.y + wave * amplitude;
        transform.position = new Vector3(startPos.x, newY, startPos.z);

        // 浮遊に合わせて角度も変える（オプション）
        if (enableRotation)
        {
            float angle = wave * maxRotationAngle;
            transform.rotation = startRotation * Quaternion.Euler(0, 0, angle);
        }
    }
}
