using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Idle
{
    /// <summary>
    /// 英単語データを管理するマネージャークラス
    /// CSVファイルから単語データを読み込み、ゲーム全体で使用できるようにします
    /// </summary>
    public class WordManager : MonoBehaviour
    {
        // シングルトンインスタンス
        public static WordManager Instance { get; private set; }

        [Header("読み込んだ単語データ")]
        [SerializeField]
        private List<WordData> wordList = new List<WordData>();

        // CSVファイル名（拡張子なし）
        private const string CSV_FILE_NAME = "WordData";

        /// <summary>
        /// 読み込まれた単語リスト（読み取り専用）
        /// </summary>
        public List<WordData> WordList => wordList;

        /// <summary>
        /// 読み込まれた単語の数
        /// </summary>
        public int WordCount => wordList.Count;

        private void Awake()
        {
            // シングルトンパターンの実装
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                LoadCSV();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// CSVファイルから単語データを読み込む
        /// </summary>
        private void LoadCSV()
        {
            // Resourcesフォルダからロード
            TextAsset csvFile = Resources.Load<TextAsset>(CSV_FILE_NAME);

            if (csvFile == null)
            {
                Debug.LogError($"CSVファイルが見つかりません: Resources/{CSV_FILE_NAME}.csv");
                Debug.LogError("Assets/Resources/ フォルダに WordData.csv を配置してください。");
                return;
            }

            wordList.Clear();
            StringReader reader = new StringReader(csvFile.text);

            // 1行目（ヘッダー）を読み飛ばす
            string header = reader.ReadLine();
            if (header == null)
            {
                Debug.LogError("CSVファイルが空です。");
                return;
            }

            int lineNumber = 2; // ヘッダーの次から
            while (reader.Peek() != -1)
            {
                string line = reader.ReadLine();

                // 空行はスキップ
                if (string.IsNullOrWhiteSpace(line))
                {
                    lineNumber++;
                    continue;
                }

                string[] columns = line.Split(',');

                // カラム数チェック（データ壊れ防止）
                if (columns.Length >= 5)
                {
                    WordData data = new WordData();

                    // 型変換（TryParseで安全策）
                    if (!int.TryParse(columns[0], out data.id))
                    {
                        Debug.LogWarning($"行{lineNumber}: IDの解析に失敗しました。'{columns[0]}'");
                    }

                    data.english = columns[1].Trim();
                    data.reading = columns[2].Trim();
                    data.meaning = columns[3].Trim();

                    if (!int.TryParse(columns[4], out data.maxHp))
                    {
                        Debug.LogWarning($"行{lineNumber}: MaxHPの解析に失敗しました。'{columns[4]}'");
                    }

                    wordList.Add(data);
                }
                else
                {
                    Debug.LogWarning($"行{lineNumber}: カラム数が不足しています。（必要: 5, 実際: {columns.Length}）");
                }

                lineNumber++;
            }

            Debug.Log($"<color=green>単語データロード完了: {wordList.Count}個の単語を読み込みました。</color>");
        }

        /// <summary>
        /// ランダムに単語を1つ取得する
        /// </summary>
        /// <returns>ランダムに選ばれた単語データ、リストが空の場合はnull</returns>
        public WordData GetRandomWord()
        {
            if (wordList.Count == 0)
            {
                Debug.LogWarning("単語リストが空です。");
                return null;
            }
            return wordList[Random.Range(0, wordList.Count)];
        }

        /// <summary>
        /// IDから単語データを取得する
        /// </summary>
        /// <param name="id">単語ID</param>
        /// <returns>指定されたIDの単語データ、見つからない場合はnull</returns>
        public WordData GetWordById(int id)
        {
            return wordList.Find(word => word.id == id);
        }

        /// <summary>
        /// 指定された範囲のHPを持つ単語をランダムに取得する
        /// </summary>
        /// <param name="minHp">最小HP</param>
        /// <param name="maxHp">最大HP</param>
        /// <returns>条件に合う単語データ、見つからない場合はnull</returns>
        public WordData GetRandomWordByHpRange(int minHp, int maxHp)
        {
            List<WordData> filteredWords = wordList.FindAll(word => word.maxHp >= minHp && word.maxHp <= maxHp);

            if (filteredWords.Count == 0)
            {
                Debug.LogWarning($"HP範囲 {minHp}-{maxHp} に該当する単語が見つかりません。");
                return null;
            }

            return filteredWords[Random.Range(0, filteredWords.Count)];
        }
    }
}
