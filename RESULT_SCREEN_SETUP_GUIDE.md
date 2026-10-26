# 🎉 リザルト画面 セットアップガイド

## 🎯 目標
25問クリア時に表示される「プレイヤーを全力で褒める」リザルト画面を実装します。

---

## 📋 完成イメージ

```
┌─────────────────────────────────┐
│      🏰 浮遊城の背景画像         │
│                                 │
│   ✨ ステージクリア！ ✨        │
│                                 │
│   [リリィの喜び立ち絵]          │
│                                 │
│   獲得ゴールド：1,234 G        │
│   マスター単語：25 / 25        │
│                                 │
│   新魔法『フライ』を習得！      │
│                                 │
│   [タイトルへ]  [シェア]       │
└─────────────────────────────────┘
```

---

## 📦 1. ResultCanvas の作成

### ステップ1: Canvas作成
1. **Hierarchy** で右クリック → `UI` → `Canvas`
2. 名前を **「ResultCanvas」** に変更
3. **Canvas Scaler** の設定:
   - UI Scale Mode: `Scale With Screen Size`
   - Reference Resolution: X `1080`, Y `1920`（縦画面スマホ用）
   - Match: `0.5`（Width と Height の中間）

### ステップ2: Canvas を非表示に
1. ResultCanvas を選択
2. **Inspector 上部のチェックボックスを外す**（非アクティブ化）
   - ゲーム開始時は非表示、クリア時に表示されます

---

## 🎨 2. UI 要素の配置

### A. 背景パネル

1. **ResultCanvas** を右クリック → `UI` → `Panel`
2. 名前を **「BackPanel」** に変更
3. **Rect Transform**:
   - Anchor: `Stretch-Stretch`（全画面）
   - Left, Top, Right, Bottom: すべて `0`
4. **Image** コンポーネント:
   - Color: 黒 `#000000`
   - Alpha: `150` 〜 `200`（半透明）

### B. 浮遊城の背景（オプション）

1. **BackPanel** を右クリック → `UI` → `Image`
2. 名前を **「CastleBackground」** に変更
3. **Image** コンポーネント:
   - Source Image: 浮遊城のスプライト（生成した画像）
   - Preserve Aspect: `✓` ON
4. **Rect Transform**:
   - Pos Y: `300` 〜 `500`（上部に配置）
   - Width/Height: 画像に合わせて調整

### C. リリィの立ち絵

1. **ResultCanvas** を右クリック → `UI` → `Image`
2. 名前を **「LilyImage」** に変更
3. **Image** コンポーネント:
   - Source Image: リリィの喜び立ち絵
   - Preserve Aspect: `✓` ON
4. **Rect Transform**:
   - Pos X: `-200` 〜 `0`（左寄り or 中央）
   - Pos Y: `-100` 〜 `100`
   - Width: `400` 〜 `600`
   - Height: 画像に合わせて調整

### D. タイトルテキスト

1. **ResultCanvas** を右クリック → `UI` → `Text - TextMeshPro`
2. 名前を **「TitleText」** に変更
3. **TextMeshProUGUI** コンポーネント:
   - Text: `ステージクリア！`
   - Font Size: `80` 〜 `100`
   - Alignment: 中央揃え
   - Color: 金色 `#FFD700` または白
4. **Rect Transform**:
   - Anchor: `Top-Center`
   - Pos Y: `-200`
   - Width: `800`, Height: `150`

### E. ゴールド表示テキスト

1. **ResultCanvas** を右クリック → `UI` → `Text - TextMeshPro`
2. 名前を **「GoldText」** に変更
3. **TextMeshProUGUI** コンポーネント:
   - Text: `獲得ゴールド：0 G`（初期値）
   - Font Size: `50` 〜 `60`
   - Alignment: 中央揃え
   - Color: 黄色 `#FFFF00`
4. **Rect Transform**:
   - Anchor: `Middle-Center`
   - Pos Y: `0` 〜 `50`
   - Width: `600`, Height: `80`

### F. 単語数表示テキスト

1. **ResultCanvas** を右クリック → `UI` → `Text - TextMeshPro`
2. 名前を **「WordCountText」** に変更
3. **TextMeshProUGUI** コンポーネント:
   - Text: `マスター単語：0 / 25`（初期値）
   - Font Size: `50` 〜 `60`
   - Alignment: 中央揃え
   - Color: 白 `#FFFFFF`
