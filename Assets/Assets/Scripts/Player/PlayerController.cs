using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace Idle
{
    // UI Image または SpriteRenderer の両方に対応
    public class PlayerController : MonoBehaviour
    {
        public enum PlayerState
        {
            Idle,
            Running,
            Attacking
        }

        [Header("Sprite Settings")]
        [SerializeField] private Sprite idleSprite;      // 待機状態の画像
        [SerializeField] private Sprite runningSprite;   // 走り状態の画像（移動中）
        [Tooltip("走り姿勢の1枚絵に重心移動と足元の効果を合わせ、短い移動を表現する")]
        [SerializeField] private bool smoothTravel;
        [SerializeField] private Sprite attackSprite;    // 攻撃（杖を振る）の画像
        [SerializeField] private float attackPoseDuration = 0.2f; // 攻撃絵を表示する時間（秒）

        [Header("Animation Settings")]
        [SerializeField] private float bobbingSpeed = 10f;
        [SerializeField] private float bobbingAmount = 10f; // UI用に調整

        [Header("=== 魔法弾設定 ===")]
        [Tooltip("魔法弾のプレハブ")]
        [SerializeField] private GameObject lilyMagicPrefab;
        
        [Tooltip("弾が発射される位置（杖の先端など）")]
        [SerializeField] private Transform firePoint;

        // 基準位置。UI の場合は RectTransform.anchoredPosition（アンカー基準）で保持する。
        // 以前は transform.localPosition を Start 時に保存して毎フレーム書き戻していたが、
        // localPosition は親（SafeArea 配下の PnlSafe）の大きさで値が変わるため、
        // Start 後に解像度や SafeArea が変わると立ち絵が画面外へずれていた。
        private Vector3 originalPosition;
        private RectTransform rectTransform;
        private PlayerState currentState = PlayerState.Idle;
        
        // SpriteRenderer または Image どちらかを使用
        private SpriteRenderer spriteRenderer;
        private Image uiImage;
        private bool useUIImage = false;

        // 連打対応用の変数
        private Coroutine currentAttackCoroutine;
        private PlayerState lastNonAttackState = PlayerState.Idle;
        private float travelTime;
        private bool traveling;
        private bool initialized;
        private Quaternion restRotation;
        private Vector3 restScale;
        private TravelFootfallGraphic footfalls;

        private void Start()
        {
            rectTransform = transform as RectTransform;
            originalPosition = GetPosition();
            initialized = true;
            
            // まずSpriteRendererを確認
            spriteRenderer = GetComponent<SpriteRenderer>();
            
            // SpriteRendererがなければImageを確認
            if (spriteRenderer == null)
            {
                uiImage = GetComponent<Image>();
                useUIImage = uiImage != null;
            }

            // 最初は通常画像にしておく
            if (idleSprite != null)
            {
                SetSprite(idleSprite);
            }

            // FirePointが設定されていなければ自分自身を使う
            if (firePoint == null)
            {
                firePoint = transform;
            }
        }

        private void Update()
        {
            if (currentState != PlayerState.Running) StopTravel();
            if (currentState == PlayerState.Running)
            {
                if (smoothTravel)
                {
                    AnimateTravel();
                    return;
                }
                // Simple bobbing animation (Sine wave)
                float newY = originalPosition.y + Mathf.Sin(Time.time * bobbingSpeed) * bobbingAmount;
                SetPosition(new Vector3(originalPosition.x, newY, originalPosition.z));
            }
            else if (currentState == PlayerState.Idle)
            {
                // Return to original position smoothly
                SetPosition(Vector3.Lerp(GetPosition(), originalPosition, Time.deltaTime * 5f));
            }
        }

        private void AnimateTravel()
        {
            if (!traveling)
            {
                traveling = true;
                travelTime = 0;
                restRotation = transform.localRotation;
                restScale = transform.localScale;
                if (useUIImage && footfalls == null)
                {
                    var go = new GameObject("Travel Footfalls", typeof(RectTransform), typeof(CanvasRenderer));
                    go.transform.SetParent(transform.parent, false);
                    go.transform.SetSiblingIndex(transform.GetSiblingIndex());
                    footfalls = go.AddComponent<TravelFootfallGraphic>();
                    footfalls.raycastTarget = false;
                    var r = footfalls.rectTransform;
                    r.anchorMin = rectTransform.anchorMin;
                    r.anchorMax = rectTransform.anchorMax;
                    r.sizeDelta = new Vector2(160, 60);
                    r.anchoredPosition = (Vector2)originalPosition + Vector2.up * (rectTransform.rect.yMin * restScale.y);
                }
            }
            travelTime += Time.deltaTime;
            // Use a stable running pose; cycling the old sheet changes body proportions.
            SetSprite(runningSprite != null ? runningSprite : idleSprite);
            // A small weight transfer, not a floating sine wave. Ease into the stride.
            float weight = Mathf.SmoothStep(0, 1, travelTime / .12f);
            float phase = travelTime * Mathf.PI * 5f;
            float lift = Mathf.Sin(phase); lift *= lift;
            float units = useUIImage ? 1f : .01f;
            SetPosition(originalPosition + new Vector3(Mathf.Sin(phase) * 2f, lift * 4f, 0) * (units * weight));
            transform.localRotation = restRotation * Quaternion.Euler(0, 0, (-.25f + Mathf.Sin(phase) * .35f) * weight);
            if (footfalls != null)
            {
                footfalls.gameObject.SetActive(true);
                footfalls.SetStride(travelTime * 2.5f, weight);
            }
        }

        private void StopTravel()
        {
            if (!traveling) return;
            traveling = false;
            transform.localRotation = restRotation;
            transform.localScale = restScale;
            SetPosition(originalPosition);
            if (footfalls != null) footfalls.gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            StopTravel();
            StopAllCoroutines();
            currentAttackCoroutine = null;
            currentState = lastNonAttackState = PlayerState.Idle;
            if (initialized) SetPosition(originalPosition);
            UpdateSprite();
        }

        private void OnDestroy()
        {
            if (footfalls != null) Destroy(footfalls.gameObject);
        }

        /// <summary>UI なら anchoredPosition、SpriteRenderer なら localPosition を基準座標として扱う</summary>
        private Vector3 GetPosition()
        {
            if (rectTransform != null)
            {
                Vector2 anchored = rectTransform.anchoredPosition;
                return new Vector3(anchored.x, anchored.y, transform.localPosition.z);
            }
            return transform.localPosition;
        }

        private void SetPosition(Vector3 position)
        {
            if (rectTransform != null)
            {
                rectTransform.anchoredPosition = new Vector2(position.x, position.y);
                return;
            }
            transform.localPosition = position;
        }

        /// <summary>
        /// SpriteRendererまたはImageにスプライトを設定
        /// </summary>
        private void SetSprite(Sprite sprite)
        {
            if (sprite == null) return;
            
            if (useUIImage && uiImage != null)
            {
                uiImage.sprite = sprite;
            }
            else if (spriteRenderer != null)
            {
                spriteRenderer.sprite = sprite;
            }
        }

        public void SetState(PlayerState state)
        {
            if (state != PlayerState.Running) StopTravel();
            Debug.Log($"★SetState呼び出し: {state} (現在の状態: {currentState})");

            // 攻撃中に他の状態への遷移が来た場合、攻撃が終わった後の戻り先を更新する
            if (currentState == PlayerState.Attacking)
            {
                lastNonAttackState = state;
                Debug.Log($"★攻撃中のため、戻り先を {state} に設定しました");
                return;
            }

            currentState = state;
            lastNonAttackState = state; // 攻撃以外の状態を記憶

            if (state == PlayerState.Running)
            {
                // Reset phase if needed, or just let it continue based on Time.time
            }

            // 状態に応じてスプライトを更新
            UpdateSprite();
            Debug.Log($"★スプライト更新完了: {state}");
        }

        private void UpdateSprite()
        {
            switch (currentState)
            {
                case PlayerState.Attacking:
                    if (attackSprite != null)
                        SetSprite(attackSprite);
                    break;
                    
                case PlayerState.Running:
                    // 走り専用スプライトがあればそれを使う、なければidleと同じ
                    if (runningSprite != null)
                        SetSprite(runningSprite);
                    else if (idleSprite != null)
                        SetSprite(idleSprite);
                    break;
                    
                case PlayerState.Idle:
                default:
                    if (idleSprite != null)
                        SetSprite(idleSprite);
                    break;
            }
        }

        public void Attack()
        {
            StopTravel();
            Debug.Log("PlayerController: Attack() called");
            
            // 魔法弾を発射 ★廃止
            // FireMagic();

            // すでに攻撃アニメーション中なら止める
            if (currentAttackCoroutine != null)
            {
                StopCoroutine(currentAttackCoroutine);
            }
            
            // 現在が攻撃状態でなければ、戻るべき状態として記憶する
            if (currentState != PlayerState.Attacking)
            {
                lastNonAttackState = currentState;
            }

            // 攻撃アニメーション
            currentAttackCoroutine = StartCoroutine(AttackAnimation());
        }

        /// <summary>
        /// 魔法弾を発射する
        /// </summary>
        private void FireMagic()
        {
            if (lilyMagicPrefab == null)
            {
                Debug.LogWarning("PlayerController: LilyMagicPrefab が設定されていません！");
                return;
            }

            // 発射位置を決定
            Vector3 spawnPosition;
            
            // FirePointが指定されていればその位置、なければ自分の位置
            if (firePoint != null)
            {
                // UIの場合、RectTransformの右端ロジックは「FirePoint自体がPlayer」の時だけに限定する
                // ユーザーが明示的にFirePointオブジェクトを置いたなら、その位置を信用する
                if (useUIImage && firePoint == transform)
                {
                    RectTransform rectTransform = GetComponent<RectTransform>();
                    if (rectTransform != null)
                    {
                        Vector3[] corners = new Vector3[4];
                        rectTransform.GetWorldCorners(corners);
                        spawnPosition = (corners[2] + corners[3]) / 2f; // 右端の中央
                    }
                    else
                    {
                        spawnPosition = firePoint.position;
                    }
                }
                else
                {
                    // FirePointが別オブジェクトならその位置を使う
                    spawnPosition = firePoint.position;
                }
            }
            else
            {
                spawnPosition = transform.position;
            }

            // 魔法弾を生成
            // UIの場合はCanvas内に生成しないと表示がおかしくなるため、Playerと同じ親にする
            GameObject magic = Instantiate(lilyMagicPrefab, transform.parent);
            magic.transform.position = spawnPosition;
            
            // 向きをリセット（プレハブの向き依存）
            magic.transform.rotation = Quaternion.identity;
            
            Debug.Log($"PlayerController: 魔法弾発射！ FirePoint: {(firePoint != null ? firePoint.name : "null")} 位置: {spawnPosition}");
        }

        private IEnumerator AttackAnimation()
        {
            currentState = PlayerState.Attacking;

            // 攻撃スプライトに切り替え
            if (attackSprite != null)
            {
                SetSprite(attackSprite);
            }

            // Simple lunge forward (UI用に値を調整)
            float lungeDistance = useUIImage ? 50f : 0.5f;
            Vector3 targetPos = originalPosition + new Vector3(lungeDistance, 0, 0);
            float duration = 0.1f;
            float elapsed = 0f;

            // 前に出る
            while (elapsed < duration)
            {
                SetPosition(Vector3.Lerp(originalPosition, targetPos, elapsed / duration));
                elapsed += Time.deltaTime;
                yield return null;
            }

            // Return
            elapsed = 0f;
            while (elapsed < duration)
            {
                // 連打されている場合、位置だけは戻る動作をするが、スプライトは変わらない
                SetPosition(Vector3.Lerp(targetPos, originalPosition, elapsed / duration));
                elapsed += Time.deltaTime;
                yield return null;
            }

            // 攻撃絵を表示する時間分待つ
            SetPosition(originalPosition);
            yield return new WaitForSeconds(attackPoseDuration);

            // アニメーション完了後の処理
            // ここまで来たら攻撃終了とみなして元の状態に戻す
            currentState = lastNonAttackState;
            currentAttackCoroutine = null;

            Debug.Log($"★攻撃アニメーション完了。状態を {lastNonAttackState} に戻します");

            // 戻る先の状態に応じてスプライトを更新
            UpdateSprite();
        }
    }
}
