using UnityEngine;

/// <summary>
/// タップ攻撃の管理 - 画面タップを検出してエフェクトを出す
/// </summary>
public class TapAttackManager : MonoBehaviour
{
    public static TapAttackManager Instance { get; private set; }

    [Header("タップエフェクト")]
    public GameObject tapEffect; // タップ位置に出すエフェクト

    [Header("サウンド")]
    public AudioClip tapSound; // タップ音
    public AudioClip hitSound; // ヒット音

    private AudioSource audioSource;
    private Camera mainCamera;

    void Awake()
    {
        // シングルトン設定
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        mainCamera = Camera.main;
    }

    void Update()
    {
        // PC/モバイル両対応のタップ検出
        if (Input.GetMouseButtonDown(0))
        {
            HandleTap(Input.mousePosition);
        }

        // モバイルのマルチタッチ対応
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began)
            {
                HandleTap(touch.position);
            }
        }
    }

    /// <summary>
    /// タップ処理
    /// </summary>
    void HandleTap(Vector3 screenPosition)
    {
        // タップ位置にエフェクトを出す
        if (tapEffect != null)
        {
            Vector3 worldPosition = mainCamera.ScreenToWorldPoint(screenPosition);
            worldPosition.z = 0;
            Instantiate(tapEffect, worldPosition, Quaternion.identity);
        }

        // タップ音を鳴らす
        if (tapSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(tapSound);
        }
    }

    /// <summary>
    /// 敵にヒットした時のサウンドを鳴らす（WordMonsterから呼ばれる）
    /// </summary>
    public void PlayHitSound()
    {
        if (hitSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(hitSound);
        }
    }
}
