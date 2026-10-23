using UnityEngine;

/// <summary>
/// 敵スポナー - 定期的に画面右から敵を生成する
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    [Header("スポーン設定")]
    public GameObject enemyPrefab; // さっき作ったプレハブを入れる
    public float spawnInterval = 2.0f; // 何秒ごとに敵が出るか

    [Header("スポーン位置のランダム範囲（Y軸）")]
    public float spawnYMin = -2f;
    public float spawnYMax = 2f;

    private float timer;

    void Update()
    {
        timer += Time.deltaTime;

        if (timer >= spawnInterval)
        {
            SpawnEnemy();
            timer = 0f;
        }
    }

    void SpawnEnemy()
    {
        if (enemyPrefab == null)
        {
            Debug.LogWarning("敵のプレハブが設定されていません！");
            return;
        }

        // Y座標をランダムにして、少し変化をつける
        float randomY = Random.Range(spawnYMin, spawnYMax);
        Vector3 spawnPosition = new Vector3(transform.position.x, randomY, 0f);

        // 自分の場所に敵を生成
        Instantiate(enemyPrefab, spawnPosition, Quaternion.identity);
    }
}
