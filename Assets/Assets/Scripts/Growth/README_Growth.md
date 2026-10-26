# LilyEnglishClicker キャラ強化システム

参考スクショ3画面をリリィの世界観に置き換えた実装一式です。

| 参考画面 | Lily版 | スクリプト |
|---|---|---|
| ① パーティ一覧（Lv / 必要Exp / 攻防HP と (+上昇量) / レベルをあげる / 特技） | 仲間一覧 | `PartyListUI` + `CharacterRowUI` |
| ② ショップ（特別な楽譜・姫のお守り・EXP購入・動画で報酬） | 強化ショップ | `ShopUI` + `PartyUpgradeRowUI` |
| ③ キャラ詳細（立ち絵・EXPでレベルアップ・会心拳 Lv22 620%・スキルレベルアップ・下部タブ） | キャラ詳細 | `CharacterDetailUI` |

通貨は「EXP（バトル・英語正解）」「★スター（放置販売）」「💎ジェム」の3種。

## 導入手順

1. `Growth` フォルダを `Assets/Scripts/` にコピー（TextMeshPro 必須）
2. Project右クリック → Create → LilyEnglish から作成
   - **Character Definition**：リリー、仲間キャラごとに1つ
   - **Skill Definition**：キャラごとの固有スキル → Character の `skill` に設定
   - **Party Upgrade**：ショップの永続強化（例は下）
3. シーンに空オブジェクト `GrowthService` を置き、`GrowthService` をアタッチ → `party` / `upgrades` に登録
4. UIを組んで各UIスクリプトの参照をつなぐ（行・タブ・強化行はプレハブ化）
5. バトル勝利・英語正解時に
   ```csharp
   GrowthService.Instance.AddCurrency(Currency.Exp, 120);
   ```
   スキル発動ダメージは `def.skill.CalcDamage(stats.atk, skillLevel)`

デバッグ：GrowthService の ⋮ メニューに「EXP +100000」「セーブデータ削除」あり。

## ショップ強化の設定例

| アセット | maxLevel | 通貨/価格 | effectsPerLevel |
|---|---|---|---|
| 魔法の単語帳（＝特別な楽譜） | 1（1回だけ） | 💎100 | 全員 ATK+10%, DEF+10% / `lily` だけ ATK+20% |
| リリーのリボン（＝姫のお守り） | 20 | 💎3, 上昇率1.5 | 全員 DEF+3% |

`targetCharacterId` を空にすると全員、IDを入れるとそのキャラだけに効きます。

## 初期値の成長カーブ（リリー既定値）

| Lv | HP | ATK | DEF | 次Lvの+ATK | 必要EXP | スキル倍率 |
|---|---|---|---|---|---|---|
| 1 | 120 | 20 | 15 | +4 | 100 | 200% |
| 10 | 298 | 68 | 51 | +6 | 200 | 380% |
| 30 | 1,042 | 266 | 199 | +13 | 932 | 780% |
| 50 | 2,266 | 592 | 444 | +20 | 4,343 | 1,180% |
| 100 | 7,426 | 1,968 | 1,476 | +36 | 203,682 | (Lv50上限) |

- 必要EXPは参考ゲームと同じく毎Lv約8%増（`expGrowthRate`）
- スキル倍率は 200% + 20%/Lv で参考の「Lv22＝620%」と一致
- キャラごとに `StatGrowth` を変えると「攻撃型」「防御型」の差が出ます

## Codex に渡すプロンプト例

```
Assets/Scripts/Growth 以下の強化システムを既存プロジェクトに統合してください。
1. 既存のバトル勝利処理・英語問題の正解処理から
   GrowthService.Instance.AddCurrency(Currency.Exp, 獲得量) を呼ぶ
2. 既存のプレイヤーステータス参照を GrowthService.Instance.GetStats(def) に置き換える
3. スキル攻撃は英語問題に正解したときだけ def.skill.CalcDamage() で計算する
4. ホーム画面に「仲間」「ショップ」ボタンを追加し、PartyListUI / ShopUI を開く
既存の機能は壊さず、変更点をPRの説明に一覧で書いてください。
```

## 次に足すと良いもの

- 「レベルをあげる」長押しで連続アップ（`LevelUpMax` は実装済み、ボタン側の長押し判定だけ）
- 実際の広告SDK：`IRewardedAdService` を実装して `SetAdService()` で差し替え
