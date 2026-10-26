# 🎮 リリィイングリッシュクリッカー セットアップガイド

## ハイブリッド型ゲームシステム
**タップでも倒せる + 放置でも倒せる** 最高の両立！

---

## 📁 実装済みスクリプト

### 1. Enemy（敵）
- `WordMonster.cs` - 単語モンスター本体
- `EnemySpawner.cs` - 敵の生成装置

### 2. Player（プレイヤー）
- `LilyAutoAttack.cs` - リリィの自動攻撃システム

### 3. Managers（管理）
- `GameManager.cs` - ゴールド・スコア管理
- `TapAttackManager.cs` - タップ攻撃の管理

---

## 🛠️ Unityエディタでのセットアップ手順

### ステップ1: 敵プレハブの作成

1. **空のGameObjectを作成**
   - ヒエラルキーで `Right Click > Create Empty`
   - 名前: `EnemyBase`

2. **見た目を追加**
   - `EnemyBase` の子として `Sprite Renderer` を追加
   - 敵の画像を設定

3. **単語表示を追加**
   - `EnemyBase` の子として `3D Object > Text - TextMeshPro` を作成
   - フォント: **Noto Sans JP (Dynamic)** を設定
   - サイズ調整して敵の上に配置

4. **HPバーを追加（オプション）**
   - `EnemyBase` の子として `UI > Slider` を追加
   - World Spaceに設定
   - 敵の頭上に配置

5. **コライダーを追加（タップ検出用）**
   - `EnemyBase` に `Box Collider 2D` を追加
   - **重要**: `Is Trigger` は **OFF** にする（タップ検出のため）

6. **スクリプトをアタッチ**
   - `WordMonster.cs` をドラッグ＆ドロップ
   - インスペクターで以下を設定:
     - `Word Text`: さっき作ったTextMeshProをドラッグ
     - `Hp Slider`: HPバーをドラッグ（オプション）
     - `Move Speed`: 3
     - `Max Hp`: 3（タップ3回で倒れる）
     - `Gold Amount`: 100

7. **プレハブ化**
   - `EnemyBase` をプロジェクトウィンドウの `Prefabs` フォルダにドラッグ
   - シーン上の `EnemyBase` は削除してOK

---

### ステップ2: 敵スポナーの設置

1. **空のGameObjectを作成**
   - 名前: `EnemySpawner`
   - 位置: `X: 10, Y: 0, Z: 0`（画面右外）

2. **スクリプトをアタッチ**
   - `EnemySpawner.cs` をドラッグ＆ドロップ
   - インスペクターで設定:
     - `Enemy Prefab`: さっき作った `EnemyBase` プレハブをドラッグ
     - `Spawn Interval`: 2.0（2秒ごとに敵が出る）
     - `Spawn Y Min`: -2
     - `Spawn Y Max`: 2

---

### ステップ3: リリィに自動攻撃エリアを追加

1. **攻撃エリアを作成**
   - リリィのGameObjectを選択
   - `Right Click > Create Empty` で子オブジェクトを作成
   - 名前: `AttackArea`

2. **トリガーを追加**
   - `AttackArea` に `Box Collider 2D` を追加
   - **重要**: `Is Trigger` を **ON** にする
   - サイズ: リリィの前方をカバーするように設定（例: X: 3, Y: 5）
   - 位置: リリィの少し前方（例: X: 1.5, Y: 0）

3. **スクリプトをアタッチ**
   - `LilyAutoAttack.cs` をドラッグ＆ドロップ
   - インスペクターで設定:
     - `Attack Interval`: 1.0（1秒ごとに自動攻撃）
     - `Auto Damage`: 1
     - `Hit Effect`: エフェクトプレハブ（オプション）

---

### ステップ4: GameManagerの設置

1. **空のGameObjectを作成**
   - 名前: `GameManager`
   - 位置: `0, 0, 0`

2. **スクリプトをアタッチ**
   - `GameManager.cs` をドラッグ＆ドロップ

3. **UI作成（Canvas内）**
   - Canvas内に `Text - TextMeshProUGUI` を2つ作成:
     - `GoldText`: ゴールド表示用
     - `ScoreText`: スコア表示用

4. **UIを接続**
   - GameManagerのインスペクターで:
     - `Gold Text`: GoldTextをドラッグ
     - `Score Text`: ScoreTextをドラッグ

---

### ステップ5: TapAttackManagerの設置（オプション）

1. **空のGameObjectを作成**
   - 名前: `TapAttackManager`

2. **スクリプトをアタッチ**
   - `TapAttackManager.cs` をドラッグ＆ドロップ
   - インスペクターで設定:
     - `Tap Effect`: タップエフェクトプレハブ（オプション）
     - `Tap Sound`: タップ音（オプション）
     - `Hit Sound`: ヒット音（オプション）

---

## 🎯 動作確認

再生ボタンを押すと：

1. ✅ 右から敵が2秒ごとに出現
2. ✅ 敵をタップすると1ダメージ（3回タップで倒れる）
3. ✅ 敵がリリィの攻撃エリアに入ると1秒ごとに自動攻撃
4. ✅ 敵を倒すとゴールド+100、スコア+1
5. ✅ 画面外に出た敵は自動削除

---

## 🎨 次のステップ

### 推奨する追加機能:
- [ ] エフェクト追加（パーティクルシステム）
- [ ] サウンド追加（BGM、効果音）
- [ ] 単語リストの実装（ランダム表示）
- [ ] アップグレードシステム（攻撃力UP、自動攻撃速度UP）
- [ ] ゴールドでアイテム購入
- [ ] ステージシステム

---

## 💡 カスタマイズのヒント

### 難易度調整:
- `WordMonster.maxHp` を増やす → 敵が硬くなる
- `EnemySpawner.spawnInterval` を減らす → 敵が増える
- `LilyAutoAttack.attackInterval` を増やす → 自動攻撃が遅くなる（タップ重要度UP）

### タップゲーム寄りにしたい場合:
- `LilyAutoAttack.autoDamage` を 0 にする → 完全タップゲーム
- `WordMonster.maxHp` を増やす → タップ連打が重要に

### 放置ゲーム寄りにしたい場合:
- `LilyAutoAttack.attackInterval` を減らす → 自動攻撃が速くなる
- `WordMonster.maxHp` を減らす → すぐ倒せる

---

Happy Coding! 🚀
