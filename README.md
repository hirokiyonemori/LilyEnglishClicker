# LilyEnglishClicker

Unity製 英語学習 × クリッカーゲーム

## Environment
- Unity 6000.2.6f2

## Setup
1. Git clone
2. Unity Hubでプロジェクト追加
3. Open

## Description
リリィと一緒に英単語を学びながら敵を倒していくクリッカーゲームです。

### Features
- 英単語クイズシステム
- 敵とのバトルシステム
- プレイヤーのアニメーション（待機・走行・攻撃）
- 背景スクロール
- ドラクエ風ステータスウィンドウ

## Project Structure
```
LilyEnglishClicker/
├─ Assets/           # ゲームアセット（スクリプト、画像、音声など）
├─ Packages/         # Unity Package Manager の依存関係
└─ ProjectSettings/  # Unity プロジェクト設定
```

## Development
### Unity Editor Settings (重要!)
プロジェクトをGitで管理する場合、以下の設定を必ず行ってください：

1. `Edit > Project Settings > Editor`
2. Version Control: **Visible Meta Files**
3. Asset Serialization: **Force Text**

これらの設定により、Gitでの差分管理が正しく行われます。
