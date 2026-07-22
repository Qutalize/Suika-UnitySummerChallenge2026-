# Suika -UnitySummerChallenge2026-

**ペンギンたちのスイカ割り** — 画面の中の画面を操作する、入れ子構造の3Dスイカ割りゲーム。

Unityサマーチャレンジ2026 応募作品（`#Unityサマーチャレンジ`）。

![メイン](/images/scene.png)

---

## ゲーム内容

目隠しをしたペンギンを操作してスイカを割ります。ステージをクリアするたびに、
ゲーム機の**画面の中**へカメラが潜っていき、その画面の中でまた次のステージを遊ぶ
——という「画面の中の画面」が何重にも入れ子になった構造が特徴です。

ステージが進むほど画面が小さく操作の入力が遅れて伝わるようになり、
「スイカ・仲間のペンギン・ビーチボール・海」を割るのが難しくなっていきます。
![ゲーム](/images/UI_game.png)
![コンソール](/images/UI_console.png)
![入れ子](/images/UI_many_consoles.png)

### 操作

| キー | 動作 |
|---|---|
| **W / S** | 前進 / 後退 |
| **A / D** | その場で左右に向きを変える |
| **Space** | 棒を振り下ろす |

### エンディング（マルチエンド）

失敗の仕方や結果によって結末が分岐し、達成状況はタイトル画面の **ENDINGS** に記録されます。

| 結末 | 条件 |
|---|---|
| WATERMELON PARTY | 全ステージクリア（トゥルーエンド） |
| PENGUIN DOWN | 仲間のペンギンを叩いてしまった |
| WRONG BALL | スイカと間違えてビーチボールを叩いた |
| TIME UP | 制限時間切れ |
| GONE SWIMMING | 海に近づいて泳いで行ってしまった |

タイトル画面には **HOW TO PLAY** / **ENDINGS** / **CREDITS** の各ページがあり、
ENDINGSページでは達成記録のリセットもできます。
![ホーム](/images/UI_home.png)

---

## 技術的な見どころ

- **入れ子スクリーン描画**（`NestedScreens.cs`）
  RenderTextureを多段にチェインし、ゲーム機の画面に「画面の中の画面…」を実3Dで描画。

- **手続き生成の海**（`GerstnerOcean.cs` / `ShoreWash.cs`）
  Gerstner波でメッシュを変形し、波打ち際の泡を表現。
- **環境音の自然化**（`AmbientSeagulls.cs`）
  波の音にカモメの鳴き声を間隔・ピッチ・定位をランダム化して重ねる。
- **実行時生成UI**（`GameHud.cs`）
  タイトル・クリア画面・各サブページ・スイカ柄のタイトルロゴをすべてコードで生成。

制作の全手順（Blenderでのモデル生成〜Unity配線〜不具合修正）は
[DEVELOPMENT.md](DEVELOPMENT.md) にまとめてあります。

---

## クレジット

- Game Design / Art / Program — **Quta**
- ペンギンモデル — Seaeees 様（https://booth.pm/ja/items/2780242）（形状や色を変更して使用）
- パラソル・ゲーム機・スイカ・棒 ： Blenderで自作
- その他：https://assetstore.unity.com/packages/3d/props/exterior/beach-party-fun-props-pack-80918
- ファンファーレ、スイカを食べる音・割る音、カモメの声：https://sounddino.com/ja/effects/
- その他の音源：Claudeが作成
- Made with Unity / Blender / Claude

