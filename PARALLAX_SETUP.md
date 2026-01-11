# パララックススクロール セットアップガイド

## 必要な背景レイヤー

以下の4層構造を推奨します：

### 1. 遠景（Sky Layer）
- **内容**: 空、雲、遠くの山
- **速度係数**: 0.2（ほとんど動かない）
- **画像サイズ**: 横幅 2048px 推奨

### 2. 中景（Middle Layer）
- **内容**: 遠くの木々、城、建物
- **速度係数**: 0.5（ゆっくり）
- **画像サイズ**: 横幅 2048px 推奨

### 3. 近景（Near Layer）
- **内容**: 道沿いの柵、木、茂み
- **速度係数**: 1.0（標準速度）
- **画像サイズ**: 横幅 2048px 推奨

### 4. 地面（Ground Layer）
- **内容**: リリィが走る地面
- **速度係数**: 1.5（一番速い）
- **画像サイズ**: 横幅 2048px 推奨

## Unity での設定手順

### 1. 背景画像の準備

各レイヤーの画像を用意します：
```
Assets/Images/Backgrounds/
├── sky.png          (遠景)
├── mountains.png    (中景)
├── trees.png        (近景)
└── ground.png       (地面)
```

**重要**: 画像は左右がシームレスにつながるようにデザインしてください。

### 2. 各レイヤーのセットアップ

#### 2.1. 空（Sky）レイヤー
1. Hierarchy で `Create > 2D Object > Sprite` または `Create > UI > Image`
2. 名前を `Sky` に変更
3. Inspector で画像を `sky.png` に設定
4. **同じ画像を使って `Sky2` を作成し、`Sky` の右側にぴったり配置**

#### 2.2. 中景（Mountains）レイヤー
1. `Create > 2D Object > Sprite`
2. 名前を `Mountains` に変更
3. 画像を `mountains.png` に設定
4. `Mountains2` を作成して右側に配置
5. Sorting Layer を `Sky` より手前に設定

#### 2.3. 近景（Trees）レイヤー
1. `Create > 2D Object > Sprite`
2. 名前を `Trees` に変更
3. 画像を `trees.png` に設定
4. `Trees2` を作成して右側に配置
5. Sorting Layer を `Mountains` より手前に設定

#### 2.4. 地面（Ground）レイヤー
1. `Create > 2D Object > Sprite`
2. 名前を `Ground` に変更
3. 画像を `ground.png` に設定
4. `Ground2` を作成して右側に配置
5. Sorting Layer を一番手前に設定

### 3. ParallaxScroller の設定

1. 空の GameObject を作成: `Create > Create Empty`
2. 名前を `ParallaxBackground` に変更
3. `ParallaxScroller` スクリプトをアタッチ
4. Inspector で以下のように設定:

```
ParallaxScroller
├─ Is Scrolling: ☑ (チェック)
├─ Base Speed: 2.0
└─ Layers (配列サイズ: 4)
    ├─ Element 0 (Sky)
    │   ├─ Layer Transform: Sky (ドラッグ&ドロップ)
    │   ├─ Speed Multiplier: 0.2
    │   └─ Loop Width: 20
    ├─ Element 1 (Mountains)
    │   ├─ Layer Transform: Mountains
    │   ├─ Speed Multiplier: 0.5
    │   └─ Loop Width: 20
    ├─ Element 2 (Trees)
    │   ├─ Layer Transform: Trees
    │   ├─ Speed Multiplier: 1.0
    │   └─ Loop Width: 20
    └─ Element 3 (Ground)
        ├─ Layer Transform: Ground
        ├─ Speed Multiplier: 1.5
        └─ Loop Width: 20
```

### 4. GameManager との連携

`GameManager.cs` で `BackgroundScroller` の代わりに `ParallaxScroller` を使用:

```csharp
[Header("Background")]
public ParallaxScroller parallaxScroller; // 変更

// 敵出現時
if (parallaxScroller != null) parallaxScroller.SetScrolling(false);

// 敵撃破後
if (parallaxScroller != null) parallaxScroller.SetScrolling(true);
```

## 画像作成のコツ

### シームレステクスチャの作り方

1. **Photoshop の場合**:
   - Filter > Other > Offset (50%, 0%)
   - 継ぎ目を Clone Stamp Tool で修正
   - 元に戻して確認

2. **オンラインツール**:
   - [Seamless Texture Checker](https://www.pycheung.com/checker/)

### AI生成を使う場合

プロンプト例:
```
A seamless repeating fantasy forest background,
2D game art style, vibrant colors,
sky layer with clouds and distant mountains,
tileable pattern, 2048x512px
```

## トラブルシューティング

### 問題: 背景が動かない
- `Is Scrolling` がチェックされているか確認
- `Base Speed` が 0 より大きいか確認
- レイヤーの Transform が正しく設定されているか確認

### 問題: 継ぎ目が見える
- 画像が本当にシームレスか確認
- `Loop Width` の値を画像の実際の横幅に合わせる
- 2枚目の配置がぴったり右側にあるか確認

### 問題: 速度がおかしい
- `Speed Multiplier` の値を調整
- 遠景は小さく（0.2〜0.5）、近景は大きく（1.0〜2.0）
