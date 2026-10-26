# 🪙 コインエクスプロージョン セットアップガイド

## 🎯 目標
敵を倒すと10〜20個のコインが飛び散り、UIのゴールド表示に吸い込まれる「気持ちいい」演出を実装します。

---

## 📦 1. コインPrefabの作成

### ステップ1: 基本オブジェクト作成
1. **Hierarchy** で右クリック → `2D Object` → `Sprite` を作成
2. 名前を **「Coin」** に変更
3. Transform の Scale を `(0.5, 0.5, 1)` に設定（適度なサイズ）

### ステップ2: スプライト設定
- **Inspector** → `Sprite Renderer`
  - Sprite: 黄色い円画像 or コイン画像（後で差し替え可能）
  - Color: 黄色 (#FFD700) ← 金色っぽく
  - Sorting Layer: UI より下（背景より上）

### ステップ3: 物理コンポーネント追加
1. **Add Component** → `Rigidbody 2D`
   - Body Type: `Dynamic`
   - Gravity Scale: `1.0`（重力で落ちる）
   - Mass: `0.1`（軽めに設定）
   - Linear Drag: `0.5`（空気抵抗）
   - Angular Drag: `0.05`（回転の抵抗）

2. **Add Component** → `Circle Collider 2D`
   - Radius: `0.5`（スプライトに合わせる）
   - Is Trigger: `false`（物理的に跳ね返る）

### ステップ4: スクリプトアタッチ
1. **Add Component** → `Coin` (スクリプト)
   - Burst Duration: `0.7`
   - Fly Speed: `8`
   - Collect Distance: `0.5`

### ステップ5: Prefab化
1. **Project** ビュー → `Assets/Prefabs` フォルダを作成（なければ）
2. Hierarchy の `Coin` オブジェクトを `Prefabs` フォルダにドラッグ
3. Hierarchy の Coin は削除してOK（Prefabから生成されるため）

---

## ✨ 2. キラキラパーティクルの追加（豪華版）

### コインPrefabを開く
1. Project ビューの `Coin` プレハブをダブルクリック
2. Prefab編集モードに入る

### パーティクルシステム追加
1. Coin オブジェクトを右クリック → `Effects` → `Particle System`
2. 名前を **「Sparkle」** に変更
3. Transform の Position を `(0, 0, 0)` に設定

### パーティクル設定

#### Main モジュール
- Duration: `1.0`
- Looping: `✓` ON
- Start Lifetime: `0.5`
- Start Speed: `0.5`
- Start Size: `0.1` 〜 `0.2`（ランダム）
- Start Color: 黄色 → 白のグラデーション
- Simulation Space: `World`（重要！コインが動いてもキラキラは残る）
- Max Particles: `20`

#### Emission モジュール
- Rate over Time: `10`（毎秒10個）

#### Shape モジュール
- Shape: `Circle`
- Radius: `0.2`
- Emit from Edge: OFF

#### Color over Lifetime モジュール
- Color: 黄色（不透明） → 黄色（透明）に変化
  - キーフレーム1: Alpha 255 (Time 0)
  - キーフレーム2: Alpha 0 (Time 1)

#### Size over Lifetime モジュール（オプション）
- Size: 小 → 大 → 小 （カーブで調整）

#### Renderer モジュール
- Render Mode: `Billboard`
- Material: `Default-Particle`（または専用のキラキラマテリアル）

---

## 🎮 3. CoinSpawnerの配置

### ステップ1: オブジェクト作成
1. **Hierarchy** で右クリック → `Create Empty`
2. 名前を **「CoinSpawner」** に変更
3. Transform の Position を `(0, 0, 0)` に設定

### ステップ2: スクリプトアタッチ
1. **Add Component** → `CoinSpawner` (スクリプト)

### ステップ3: Inspector設定

#### コイン設定
- **Coin Prefab**: 先ほど作成した `Coin` プレハブをドラッグ
- **Coin UI Target**:
  - **方法A（自動）**: 空欄のままでOK（Start()で自動検出）
  - **方法B（手動）**: Canvas → `GoldText` をドラッグ

#### エクスプロージョン設定（お好みで調整）
- **Min Coin Count**: `10`（最小コイン数）
- **Max Coin Count**: `20`（最大コイン数）
- **Explosion Radius**: `2.0`（未使用だが将来用）
- **Min Force**: `3.0`（弱い力）
- **Max Force**: `8.0`（強い力）
- **Upward Bias**: `1.5`（上方向の偏り、噴水のように）
- **Coin Spawn Interval**: `0.05`（コイン生成の時間差）

#### サウンド
- **Play Burst Sound**: `✓` ON

---

## 🔊 4. サウンド設定（オプション）

### AudioManagerの設定
1. Hierarchy → `Managers` → `AudioManager` を選択
2. Inspector で以下を設定:
   - **Coin**: 既存のコインSE（そのまま）
   - **Coin Burst**: ジャララッという飛び散りSE（あれば設定）
   - **Coin Collect**: ピコーンという回収SE（あれば設定）

> **NOTE**: `CoinBurst` と `CoinCollect` が空でも、フォールバックで `Coin` SEが使われます。

---

## 🎨 5. ビジュアル強化（推奨）

### コイン画像の差し替え
1. お好みのコイン画像を `Assets/Sprites` にインポート
2. Texture Type を `Sprite (2D and UI)` に設定
3. Coin Prefab の `Sprite Renderer` → Sprite に設定

### おすすめの画像
- 金色の円
- コインのイラスト
- 魔法少女風のキラキラコイン

### カラーバリエーション
Sprite Renderer の Color を変えることで：
- 黄色 (#FFD700): ゴールド
- 銀色 (#C0C0C0): シルバー
- 虹色: レインボーコイン（レア報酬用）

---

## ✅ 6. 動作確認チェックリスト

### 必須確認項目
- [ ] Coin Prefab が作成されている
- [ ] CoinSpawner が Hierarchy に配置されている
- [ ] CoinSpawner の Coin Prefab フィールドに Coin が設定されている
- [ ] UIManager の GoldText が設定されている
- [ ] 敵を倒すとコインが飛び散る
- [ ] コインがUIゴールド表示に吸い込まれる
- [ ] ゴールドが加算される
- [ ] ゴールドテキストがバウンドする

### デバッグ用ログ確認
Console で以下のログが出ているか確認:
```
✓ CoinSpawner: UIターゲットを自動検出しました - GoldText
✓ 報酬獲得: 50 Gold (単語の長さ: 5文字)
```

エラーが出る場合:
```
✗ CoinSpawner: coinPrefab が設定されていません！
  → Coin Prefab を設定してください

✗ CoinSpawner: UIターゲットが見つかりません
  → UIManager の GoldText を設定してください

✗ EnemyController: CoinSpawner が見つかりません
  → Hierarchy に CoinSpawner を配置してください
```

---

## 🎯 7. 微調整のポイント

### コインが飛びすぎる場合
- `Max Force` を下げる（8 → 5）
- `Gravity Scale` を上げる（1 → 1.5）

### コインの吸い込みが遅い場合
- `Fly Speed` を上げる（8 → 12）

### コインが少ない/多い場合
- `Min/Max Coin Count` を調整

### コインが早く消える場合
- `Burst Duration` を長くする（0.7 → 1.0）

### コインがUIに届かない場合
- `Collect Distance` を大きくする（0.5 → 1.0）

---

## 🚀 8. 次のステップ

コインエクスプロージョンが動作したら：

### A. リザルト画面の実装
- クリア画面
- 獲得ゴールド・スコアの表示
- リリィの喜び立ち絵
- SNSシェア機能

### B. さらなるビジュアル強化
- コインのアニメーション（回転スプライト）
- レアコイン（虹色、大きさ2倍）
- 背景エフェクト

### C. サウンド追加
- 専用SE（ジャララッ、ピコーン）
- BGM

---

## 💡 デザイナー向けTIPS

### 「気持ちよさ」の黄金比
1. **飛び散り**: 0.7秒（この間に「やった！」と思わせる）
2. **吸い込み速度**: 速すぎず遅すぎず（プレイヤーの目で追える）
3. **コイン数**: 10〜20個（多すぎると処理落ち、少ないと寂しい）

### 参考ゲーム
- パズドラのコイン演出
- モンストのドロップ演出
- ツムツムのスコア演出

### A/Bテスト推奨項目
- コインの数
- 飛び散る力の強さ
- 吸い込み速度
- パーティクルの量

---

## 📞 トラブルシューティング

### Q1. コインが生成されない
**A**: Console で「CoinSpawner が見つかりません」というログを確認。
       Hierarchy に CoinSpawner があるか確認してください。

### Q2. コインが画面外に飛んで消える
**A**: `Max Force` を下げるか、`Burst Duration` を短くしてください。

### Q3. コインがUIに吸い込まれない
**A**: `Coin UI Target` が設定されているか確認。
       UIManager.GoldText が null でないか確認してください。

### Q4. ゴールドが加算されない
**A**: Coin.cs の `CollectCoin()` が呼ばれているか Console でログ確認。
       `Managers.Instance.gameManager` が null でないか確認してください。

### Q5. コインが回転しない
**A**: Rigidbody2D の `Angular Drag` を下げてください（0.05 → 0.01）。

---

## 🎉 完成イメージ

```
敵を倒す
  ↓
「ジャララッ！」SE + 10〜20個のコインが四方八方に飛び散る
  ↓
コインがキラキラしながら放物線を描く（0.7秒）
  ↓
一斉にUIゴールド表示に向かって吸い込まれる
  ↓
「ピコン、ピコン、ピコン...」と次々にゴールド加算
  ↓
ゴールドテキストがバウンド！
  ↓
「気持ちいい！もう1回！」← プレイヤーの感想
```

---

**これで「売れるゲーム」の核心である「気持ちよさ」が完成します！**

まずはUnityで再生して、この快感を体験してみてください。
調整したい箇所があれば、いつでもお声がけください！

Happy Coding! 🎮✨