4. **Rect Transform**:
   - Anchor: `Middle-Center`
   - Pos Y: `-80`
   - Width: `600`, Height: `80`

### G. ご褒美テキスト

1. **ResultCanvas** を右クリック → `UI` → `Text - TextMeshPro`
2. 名前を **「RewardText」** に変更
3. **TextMeshProUGUI** コンポーネント:
   - Text: `新魔法『フライ』を習得！`
   - Font Size: `40` 〜 `50`
   - Alignment: 中央揃え
   - Color: 虹色または金色
4. **Rect Transform**:
   - Anchor: `Middle-Center`
   - Pos Y: `-200`
   - Width: `700`, Height: `120`

### H. タイトルボタン

1. **ResultCanvas** を右クリック → `UI` → `Button - TextMeshPro`
2. 名前を **「TitleButton」** に変更
3. **Button** コンポーネント:
   - Transition: `Color Tint`
   - Normal Color: 青 `#4444FF`
   - Highlighted Color: 明るい青
   - Pressed Color: 暗い青
4. **Rect Transform**:
   - Anchor: `Bottom-Center`
   - Pos Y: `150`
   - Width: `300`, Height: `100`
5. 子オブジェクトの **Text (TMP)** を編集:
   - Text: `タイトルへ`
   - Font Size: `40`

### I. シェアボタン（オプション）

1. **ResultCanvas** を右クリック → `UI` → `Button - TextMeshPro`
2. 名前を **「ShareButton」** に変更
3. **Button** コンポーネント:
   - Transition: `Color Tint`
   - Normal Color: 緑 `#44FF44`
4. **Rect Transform**:
   - Anchor: `Bottom-Center`
   - Pos Y: `50`
   - Width: `300`, Height: `100`
5. 子オブジェクトの **Text (TMP)** を編集:
   - Text: `シェアする`
   - Font Size: `40`

---

## 🔧 3. ResultManager スクリプトの設定

### ステップ1: スクリプトをアタッチ

1. **ResultCanvas** を選択
2. **Add Component** → `Result Manager`

### ステップ2: Inspector で参照を設定

#### UI パーツ
- **Result Canvas**: `ResultCanvas` 自身をドラッグ
- **Lily Image**: `LilyImage` をドラッグ
- **Lily Happy Sprite**: リリィの喜び立ち絵スプライトを設定

#### テキスト
- **Title Text**: `TitleText` をドラッグ
- **Gold Text**: `GoldText` をドラッグ
- **Word Count Text**: `WordCountText` をドラッグ
- **Reward Text**: `RewardText` をドラッグ

#### ボタン
- **Title Button**: `TitleButton` をドラッグ
- **Share Button**: `ShareButton` をドラッグ（任意）

#### 演出設定
- **Text Animation Delay**: `0.5`
- **Gold Count Duration**: `2.0`

#### サウンド
- **Result BGM**: リザルト用BGM（あれば）
- **Success Sound**: クリア成功SE（あれば）

---

## 🎮 4. GameManager との連携

### ステップ1: GameManager に ResultManager を設定

1. **Hierarchy** → `Managers` → `GameManager` を選択
2. **Inspector** の **Result Screen** セクション:
   - **Result Manager**: `ResultCanvas` をドラッグ
   - **Target Score**: `25`（クリアに必要な敵の数）

### ステップ2: 動作確認

1. Play ボタンを押してゲーム開始
2. 敵を25体倒す
3. リザルト画面が表示されることを確認

---

## ✨ 5. 演出の強化（オプション）

### A. フェードイン演出

ResultCanvas に **Canvas Group** コンポーネントを追加:
- Alpha を 0 → 1 にアニメーション
- DOTween や LeanTween を使うとスムーズ

### B. パーティクルエフェクト

1. ResultCanvas の子として `Particle System` を追加
2. 紙吹雪や星が降る演出
3. Duration: `5` 〜 `10` 秒
4. Emission: Rate over Time `20` 〜 `50`

### C. サウンド演出

AudioManager に以下を追加:
- **Result BGM**: 明るい勝利BGM
- **Success Sound**: ファンファーレやベル音

---

## 🐛 6. トラブルシューティング

### Q1. リザルト画面が表示されない

**A**: Console で以下を確認:
```
✓ ★ゲームクリア！スコア: 25/25
✗ GameManager: ResultManager が見つかりません
```

**解決策**:
1. GameManager の Inspector で Result Manager フィールドを確認
2. ResultCanvas に ResultManager コンポーネントがアタッチされているか確認

### Q2. テキストが表示されない

**A**: ResultManager の Inspector で各テキストフィールドが設定されているか確認

**解決策**:
1. TitleText, GoldText, WordCountText が全て設定されているか確認
2. テキストの初期状態が Active になっているか確認

### Q3. ボタンが押せない

**A**: ボタンの On Click イベントは自動登録されます

**解決策**:
1. ResultManager.Start() でボタンイベントが登録されているか確認
2. Console でエラーが出ていないか確認

### Q4. カウントアップアニメーションが動かない

**A**: Time.timeScale が 0 になっていないか確認

**解決策**:
1. ゲームクリア時に Time.timeScale = 0 にしていないか確認
2. している場合は、ResultManager のコルーチンを `WaitForSecondsRealtime` に変更

### Q5. シェア機能が動かない

**A**: Twitter Web Intent は実機でのみ動作します

**解決策**:
1. Unity エディタではブラウザが開くだけです
2. 実機ビルドで確認してください

---

## 📱 7. モバイル対応

### スマホでの表示確認

1. **Game ビュー** で解像度を変更:
   - `Free Aspect` → `iPhone` または `Android`
2. 縦画面（Portrait）での表示を確認
3. 各UI要素が画面外に出ていないか確認

### Safe Area 対応（ノッチ対応）

1. ResultCanvas に **Safe Area** コンポーネントを追加（サードパーティ製）
2. または、上下に余白を持たせる（Padding Top: 100, Bottom: 100）

---

## 🎯 8. 次のステップ

リザルト画面が動作したら:

### A. さらなる演出強化
- リリィのボイス追加
- アニメーション（立ち絵がジャンプなど）
- 背景の動的変化

### B. ソーシャル機能
- スクリーンショット撮影
- Twitter/Facebook 自動投稿
- ランキング表示

### C. 次のステージへの導線
- 「ステージ2」ボタン追加
- ステージ選択画面の実装
- 難易度選択

---

## 💡 デザイナー向け TIPS

### 「褒める」演出の黄金律

1. **即座のフィードバック**: クリア瞬間に成功SEとパーティクル
2. **視覚的な報酬**: ゴールドのカウントアップで達成感
3. **言語化された褒め**: 「素晴らしい！」「完璧！」などの文字
4. **次への期待**: 「新魔法習得」で次のプレイへの動機付け

### 参考ゲーム
- **パズドラ**: リザルト画面のゴールドカウントアップ
- **モンスト**: クリア時の派手な演出
- **ツムツム**: スコア表示とSNSシェア

### 色の心理学
- **金色**: 成功、勝利、報酬
- **虹色**: 特別、レア、ご褒美
- **白**: 清潔、シンプル、達成

---

## 🎉 完成イメージ（フロー）

```
25体目の敵を倒す
  ↓
「ジャラジャラッ！」コイン飛び散り
  ↓
最後のコインがUIに到達
  ↓
currentScore が 25 に到達
  ↓
GameManager.CheckGameClear() 呼び出し
  ↓
ResultManager.ShowResult() 実行
  ↓
背景が暗くなる（半透明パネル）
  ↓
浮遊城の背景が表示される
  ↓
「ステージクリア！」テキスト表示
  ↓
ファンファーレSE再生
  ↓
リリィが喜びの表情で登場
  ↓
ゴールドが 0 → 1,234 にカウントアップ
  ↓
「マスター単語：25 / 25」表示
  ↓
「新魔法『フライ』を習得！」表示
  ↓
紙吹雪パーティクルが降る
  ↓
「タイトルへ」「シェア」ボタン表示
  ↓
プレイヤー「すごい！もう一回やろう！」
```

---

**これで「プレイヤーを全力で褒める」リザルト画面が完成します！**

まずは基本形を作って動かしてみて、徐々に演出を豪華にしていきましょう。
調整したい箇所があれば、いつでもお声がけください！

Happy Coding! 🎮✨
