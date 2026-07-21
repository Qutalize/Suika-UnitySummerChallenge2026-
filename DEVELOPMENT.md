# Suika -UnitySummerChallenge2026- 開発ノート

「ペンギンたちのスイカ割り」の制作記録です。
Blenderでのモデル生成からUnityでの配線、テストプレイでの不具合修正、
エンディング実装までの手順を1本にまとめています。
（元は `README_手順書.md` / `improve.md` / `ending_setup.md` の3ファイルでした）

> 応募締切：2026年7月24日 ／ Unityサマーチャレンジ2026

## 目次

- [Part 1. 制作手順（Blender & Unity）](#part-1-制作手順blender--unity)
- [Part 2. テストプレイ後の修正記録](#part-2-テストプレイ後の修正記録)
- [Part 3. エンディング実装手順](#part-3-エンディング実装手順)

---

# Part 1. 制作手順（Blender & Unity）

## Unityサマーチャレンジ2026 制作手順書（v4・3Dゲーム版）
### 「ペンギンたちのスイカ割り」— 画面の中の画面を操作する入れ子3Dゲーム

> **応募締切：2026年7月24日**（§9）

---

### v4での変更点（v3から読み直す場合はここだけ確認）

実装を進める中で判明した、**v3の記述が誤っていた箇所**と**追加した機能**です。

#### 訂正（v3の通りに作ると動かない／不自然になる箇所）

| 箇所 | v3の記述 | 正しくは |
|---|---|---|
| §1 FBX名 | `penguin` / `penguin_blind` | **`penguin_slim` / `penguin_slim_sunglasses`**。旧名のファイルは出力されません |
| §2 マテリアル | SandMat等を「調整」と記載 | **自分で新規作成**するマテリアルです（§2-3〜2-5） |
| §5-1 ゲーム機の位置 | `(0, 0.42, -0.30)` | Z がマイナス＝**背中側**でした。前（プラス）へ（§5-2b） |
| §5-2 棒の位置 | `(0, 0, 0)` | メッシュ原点が**棒の中央**にあるため、見た目で判断が必要（§5-2手順2） |
| §5-3 カメラ | 「Depth を -10」 | URPでは **`Priority`**（Output セクション内）（§5-3①） |
| §5-3 ScreenFocus | 「+Z が画面から外へ」 | **逆**。+Z は画面の**奥**へ向けます（§5-3④） |
| §5-1 スイカ | `Y = 0` | メッシュ原点が球の中心のため **Y = 0.14** 前後（§5-1補足） |
| §2-1 入力設定 | 記載なし | **Active Input Handling を `Both`** にしないと Play した瞬間に例外で止まります（§2-1手順3 / §6-5冒頭） |

#### 追加した機能

| 内容 | 理由 | 参照 |
|---|---|---|
| **羽（フリッパー）の分離** | 元モデルにボーンが無く、羽が動かないとゲーム機が宙に浮き、棒を手を固定したまま振ることになるため | §1-3 / §5-1b |
| **振り判定を連続化**（`PenguinPlayerController`） | 振り下ろし終了の1フレームだけ判定していたため、通過中の命中を取りこぼしていた | §5-2手順5 |
| **`StickDebugGizmo.cs`** | 当たり判定が見えないと位置合わせができないため | §5-2手順4 |

#### 未解決の課題（§12にまとめ）

- ⚠️ **ペンギンが Unity +Z を向いていない（発生確認済み・最優先）**
  — 棒が横薙ぎになる／前進キーで横に歩く。**他の微調整より先に直してください**（§12-2）
- `PenguinIdleBob` が演出アニメと競合し、ジャンプが見えない（§12-1）
- 羽の切り出ししきい値は**未検証**（実行して調整が必要）（§12-3）

#### 構造の変更（実機で確定した内容）

棒は **StickRoot を1階層挟む**構造に変更しました。
棒のメッシュ原点が中央にある問題を StickRoot に専任させることで、
FBX側のオフセットの有無に左右されなくなります（§5-2）。

```
StickPivot → StickRoot(0,0,0.26) → stick(0,0,0 / 90,0,0) → StickTip(0,0.26,0)
```

---

### 0. 画面遷移フロー（絵コンテ①〜⑩対応）

```
① タイトル [はじめる]
        ↓
② 導入アニメ（4匹がスイカ割りを楽しむ / 1匹はパラソルでゲーム中）
        ↓
   ステージ1（通常の3D俯瞰視点でプレイ）
        ↓ クリア
③ ゲーム機の画面へズームインするアニメ → ステージ2
        ↓ クリア
④⑤ ステージ3〜5：クリアごとにさらに画面の中へズームイン。
    プレイ画面の周囲には前ステージの風景（パラソル・砂浜）が実3Dで残る
        ↓ ステージ5クリア
⑥ すべての画面からゆっくりズームアウト（ステージ1の視点まで戻る）
        ↓
⑦ 4匹に呼ばれてスイカ割りに参加するエンディングアニメ
        ↓
⑧ クリア画面 [クレジット] [タイトルへ]
        ↓
⑨(=①) タイトル
```
- **ゲームオーバー（他のペンギンを叩く／10秒経過）は、どのステージでも①タイトルへ戻る**
- ⑩ ステージセレクトは未定枠 → §10「今後の拡張」参照

#### 入れ子の仕組み（3D実装）
2DのベゼルUIではなく、**実際の3Dワールドのゲーム機画面**で入れ子を作ります。
`NestedScreens.cs` がカメラを連鎖させ、
「プレイ映像 → それが映った画面を見た映像 → さらにそれが映った画面…」を
RenderTextureで段階的に生成。最前面のカメラは本物の3D空間でゲーム機を映すため、
画面の周囲にパラソルや砂浜がそのまま残ります（コンテ④の指示通り）。
各階層が1フレームずつ遅れる点も、入力遅延の演出と自然に噛み合います。

#### 同梱ファイル
```
unity-summer/
├── README_手順書.md
├── blender/build_beach_models.py   … 全モデル生成＋FBX一括出力＋羽の分離
├── textures/watermelon_skin.png, watermelon_inside.png
├── audio/ ocean_loop / swing / crack / hit_fail / clear / gameover / cheer .wav
└── unity/Scripts/
    ├── SuikawariGame.cs      … 進行管理（①〜⑨の遷移すべて）
    │                            ※当たり判定は距離のみ（Collider不使用）
    ├── NestedScreens.cs      … 3D入れ子レンダラー（カメラ連鎖）
    ├── GameHud.cs            … タイトル/クリア画面/HUD/フェード（自動生成）
    ├── PenguinPlayerController.cs … 遅延入力つき操作
    ├── EndingDirector.cs     … ⑦合流アニメ
    ├── GerstnerOcean.cs      … 動的な海
    ├── PenguinIdleBob.cs     … 待機モーション
    ├── CameraSway.cs         … 手持ち風カメラ揺れ（3D感の強化）
    └── StickDebugGizmo.cs    … 棒の当たり判定を可視化（開発中のみ・§5-2手順4）
```

#### 全体工程（目安）
| 日 | 作業 | 詰まりやすい所 |
|---|---|---|
| 1日目 | Blenderでモデル生成・FBX出力（§1） | **羽の切り出ししきい値の調整**（§1-3） |
| 2日目 | Unityセットアップ、マテリアル作成、砂浜・海・山（§2〜4） | 新規マテリアルの作成（§2-5） |
| 3日目 | シーン配置・羽と棒の接続・カメラ定位置（§5） | **ペンギンの向き確認**（§5-1b）→ ここを飛ばすと後で全部やり直し |
| 4日目 | ゲーム配線・遷移確認・難易度調整（§6） | 参照の割り当て漏れ（§6-2） |
| 5日目 | ライティング・サウンド仕上げ（§7〜8）、撮影・投稿（§9） | 録画時のフレーム落ち（§9-2） |

> **時間が足りない場合の優先順位**：
> ①ステージ1が遊べる → ②入れ子（ステージ2〜3）が映る → ③エンディング → ④羽の動き。
> 入れ子表現がこの作品の核なので、②までは必ず確保してください。
> 羽が動かなくても静止画・短い動画なら成立します（§12-1に暫定策）。

---

### 1. Blender：モデリング（スクリプト一括生成）

対応バージョン：**Blender 3.6〜4.x**

#### 1-1. 実行の準備

このスクリプトは、**外部から入手したペンギンモデル（`penguin.fbx`）を読み込んで加工**します。
完全にゼロから生成するわけではないので、元モデルが必要です。

1. `penguin.fbx` を任意のフォルダに置く
2. **同じフォルダ**に Blender の新規ファイルを保存
   （例 `C:\unity-summer\beach_models.blend`）
   - スクリプトは blend ファイルの場所を基準に `penguin.fbx` を探し、
     同じ場所の `export/` へ出力します
3. `build_beach_models.py` 冒頭の **`TEX_DIR`** を、
   Unityプロジェクトの `Assets/Textures` の絶対パスに書き換え
4. [Scripting]タブ → 新規 → 貼り付け → 実行(Alt+P)

#### 1-2. 出力されるFBX（7個）

| ファイル | 中身 | 使う場所 |
|---|---|---|
| `penguin_slim.fbx` | 痩身化した素のペンギン | **PlayerPenguin**（1体） |
| `penguin_slim_sunglasses.fbx` | 痩身化 + 金縁サングラス | **NPC_A〜D + GamerPenguin**（5体） |
| `watermelon.fbx` | スイカ（丸ごと） | Melon |
| `watermelon_halves.fbx` | 割れたスイカ（**2オブジェクト入り**） | MelonHalves |
| `parasol.fbx` | ビーチパラソル | Parasol |
| `console.fbx` | 携帯ゲーム機（画面 = `ScreenMat`） | Console |
| `stick.fbx` | スイカ割りの棒 | stick |

> ⚠️ 旧版に書かれていた `penguin` / `penguin_blind` というファイルは**出力されません**。
> サングラスが付くのは**観客側**で、プレイヤーに目隠しメッシュはありません。

#### 1-3. 実行後に必ず確認すること

**Window → Toggle System Console** でログを開き、次の2点を確認します。

**① くちばしの黄色化**
```
→ くちばし先端から深さ0.075(全高比)以内の◯面を 'BeakYellow' に割り当て
```
`[注意] くちばし付近のポリゴンが見つかりませんでした` と出た場合は、
`BEAK_REGION_DEPTH` を大きく（例 0.12）して再実行。

**② 羽（フリッパー）の切り出し** ← v4で追加
```
--- 羽の切り出し（スリム版）---
  全高=1.023 左右半幅=0.187 しきい値=0.116
  高さ帯=0.307〜0.798 (全2480面)
  右羽=124面 / 左羽=118面
  Flipper_R の肩(原点) = (0.142, -0.031, 0.702)
```

| ログ | 意味 | 対処 |
|---|---|---|
| `右羽=0面 / 左羽=0面` | 羽を検出できていない | `FLIPPER_SIDE_RATIO` を **0.5** に下げる |
| `[注意] 選択が多すぎます` | 胴体を巻き込んでいる | `FLIPPER_SIDE_RATIO` を **0.75** に上げる |
| 頭やサングラスが切れた | 高さ帯が広すぎ | `FLIPPER_Z_MAX` を **0.70** に下げる |
| 羽の下半分しか切れない | 高さ帯が狭い | `FLIPPER_Z_MIN` を **0.20** に下げる |

**左右の羽の面数が大きく違う場合**（例 124面 / 12面）は、
モデルが左右非対称か、中心線の検出がずれています。
Blenderのビューポートで `Flipper_R` `Flipper_L` を選んで、
実際に羽だけが選択されているか目で確認してください。

> **羽の分離が不要な場合**は `SPLIT_FLIPPERS = False` にすると、
> 従来通り1枚メッシュで出力されます（羽は動かせなくなります）。

#### 1-4. 調整用パラメータ一覧

スクリプト冒頭の `# 調整パラメータ` ブロックで変更できます。

| パラメータ | 既定値 | 意味 |
|---|---|---|
| `TEX_DIR` | — | テクスチャの絶対パス（**要変更**） |
| `FBX_NAME` | `penguin.fbx` | 元モデルのファイル名 |
| `SLIM_LR` / `SLIM_FB` | 0.84 / 0.90 | 痩身化の度合い（左右 / 前後） |
| `FRONT_OVERRIDE` | `+X` | くちばしの向く軸。`AUTO` で自動判定 |
| `BEAK_REGION_DEPTH` | 0.075 | くちばしとみなす深さ（全高比） |
| **`SPLIT_FLIPPERS`** | True | 羽を分離するか |
| **`FLIPPER_SIDE_RATIO`** | 0.62 | 羽とみなす左右の張り出し（半幅比） |
| **`FLIPPER_Z_MIN/MAX`** | 0.30 / 0.78 | 羽とみなす高さ帯（全高比） |
| **`FLIPPER_PIVOT_DROP`** | 0.08 | 肩を羽の上端からどれだけ下げるか |
| **`PENGUIN_BAKE_TRANSFORM`** | False | 親子構造がある場合の Apply Transform。Unityで倒れて見えるときだけ True |

> **重要（3D入れ子用）**：ゲーム機の画面は `ScreenMat` マテリアルとして本体メッシュに
> 統合されています。`NestedScreens` はマテリアル名で自動検出するのでそのまま使えます。

> **重要（羽の分離）**：ペンギンFBXには **`Flipper_R` / `Flipper_L`** が
> 子オブジェクトとして含まれます。元モデルにボーンが無いため、
> 羽のポリゴンを切り出して**肩を原点とする剛体**にしたものです。
> Unity側でこれを回転させると羽が動きます（§5-1b）。
>
> 実行後は **Window → Toggle System Console** でログを開き、
> `右羽=◯面 / 左羽=◯面` が妥当か確認してください。0面や極端に多い場合は
> スクリプト冒頭の `FLIPPER_SIDE_RATIO` / `FLIPPER_Z_MIN` / `FLIPPER_Z_MAX` を
> 調整して再実行します。

寸法・色を変えたい場合はスクリプト内の数値を編集して再実行。

---

### 2. Unity：プロジェクト作成とインポート

#### 2-1. プロジェクトを作る

1. Unity Hub → New Project → テンプレートから **Universal 3D (URP)** を選択
   （Unity 2022 LTS または Unity 6）
2. プロジェクト名と保存先を決めて Create Project
3. **入力システムの設定を先に済ませておく**（後で必ず引っかかります）
   - **Edit → Project Settings → Player → Other Settings → Active Input Handling**
   - **`Both`** に変更 → **Editor を再起動**
   - このプロジェクトのスクリプトは旧 `UnityEngine.Input` API で書かれているため、
     新 Input System のみの設定だと Play した瞬間に例外が出て何も動きません（§6-5）

#### 2-2. フォルダを作ってファイルを入れる

Project ウィンドウ（画面下部）の `Assets` を右クリック → `Create → Folder` で
次の5つのフォルダを作ります。

```
Assets/
├── Models      … FBX を入れる
├── Textures    … png を入れる
├── Audio       … wav を入れる
├── Scripts     … cs を入れる
└── Materials   … マテリアルを入れる（このあと作ります）
```

**入れ方**：エクスプローラーから各ファイルを Project ウィンドウのフォルダへ
**ドラッグ＆ドロップ**するだけです（コピーされます）。

- `blender/export/*.fbx`（7個） → `Assets/Models`
- `textures/*.png`（2個） → `Assets/Textures`
- `audio/*.wav`（7個） → `Assets/Audio`
- `unity/Scripts/*.cs`（8個） → `Assets/Scripts`

`ocean_loop.wav` だけ設定を変えます：
`Assets/Audio/ocean_loop` を選択 → Inspector → **Load Type: Streaming** → Apply
（長いループ音をメモリに全部載せないための設定です）

---

#### 2-3. マテリアルとは（ここが分かりにくいところ）

**マテリアル = 「この面はどう見えるか」を決める設定ファイル**です。
色・ツヤ・貼り付ける画像などをひとまとめにしたもので、
`Assets/Materials/` の中に `.mat` ファイルとして置かれます。

このプロジェクトで扱うマテリアルは、出どころが **2種類**あります。

| 種類 | 何か | どうやって手に入れる |
|---|---|---|
| **A. FBX に付いてきたもの**<br>（MelonSkin / ScreenMat など） | Blender で設定した見た目が FBX の中に埋め込まれている | **Extract Materials** で取り出す（§2-4） |
| **B. 自分で作るもの**<br>（SandMat / OceanMat / MountainMat） | 砂浜・海・山は Unity 側で作るオブジェクトなので、FBX には入っていない | **右クリック → Create → Material** で新規作成（§2-5） |

> **「SandMat が見つからない」のは正常です。** SandMat / OceanMat / MountainMat は
> どこかに存在するものを探すのではなく、**これから自分で作る**マテリアルです。
> §1の表で「（新規）」と書いてあったのがその意味でした。

---

#### 2-4. A：FBX からマテリアルを取り出す（Extract Materials）

FBX をインポートしただけの状態では、マテリアルは FBX ファイルの**内部に埋め込まれていて
編集できません**（Inspector がグレーアウトして触れない）。
これを独立した `.mat` ファイルとして外に出す操作が **Extract Materials** です。

**手順（FBX 7個すべてに対して行います）**

1. Project ウィンドウで `Assets/Models/penguin` を**クリックして選択**
2. Inspector 上部のタブから **Materials** を選ぶ
   （タブは `Model` / `Rig` / `Animation` / `Materials` の4つ）
3. Material Creation Mode が `Standard` になっていることを確認
4. 下の方にある **`Extract Materials...`** ボタンをクリック
5. フォルダ選択ダイアログが出るので **`Assets/Materials`** を選んで OK
6. `Assets/Materials/` に `.mat` ファイルが生成されます

7個の FBX すべてに繰り返すと、`Assets/Materials/` は例えばこうなります：

```
Assets/Materials/
├── PenguinBody.mat
├── PenguinBelly.mat
├── MelonSkin.mat
├── MelonInside.mat
├── ScreenMat.mat      ← 入れ子表現の要
├── ConsoleBody.mat
├── ParasolCloth.mat
└── StickMat.mat
```

> 実際の名前は `build_beach_models.py` の設定によります。
> **`ScreenMat` という名前を含むマテリアルが1つある**ことだけ必ず確認してください。
> `NestedScreens.cs` はこの名前で画面を探します（部分一致なので
> `ScreenMat 1` のような名前でも動きます）。

**もしピンク色になったら**：URP ではない古いシェーダーのままです。
`Assets/Materials` のマテリアルを全選択 →
`Edit → Rendering → Materials → Convert Selected Built-in Materials to URP`

---

#### 2-5. B：新しいマテリアルを作る（SandMat など）

**基本の手順（3つとも同じ流れです）**

1. Project ウィンドウで **`Assets/Materials` フォルダを開く**
2. 何もないところで**右クリック → `Create → Material`**
3. 名前を入力（例：`SandMat`）して Enter
   ※ 作った直後は名前入力状態になっています。間違えたら F2 で付け直せます
4. 作った `SandMat` を**クリックして選択**すると、Inspector に設定項目が出ます
5. Inspector 最上部の **Shader** が `Universal Render Pipeline/Lit` に
   なっているか確認（URP プロジェクトなら既定でこれです）
6. 下の各項目を設定する

**色の変え方**：Inspector の `Surface Inputs → Base Map` の
**右側にある白い四角**をクリックするとカラーピッカーが開きます。
その一番下の **Hexadecimal** 欄に `E8D5A3` のように入力すれば正確な色になります。
（左側の四角はテクスチャ画像を入れる場所で、色とは別物です）

##### ① SandMat（砂浜）

| 項目 | 値 |
|---|---|
| Shader | Universal Render Pipeline/Lit |
| Base Map の色 | `#E8D5A3`（薄い黄土色） |
| Metallic Map | **0** |
| Smoothness | **0.1**（砂はほぼ反射しません） |

##### ② OceanMat（海）

海は**半透明**にするので、他より手順が1つ多いです。

| 項目 | 値 |
|---|---|
| Shader | Universal Render Pipeline/Lit |
| **Surface Type** | **Transparent** ← 先にこれを変える |
| Blending Mode | Alpha |
| Base Map の色 | `#2E9EC4`、**アルファ(A) を 217**（≒0.85） |
| Metallic Map | 0 |
| Smoothness | **0.9**（水面はよく反射します） |

> **アルファの入れ方**：カラーピッカーの Hexadecimal 欄に
> `2E9EC4DD` と8桁で入れると、最後の2桁がアルファになります。
> または `A` のスライダーを 217 に合わせます。
> Surface Type を Transparent にしていないと、アルファを下げても透けません。

##### ③ MountainMat（遠景の山）

| 項目 | 値 |
|---|---|
| Shader | Universal Render Pipeline/Lit |
| Base Map の色 | `#7E9187`（灰緑） |
| Metallic Map | 0 |
| Smoothness | **0.05**（遠景なのでツヤは不要） |

##### ④ SnowMat（山頂の雪冠・§3-2で使います）

| 項目 | 値 |
|---|---|
| Shader | Universal Render Pipeline/Lit |
| Base Map の色 | `#F2F5F7` |
| Smoothness | 0.2 |

---

#### 2-6. 取り出したマテリアルの微調整

§2-4 で取り出したマテリアルのうち、2つだけ確認・調整します。

| マテリアル | やること |
|---|---|
| **MelonSkin** | Base Map の**左の四角**に `Assets/Textures/watermelon_skin.png` をドラッグ。Smoothness **0.55**（スイカの皮のツヤ） |
| **MelonInside** | Base Map に `watermelon_inside.png` をドラッグ。Smoothness 0.3 |
| **ScreenMat** | **何も触らないでOK**。実行時に `NestedScreens.cs` が RenderTexture と発光設定を書き込みます |

> **ScreenMat を手動で光らせないでください。** Emission を先に設定しても、
> `NestedScreens.Awake()` が `_EmissionColor` を上書きします（明るさは
> NestedScreens の **Screen Brightness** で調整します。§6-1）。

---

#### 2-7. マテリアルをオブジェクトに割り当てる方法

作ったマテリアルは、**オブジェクトに割り当てて初めて見た目に反映されます**。
§3以降で何度も出てくる操作なので、ここで覚えてください。

**方法1（一番簡単）**：Project ウィンドウのマテリアルを、
**Scene ビューのオブジェクトの上に直接ドラッグ＆ドロップ**

**方法2（確実）**：
1. Hierarchy でオブジェクトを選択
2. Inspector の **Mesh Renderer → Materials → Element 0** の欄へ
   Project ウィンドウからマテリアルをドラッグ

**確認**：割り当たると Inspector の一番下にマテリアルのプレビューが表示されます。
そこが白いままなら割り当てに失敗しています。

---

#### 2-8. マテリアル早見表（作業後の答え合わせ用）

ここまで終わると、`Assets/Materials/` の中身と用途はこうなっているはずです。

| マテリアル | 出どころ | 設定 | 使う場所 |
|---|---|---|---|
| MelonSkin | FBX から抽出 | Base Map = watermelon_skin.png、Smoothness 0.55 | Melon（自動） |
| MelonInside | FBX から抽出 | Base Map = watermelon_inside.png | MelonHalfL/R（自動） |
| ScreenMat | FBX から抽出 | **そのまま**（実行時に NestedScreens が設定） | Console の画面（自動） |
| その他 FBX 由来 | FBX から抽出 | そのまま | ペンギン・パラソル等（自動） |
| **SandMat** | **自分で作る** | URP/Lit、`#E8D5A3`、Smoothness 0.1 | Sand（§3-1で手動割当） |
| **OceanMat** | **自分で作る** | URP/Lit、**Transparent**、`#2E9EC4` α0.85、Smoothness 0.9 | Ocean（§4-1で手動割当） |
| **MountainMat** | **自分で作る** | URP/Lit、`#7E9187` | Mountain_A/B（§3-2で手動割当） |
| **SnowMat** | **自分で作る** | URP/Lit、`#F2F5F7` | Snow_A/B（§3-2で手動割当） |

「自動」＝ FBX を Hierarchy にドラッグした時点で既に割り当たっているもの。
「手動割当」＝ §2-7 の方法で自分で割り当てるもの（＝新規作成した4つ）。

---

### 3. 砂浜と山の作成

ここからは**新規シーンを1つ作って**（`Assets/Scenes/Beach.unity` として保存）
そこにすべてを組み立てていきます。以降の座標はすべて **ワールド座標** です
（「◯◯の子」と書いてある場合のみローカル座標）。

> 座標系の約束：**+Z が「海の方向（奥）」、+X が右、+Y が上**。
> プレイヤーとスイカは Z＝1〜5 のあたり、海は Z＝45、山は Z＝78〜85 の遠景です。

#### 3-1. 砂浜（Sand）

1. Hierarchy で右クリック → `3D Object → Plane`、名前を **Sand** に変更
2. Inspector の Transform を設定
   - Position **(0, 0, 0)**
   - Rotation **(0, 0, 0)**
   - Scale **(8, 1, 8)** … Unity の Plane は 1 Unit = 10m なので **80m×80m** になります
3. §2-5① で作った **SandMat** を割り当てる（§2-7 の方法）
   - Project の `Assets/Materials/SandMat` を Scene ビューの Sand へドラッグ、または
     Inspector → Mesh Renderer → Materials → Element 0 へドラッグ
4. 砂の粒感が欲しければ、SandMat の Base Map の**左の四角**に薄いノイズ画像を入れて
   Tiling を **(20, 20)** にすると、寄りの画でものっぺりしません（任意）

> **なぜ 80m も必要？** ステージ2以降はゲーム機画面のドアップになりますが、
> ⑥のズームアウトで一気に全景まで引きます。そのとき砂浜の端が見えると
> 「板が浮いている」ように見えてしまうため、海と山まで届く広さにしておきます。

#### 3-2. 山（遠景・コンテの構図に必須）

Unity の標準メニューには Cone がありません。次のどれかで用意します。

- **方法A（推奨・最速）**：Package Manager → **ProBuilder** をインストール →
  `Tools → ProBuilder → ProBuilder Window` → `New Shape` → Cone を配置
- **方法B**：Blender スクリプトの末尾に円錐を追加して FBX 出力
- **方法C（Cone なしでも可）**：Cube を Rotation Y 45°・Scale を縦長にして
  「岩山」として使う。遠景なので意外と成立します

配置（空オブジェクト **Mountains** を作り、その子にまとめると管理が楽です）

| 名前 | Position | Scale | Rotation | マテリアル |
|---|---|---|---|---|
| Mountain_A | (-18, 0, 78) | (22, 16, 22) | (0, 0, 0) | MountainMat |
| Mountain_B | (12, 0, 85) | (28, 20, 28) | (0, 20, 0) | MountainMat |
| Snow_A | (-18, 12, 78) | (7, 4, 7) | (0, 0, 0) | SnowMat |
| Snow_B | (12, 15, 85) | (9, 5, 9) | (0, 20, 0) | SnowMat |

※ MountainMat / SnowMat は §2-5③④ で作ったものです。割り当ては §2-7 の方法で。

- 山の**足元が海の水平線より下**に来るよう Y を調整します。Scene ビューを
  ViewCamera（§5）の位置から覗いて、山の裾が海に隠れる高さが正解です
- 雪冠（Snow_A/B）は同じ Cone を小さくして山頂に載せ、SnowMat を割り当てるだけ
- **チェック**：Mountains 全体を選択して非アクティブ↔アクティブを切り替え、
  「あるとないとで夏の浜辺らしさが変わるか」を見比べてください

#### 3-3. パフォーマンスの下準備

遠景の山と砂浜は動かないので、**Static** にしておくとライティングとバッチングが効きます。

1. Sand / Mountains を選択
2. Inspector 右上の **Static** チェックボックスをオン（全項目）
3. ダイアログが出たら「Yes, change children」

> **入れ子ゲームでは描画コストが階層数ぶん増えます**（ステージ5 = カメラ4台）。
> Static 化とマテリアル数の削減はここで効いてきます。

---

### 4. 動的な海の作成

`GerstnerOcean.cs` は **メッシュを実行時に自動生成**します。
Plane を置くのではなく、空オブジェクトにスクリプトを付けるだけです。

#### 4-1. オブジェクトの作成

1. Hierarchy 右クリック → `Create Empty`、名前を **Ocean** に変更
2. Transform → Position **(0, 0.12, 45)**、Rotation (0,0,0)、Scale (1,1,1)
   - Y=0.12 は「砂浜より少し高い水面」。あとで波打ち際を見ながら微調整します
3. Inspector → `Add Component` → **GerstnerOcean**
   - `[RequireComponent]` が付いているので **MeshFilter と MeshRenderer が自動で追加**されます
4. Mesh Renderer → Materials → Element 0 に **OceanMat**（§2-5②で作ったもの）を割り当て

#### 4-2. GerstnerOcean のパラメータ

| 項目 | 推奨値 | 意味 |
|---|---|---|
| Size | **80** | 海の一辺(m)。砂浜と同じ幅にすると継ぎ目が目立ちません |
| Resolution | **140**（重ければ 100 以下） | 一辺の分割数。頂点数は (N+1)² なので 140 → 約2万頂点 |
| Time Scale | **1** | 全体の速度倍率。0.7 くらいにすると「凪いだ夏の海」に |

**Waves 配列（3つの重ね合わせが既定値。穏やかな夏の海にはこのままでOK）**

| # | Direction | Steepness | Wavelength | 役割 |
|---|---|---|---|---|
| 0 | (1, 0.2) | 0.22 | 11 | 大きなうねり |
| 1 | (0.7, 1) | 0.15 | 5.5 | 中波 |
| 2 | (-0.4, 0.8) | 0.10 | 2.8 | さざ波 |

調整の指針：
- **Steepness は 0.10〜0.25 に収める**。0.3 を超えると波が自分自身を突き抜けて
  トゲトゲになります（ゲルストナー波の性質）
- **波長は 3〜12m**。合計の Steepness（3つの和）が 0.5 を超えると荒れた海に見えます
- Direction は正規化されるので長さは気にせず、**向きの比率だけ**指定すればOK
- 波の速さは波長から物理的に決まります（位相速度 c = √(9.81/k)）。
  「遅くしたい」ときは Time Scale を下げてください

#### 4-3. 波打ち際の合わせ込み

海と砂浜が交差する線が「波打ち際」になります。**再生中に**合わせるのがコツです。

1. Play を押す（メッシュは Awake で生成されるので、停止中は板のままです）
2. Scene ビューに切り替え、Ocean を選択
3. **Z を 40〜50 の範囲**で動かし、砂浜との交差線が画面の奥 1/3 に来る位置を探す
4. **Y を 0.05〜0.20 の範囲**で動かし、波が砂を薄く洗う高さに合わせる
   - Y が高すぎる → 砂浜が水没
   - Y が低すぎる → 海と砂の間に隙間ができて地面が透ける
5. 気に入った値を **メモしてから Play を止め**、停止中の Transform に入力し直す
   （再生中の変更は保存されません）

> 交差線が直線的すぎて気になる場合は、Sand を少しだけ回転（Rotation Y 3°）させるか、
> Ocean の Rotation Y を 5° ずらすと、波の向きと海岸線が平行でなくなり自然になります。

#### 4-4. 波の音

1. Ocean を選択 → `Add Component` → **Audio Source**
2. 設定
   - AudioClip: **ocean_loop**
   - Play On Awake: **オン**
   - Loop: **オン**
   - Volume: **0.5**
   - Spatial Blend: **0.6**（3D寄り。引きの画で遠鳴り、寄りで近く聞こえます）
   - 3D Sound Settings → Volume Rolloff: **Linear Rolloff**、
     Min Distance **10** / Max Distance **120**
3. **Audio Listener は ViewCamera ではなく GameplayCamera 側に置きます**（§5-3参照）。
   海の音量は「プレイ中のカメラ位置」を基準に聞こえる、と覚えてください

---

### 5. シーン配置

#### 5-1. オブジェクト配置

FBX は `Assets/Models` から Hierarchy へドラッグすると配置できます。
名前は必ず下表の通りにしてください（後の割り当てで迷わなくなります）。

FBX は `Assets/Models` から Hierarchy へドラッグすると配置できます。
名前は必ず下表の通りにしてください（後の割り当てで迷わなくなります）。

> **FBX の名前について（重要）**：`build_beach_models.py` が実際に出力するのは
> **`penguin_slim.fbx`** と **`penguin_slim_sunglasses.fbx`** の2種類です。
> 旧版に書いてあった `penguin` / `penguin_blind` というファイルは存在しません。
>
> | FBX | 中身 | 使う場所 |
> |---|---|---|
> | `penguin_slim` | 痩身化しただけの素のペンギン（1匹用） | **PlayerPenguin** |
> | `penguin_slim_sunglasses` | 痩身化 + 金縁サングラス（5体用） | **NPC_A〜D と GamerPenguin** |
>
> サングラスが付いているのは**観客側**です。プレイヤーは「目隠し」のメッシュを
> 持たないので、目隠し感を出したい場合は §5-1補足の方法で布を足してください。

| オブジェクト | 元FBX | Position | Rotation Y | 備考 |
|---|---|---|---|---|
| Melon | watermelon | (0, 0, 4) | 0 | スイカ本体。Y は §5-1補足で実測調整 |
| MelonHalves | watermelon_halves | (0, 0, 4) | 0 | 中に `melon_half_L` `melon_half_R` の2つの子が入っています |
| PlayerPenguin | **penguin_slim** | (0, 0, 1.5) | 0 | 操作キャラ |
| NPC_A | **penguin_slim_sunglasses** | (-1.6, 0, 3.2) | 60 | `PenguinIdleBob` を付ける |
| NPC_B | **penguin_slim_sunglasses** | (1.6, 0, 3.4) | -60 | 同上 |
| NPC_C | **penguin_slim_sunglasses** | (-1.0, 0, 5.0) | 160 | 同上 |
| NPC_D | **penguin_slim_sunglasses** | (1.2, 0, 4.9) | -160 | 同上 |
| Parasol | parasol | (-6, 0, 1.5) | 0 | Rotation Z を 8° 傾けると◎ |
| GamerPenguin | **penguin_slim_sunglasses** | (-5.5, 0, 1.2) | 75 | `PenguinIdleBob` を付ける |
| Console | console | **ConsoleAnchor の子**（§5-2b） | — | 手に持たせます |
| JoinPoint | Create Empty | (0, 0, 2.6) | — | ⑦の合流位置。見た目なし |

補足：

- **MelonHalves は1回だけドラッグ**します。`watermelon_halves.fbx` には
  `melon_half_L` と `melon_half_R` の**2オブジェクトが入っている**ので、
  Hierarchy を展開すると子が2つぶら下がります。
  `SuikawariGame` の Melon Half L / R には、**この2つの子**を割り当ててください。
  そして **子2つを非アクティブ**にします（親は アクティブのままでOK）
- **スイカの高さ（Y）は実測してください**。生成スクリプトではスイカのメッシュ原点が
  **球の中心**（地面から 0.14m）に置かれています。そのため Y=0 のまま置くと
  **半分地面に埋まる**ことがあります。Scene ビューで横から見て、スイカが砂浜に
  ちょこんと載る高さ（おおむね **Y = 0.14**）に調整してください。
  MelonHalves も同じ高さに揃えます
- **NPC の Rotation Y** は「スイカ (0,0,4) の方を向く」ように調整します。
  正確に出したいときは、NPC を選択して Inspector の Rotation Y を動かしながら
  Scene ビューでくちばしの向きを見るのが早いです
- **JoinPoint** は空オブジェクト（Create Empty）。位置だけ使うので見た目は不要ですが、
  Inspector 左上のアイコンドロップダウンで色を付けておくと Scene ビューで見つけやすいです
- **目隠しを足したい場合**（任意）：PlayerPenguin の子に `3D Object → Cube` を作り、
  Scale (0.20, 0.05, 0.16) 程度にして目の位置へ。黒いマテリアルを割り当てれば
  目隠し布になります。サイズはペンギンの実寸に合わせてください

---

#### 5-1b. 羽（フリッパー）は分離済みです（先に読んでください）

棒とゲーム機を持たせる前に、モデルの構造を知っておく必要があります。

元のペンギンモデルには**ボーン（Armature）がありません**。
`build_beach_models.py` の `import_penguin()` がメッシュだけを取り出して
結合しているためです。そのままでは羽が一切動かず、

- 棒を「手を固定したまま」肩から生やして振ることになる
- ゲーム機が胸の前に**浮いた**状態になる

という不自然さが出ます。

そこで生成スクリプトに **羽のポリゴンだけを別オブジェクトへ切り出す処理**
（`separate_flippers()`）を入れてあります。スキニングは使わず、
**羽を「肩を原点とする剛体の子オブジェクト」として切り離す**方式です。
ペンギンの羽はもともと硬いので、剛体回転でも十分自然に見えます。

**FBXを読み込むと、こういう構造になっています：**

```
penguin_slim (胴体メッシュ)
├── Flipper_R   ← 原点が右肩。ここを回すと右羽が動く
└── Flipper_L   ← 原点が左肩
```

これを Unity 側で次のように組み替えて使います。

```
PlayerPenguin （胴体）
├── Flipper_L
└── StickPivot        ← 空オブジェクト。肩に置き、ここを回して振る
    ├── Flipper_R     ← ここへ移動させる（羽が棒と一緒に振れる）
    └── StickRoot     ← 棒の原点補正 Position (0,0,0.26)
        └── stick     ← Position (0,0,0) / Rotation (90,0,0)
            └── StickTip  ← 当たり判定を取る「棒の先端」(0,0.26,0)

GamerPenguin （胴体）
├── Flipper_R  ← 前方へ回してゲーム機を支える姿勢に
├── Flipper_L  ← 同上
└── ConsoleAnchor
    └── Console
```

> **羽の切り出しがうまくいかない場合**：Blenderのシステムコンソール
> （Window → Toggle System Console）に、切り出した面数が表示されます。
>
> | ログの内容 | 意味 | 対処 |
> |---|---|---|
> | `右羽=0面 / 左羽=0面` | 羽を検出できていない | `FLIPPER_SIDE_RATIO` を **0.5** に下げる |
> | `選択が多すぎます` の警告 | 胴体まで巻き込んでいる | `FLIPPER_SIDE_RATIO` を **0.75** に上げる |
> | 頭やサングラスが一緒に切れた | 高さ帯が広すぎ | `FLIPPER_Z_MAX` を **0.70** に下げる |
> | 羽の下半分しか切れない | 高さ帯が狭い | `FLIPPER_Z_MIN` を **0.20** に下げる |
>
> 調整したら Blender で再実行し、FBX を Unity に上書きインポートしてください。
> **どうしても分離できない場合**は `SPLIT_FLIPPERS = False` にすると
> 従来通り1枚メッシュで出力されます（羽は動かなくなります）。

##### ⚠️ 先に確認：ペンギンはどちらを向いていますか

このプロジェクトのスクリプトは、**ペンギンが Unity の +Z 方向を向いている**
前提で動きます（`PenguinPlayerController` が `transform.forward` へ進むため）。

1. PlayerPenguin をシーンに置き、Rotation を **(0, 0, 0)** にする
2. Scene ビューで**真上から**見て、**くちばしが +Z（青軸の方向）を向いている**か確認

向きが違う場合の対処：

- **90°や180°ずれている** → PlayerPenguin を空オブジェクト（Create Empty）の子にし、
  **子側のメッシュだけを回転**させて、親の +Z とくちばしを揃えます。
  以降は**親オブジェクト**を PlayerPenguin として扱ってください
- 生成時点で直したい場合 → Blender で書き出す前にモデルを回転させ、
  **くちばしが Blender の -Y 方向**を向くようにします（Blender の -Y が Unity の +Z）

> ここがズレていると、**前進キーで横に歩く**、**振りの回転軸がおかしい**、
> NPC の Rotation Y がすべて 90° ずれる、といった症状が出ます。
> 棒の調整を始める前に必ず合わせてください。

#### 5-2. プレイヤーの棒を「手」に持たせる

`PenguinPlayerController` は **StickPivot を回転させて**振りモーションを作り、
**StickTip の位置**で当たり判定を取ります。この2つの空オブジェクトが要です。

**棒の実寸（生成スクリプトから確定）**

| 項目 | 値 | 出どころ |
|---|---|---|
| 長さ | **0.52 m** | `build_stick()` の `depth=0.52` |
| 半径 | 0.014 m | 同 `r=0.014` |
| **メッシュ原点** | **棒の中央**（端ではありません） | `transform_apply(location=False)` で原点が移動しないため |
| 伸びる方向 | Unity の **ローカル +Y / -Y**（上下） | Blender の Z 軸が Unity の Y 軸になるため |

> **ここが「繋がって見えない」原因です。** 棒の原点は**中央**なので、
> Position (0,0,0) のまま置くと**棒の真ん中がピボットに刺さり、
> 半分が手から後ろへ突き出ます**。長さの半分（0.26m）ぶんずらす必要があります。

##### 手順1：StickPivot を「右肩」に置き、羽をその子にする

羽の原点はすでに**肩**にあります。StickPivot もそこに重ねると、
**羽と棒が同じ支点で一緒に振れる**ようになります。

1. **Flipper_R の肩の位置を調べる**
   - Hierarchy で PlayerPenguin を展開し、**Flipper_R を選択**
   - Scene ビューでギズモが出ている場所が**肩（回転の中心）**です
   - Inspector の **Position の値をメモ**します（これが肩のローカル座標）
2. **PlayerPenguin を右クリック → `Create Empty`** → 名前を **StickPivot** に変更
3. StickPivot の **Position に、いま控えた Flipper_R の Position を入力**
   - これで StickPivot と肩が**ぴったり重なります**
   - Rotation は **(0, 0, 0)** のままにしておきます（後で -20 にします）
4. **Flipper_R を StickPivot の中へドラッグ**して子にする
   - Unity は親子付けのとき**見た目の位置を保つ**ので、羽は動きません
   - 移動後、Flipper_R の **Position が (0, 0, 0) になっていれば成功**です
     （＝肩とピボットが完全に一致している）
   - 0 にならない場合は手順3の座標が少しズレています。
     Flipper_R の Position を手で (0,0,0) にすれば強制的に合わせられます
5. **動作テスト**：StickPivot の **Rotation X** を -60 くらいにしてみてください。
   **羽が肩を中心に持ち上がれば成功**です
   - 羽が肩から外れて飛んでいく → 手順3の位置がズレています
   - 羽が横に倒れる／ねじれる → **回転軸が違います**。
     Rotation Y や Z を試し、**前後に振れる軸**を見つけてください。
     もし X 以外だった場合は §5-1b の「向きの確認」に戻ってください
     （`PenguinPlayerController` は**ローカル X 軸**で振る前提です）
6. テストが終わったら **Rotation を (-20, 0, 0)** に戻す ＝ 構えの初期姿勢
   - この -20 は `PenguinPlayerController.ResetState()` が設定する値と同じです

> **Flipper_L はそのまま**（PlayerPenguin 直下）にしておきます。
> 片方の羽だけで棒を振る形になりますが、正面から見ると自然です。
> 両手持ちにしたい場合は Flipper_L も StickPivot の子にしてください。

##### 手順2：棒を StickPivot の子にし、根本を手に合わせる

> ⚠️ **数値をそのまま信用しないでください。**
> 棒のメッシュ原点は中央にあり、さらに **FBX 側にも Blender の
> オブジェクト位置（0.26m）が焼き込まれている**可能性があります。
> この2つが二重に効くと、`(0, 0, 0.26)` を入れても
> **棒の中心がピボットに来てしまう**（＝手の後ろに半分突き出る）ことがあります。
> 下の手順は、**どちらの状態でも正しく合わせられる**やり方です。

**役割を1階層ずつ分ける**のが、結果的にいちばん確実でした。
棒のメッシュ原点が中央にある問題を、**StickRoot という中間の空オブジェクト**に
専任させることで、回転支点（StickPivot）と切り離します。

```
StickPivot        ← 手（肩）の位置。★スクリプトはここを回転させる
 └ StickRoot      ← Position (0, 0, 0.26)  ※原点補正だけを担当
    └ stick       ← Position (0, 0, 0) / Rotation (90, 0, 0)
       └ StickTip ← Position (0, 0.26, 0)  ※棒の先端
```

**この構造の利点**

| 階層 | 役割 | 触る理由 |
|---|---|---|
| StickPivot | 回転支点 | 位置合わせ（手／肩）だけ。回転はスクリプトが上書き |
| **StickRoot** | **原点補正** | 棒のメッシュ原点が中央にあるぶんを吸収。**ここだけ調整すればよい** |
| stick | メッシュ | 触らない（0,0,0 / 90,0,0 固定） |
| StickTip | 判定点 | 棒の先端に置く |

原点補正を StickRoot に閉じ込めたので、**FBX側のオフセットの有無に振り回されません**。
ズレていたら **StickRoot の Position Z だけ**を動かせば直ります。

##### 作り方

1. **StickPivot を右クリック → `Create Empty`** → 名前を **StickRoot** に変更
2. StickRoot の Position を **(0, 0, 0.26)**、Rotation は (0,0,0)
   - 0.26 = 棒の長さ 0.52 の半分（`build_stick()` の `depth=0.52`）
3. `Assets/Models/stick` を **StickRoot の子**としてドラッグ
4. stick の Transform を **Position (0,0,0) / Rotation (90,0,0) / Scale (1,1,1)**
5. **stick を右クリック → `Create Empty`** → 名前を **StickTip**
6. StickTip の Position を **(0, 0.26, 0)**
   - stick のローカル座標では棒は **+Y 方向**に伸びています（Rotation 90 は
     stick 自身に掛かるため、子から見ると +Y のまま）。**Z ではなく Y** です

##### 合格条件

Scene ビューを**真横から**見て、

- 棒の**根本の断面が、羽の先端（＝StickPivot の位置）に接している**
- 棒が羽より**後ろに突き出ていない**／**離れて浮いていない**
- StickTip のギズモが**棒の先端の断面**に乗っている

ズレていたら **StickRoot の Position Z** を 0.01 刻みで調整してください
（0.26 が合わない場合は 0 や -0.26 が正解のこともあります。§5-2手順2の判定表参照）。

---

##### ⚠️ 振りのテストで「Rotation Z」で振れた場合（重要）

StickPivot の Rotation を手で動かして振りを確認したとき、

- **Rotation X で前後に振れた** → そのまま進めてOK
- **Rotation Z で前後に振れた**（例：0 → -100） → **ペンギンの向きがズレています**

**なぜか**：振りの回転軸は「左右方向の軸」でなければなりません。
ペンギンが Unity の +Z を向いていれば左右軸は X ですが、
**+X を向いていると左右軸は Z** になります。
元モデルのくちばしは Blender の +X 方向（`FRONT_OVERRIDE = "+X"`）なので、
変換後に +X を向いたままだとこうなります。

**放置するとこうなります**：

- `PenguinPlayerController` は **ローカルX軸でしか振りません**
  （`stickPivot.localRotation = Quaternion.Euler(a, 0, 0)`）。
  Z で振れる状態のままだと、**再生時は横薙ぎになります**
- `transform.forward`（+Z）へ進むので、**前進キーで横に歩きます**
- NPC の Rotation Y が全部 90° ずれます

**対処A（推奨・コード変更なし）：ペンギンの向きを直す**

1. Hierarchy 右クリック → `Create Empty` → 名前を **PlayerPenguin** に変更
   （Position はペンギンを置きたい場所、Rotation は (0,0,0)）
2. 今あるペンギンのメッシュを**この空オブジェクトの子**にする
3. **子（メッシュ）の Rotation Y だけ**を回して、
   **親の +Z 方向にくちばしが向く**ようにする（多くの場合 -90 または 90）
4. **StickPivot / ConsoleAnchor は親（PlayerPenguin）の直下**に作り直す
   - 子のメッシュの下に置くと、メッシュの回転を巻き込んでしまいます
5. `PenguinPlayerController` は**親**に付ける

これで左右軸が X に戻り、**Rotation X で正しく振れる**ようになります。
移動方向とNPCの向きも同時に直ります。**5体のペンギンすべてに同じ処置**をしてください。

**対処B：スクリプト側を軸対応にする**

シーンを組み替えたくない場合は、`PenguinPlayerController` に
軸と角度の設定を追加する方法もあります（未適用）。

```csharp
public enum SwingAxis { X, Y, Z }
public SwingAxis swingAxis = SwingAxis.Z;
public float angleReady  =    0f;   // 構え
public float angleWindUp =   80f;   // 振りかぶり
public float angleImpact = -100f;   // 振り下ろし
```
`RotateStick()` の `Quaternion.Euler(a, 0, 0)` を、選んだ軸に応じて
`Euler(0,0,a)` などに切り替えるだけです。
ただし**移動方向のズレ（前進キーで横に歩く）は別途直す必要がある**ため、
根本解決は対処Aです。

> **角度について**：対処Bを採る場合、既定の
> `-20 → -110 → 55`（X軸前提）はそのままでは使えません。
> 実測で **0 → -100 が振り下ろし**なら、
> 構え **0** / 振りかぶり **+80** / 振り下ろし **-100** あたりが出発点になります。
> Inspector で StickPivot を手動回転させ、3姿勢の角度を実測してから入れてください。

##### 手順3：StickTip を「実際に見えている先端」に置く

**ここが判定のズレに直結します。** 当たり判定は StickTip の位置**だけ**で
行われるので、StickTip が棒の中央や空中にあると
「当たって見えるのに割れない」「離れているのに当たる」が起きます。

1. **stick を右クリック → `Create Empty`** → 名前を **StickTip** に変更
2. Position を **(0, 0, 0)** にする（＝棒のメッシュ原点＝**棒の中央**に置かれます）
3. **移動ツール（W キー）で、Scene ビュー上で棒の先端まで実際にドラッグ**します
   - **真横**と**真上**の2方向から見て合わせてください
   - 目標は**棒の先端の断面の中心**。少し内側でも構いませんが、外へはみ出さないこと
4. 参考：うまく合っていれば、Position は **(0, 0.26, 0) 前後**になります
   - stick のローカル座標では棒はまだ **+Y 方向**に伸びています
     （Rotation 90 は stick 自身に掛かっているため、子から見ると +Y のまま）。
     だから **Z ではなく Y** に値が入ります
   - ここが (0, 0, 0.26) など**Y 以外に入っていたら間違い**です

##### 手順4：判定を「目で見て」確認する（推奨）

数値だけで詰めると必ずどこかでズレます。付属の
**`StickDebugGizmo.cs`** を使うと、当たり判定そのものが Scene ビューに描画されます。

1. **PlayerPenguin** を選択 → `Add Component` → **StickDebugGizmo**
2. **Scene ビューを表示したまま** Play を押す
   （Game ビューではなく **Scene ビュー**を見てください。ギズモは Scene ビューにだけ出ます）
3. 表示されるもの

| 色 | 意味 |
|---|---|
| 🟡 黄の線と小球 | StickPivot（手）から StickTip への「棒の芯」 |
| 🟢 緑の球 | **スイカに当たる範囲**（`melonHitRadius` 0.5m） |
| 🔴 赤のワイヤー球 | ペンギンを誤爆する範囲（`penguinHitRadius` 0.55m） |
| 🔵 水色の小球 | 判定に使われる**スイカの中心**（`melonPos + 0.15m`） |

4. **合格条件**：黄の線が**見えている棒と重なっている**こと。
   ズレていたら StickTip の位置が間違っています（手順3に戻る）
5. Space で振って、**緑の球が水色の小球を飲み込む瞬間がある**ことを確認

> 完成したら StickDebugGizmo のチェックを外すか、コンポーネントを削除してください。
> `OnDrawGizmos` はエディタ専用なので、付けたままでもビルドには影響しません。

##### 手順5：姿勢ごとの手動チェック（補助）

Play 中に **StickPivot を選択**し、Inspector の **Rotation X** を手で動かします。

| Rotation X | 姿勢 | スクリプト上の意味 |
|---|---|---|
| **-20** | 構え | 初期姿勢・振り終わりの戻り先 |
| **-110** | 振りかぶり | 頭の上まで持ち上げた状態 |
| **-30 〜 +55** | 振り下ろし | **この区間ずっと当たり判定が出ます**（下記） |

- 届かない（宙で止まる） → StickPivot の **Position Y を下げる**
- 地面にめり込む → StickPivot の **Position Y を上げる**
- 横にズレる → StickPivot の Position X を調整

> **判定タイミングについて（スクリプトを修正済み）**
>
> 以前の `PenguinPlayerController` は、**振り下ろしが終わった瞬間（55°）の
> 1フレームだけ**を判定していました。そのため棒がスイカを**通過する途中**で
> 当たっていても取りこぼし、逆に振り終わりの位置がたまたま近いと
> 「当たっていないのに割れる」ことが起きていました。
>
> 現在は **振り下ろし中（-110° → 55°）のあいだ毎フレーム判定**するように
> 変更してあります（`RotateStick(..., checkHit: true)`）。
> 命中するとゲーム側の状態が Play 以外へ移るので、二重に成立することはありません。
> **見た目に当たった瞬間に割れる**ようになります。

> 当たり判定は**距離だけ**で見ています（Collider は不要です）。
> `melonHitRadius` 0.5m 以内で成功、`penguinHitRadius` 0.55m 以内で失敗。
> **棒の長さ 0.52m に対して判定半径 0.5m はかなり大きい**ので、
> 「振ればだいたい当たる」寄りです。シビアにしたい場合は
> `melonHitRadius` を **0.3** 程度に下げてください（§6-7）。

---

#### 5-2b. ゲーム機をゲーマーペンギンの「手」に持たせる

ゲーマーペンギンは**羽を前方へ回して「持っている姿勢」に固定**し、
その羽の先端にゲーム機を置きます。振りのようなアニメーションは不要で、
**静的なポーズを付けるだけ**です。

##### 手順0：羽を「持つ姿勢」に回す

1. GamerPenguin を展開し、**Flipper_R を選択**
2. **Rotation X を -70 前後**にする（羽が前方へ持ち上がります）
   - 軸が違って横に倒れる場合は Y / Z を試してください（§5-2手順1と同じ要領）
   - 「肘を曲げて胸の前で持つ」ように見える角度を探します。**-60〜-80** が目安
3. **Flipper_L も同じ角度**にする
4. 真上から見て、**両羽の先端が体の前で近づいている**ことを確認
   - 開きすぎならもう少し内側へ（Rotation Y を ±10 ほど足す）

これで「両羽でゲーム機を挟んで持っている」姿勢になります。
次はその**羽の先端の高さ**にゲーム機を置きます。

**ゲーム機の実寸（生成スクリプトから確定）**

| 項目 | 値 | 出どころ |
|---|---|---|
| 幅（Unity X） | **0.145 m** | `consoleBody` の dims |
| 高さ（Unity Y） | **0.085 m** | 同 |
| 厚み（Unity Z） | **0.014 m** | 同 |
| メッシュ原点 | 本体の**中心** | |
| **画面の向き** | **Unity の +Z 側**（画面表面は原点から Z ≒ +0.0095 m） | `screen` が Blender -Y 側にあり、Unity では +Z になるため |

##### 手順1〜：ゲーム機を羽の先端に置く

1. **GamerPenguin を右クリック → `Create Empty`** → 名前を **ConsoleAnchor** に変更
2. Position をいったん (0, 0, 0) にする
3. **移動ツール（W キー）で、手順0で前方へ回した両羽の先端が触れる位置**まで動かす
   - 正面と真横の2方向から確認
   - **胸の高さではなく「羽の先端の高さ」**に合わせてください。
     ここを外すと、羽が空を掴んだままゲーム機だけが浮いて見えます
   - 参考値：全高 1.0m のペンギンで **(0, 0.45, 0.18)** 前後
   - **Z は必ずプラス（＝体の前）**にしてください。
     旧版に書いてあった Z = −0.30 は**背中側**で、ゲーム機が背後に浮いていました
4. **Rotation X を 20〜30** にする ＝ ゲーム機をやや上向きに傾け、
   「覗き込んでいる」姿勢にします
5. `Assets/Models/console` を **ConsoleAnchor の子**としてドラッグ
6. console の Transform → Position **(0, 0, 0)**、Rotation **(0, 0, 0)**、Scale (1,1,1)
7. **確認**：Scene ビューで GamerPenguin を正面から見て、
   - ゲーム機が**両手の間**にある
   - **画面（黒い面）がペンギンの顔の方を向いている**
   - 体にめり込んでいない（めり込んでいたら ConsoleAnchor の Z を +0.02 ずつ増やす）

> **画面がペンギンと反対を向いてしまう場合**：console の Rotation Y に **180** を入れて
> 裏返してください。ただしその場合、次の ScreenFocus の向きも 180° 変わります。
> **ペンギンが画面を見ている**のが正しい状態です（プレイ中なので当然ですが、
> ここが裏返っていると④⑤のカメラが機体の裏側を撮ることになります）。

##### エンディングとの関係

`EndingDirector` は `console.SetParent(null)` でゲーム機を**親から切り離して**
砂浜に置きます。このとき参照するのは **Console（メッシュ本体）**であって
ConsoleAnchor ではありません。

そのため §6-2 の EndingDirector の **Console 欄には、
ConsoleAnchor ではなく Console（メッシュ本体）を割り当ててください**。
同様に §6-1 の NestedScreens の **Console Renderer** も Console 本体です。

#### 5-3. カメラ構成（v3 の要）

カメラは **2種類 + 実行時生成の入れ子カメラ**という3層構造です。

```
GameplayCamera  … 俯瞰。RT_Game に描画。画面に映る「プレイ映像」の源
                  ★Audio Listener はここに残す
NestCam0..N     … NestedScreens が実行時に生成（触らなくてよい）
ViewCamera      … プレイヤーが実際に見るカメラ。ポーズ間を移動する
                  ★Audio Listener は削除する
```

##### ① GameplayCamera を作る

1. Hierarchy 右クリック → `Camera`、名前を **GameplayCamera** に
2. Transform → Position **(0, 3.0, -0.8)**、Rotation **(38, 0, 0)**
3. **Audio Listener を確認する**
   - Inspector に **Audio Listener** コンポーネントが**あればそのまま残す**
   - **無ければ** `Add Component` → `Audio` → **Audio Listener** で追加する
   - ※ Unity のバージョンによって、`GameObject → Camera` で作ったカメラに
     Audio Listener が付く場合と付かない場合があります。
     **最終的にシーン全体で1個だけ**になっていれば正解です（§5-4のチェック参照）
4. Camera コンポーネントの設定

> ⚠️ **「Depth」という項目は URP には存在しません**（旧版の記述を訂正）。
> Built-in レンダーパイプラインの `Depth` は、**URP では `Priority`（優先度）**
> という名前に変わり、置き場所も **Output セクションの中**に移りました。
> 値の意味は同じで、**小さいほど先に描画**されます。

**URP の Camera Inspector はセクションごとに折りたたまれています。**
目的の項目がどのセクションにあるかは下表の通りです。

| 設定する項目 | どこにあるか | 値 | 理由 |
|---|---|---|---|
| **Priority**（旧 Depth） | **Output** セクションを開く | **-10** | 入れ子カメラ（自動生成、Priority は 0 未満）より**さらに先に**描画させ、プレイ映像が1フレーム古くならないようにする |
| Output Texture | **Output** セクション | **空のまま** | `NestedScreens.Awake()` が実行時に RT_Game を設定します。手動で入れると二重設定になります |
| Render Type | **Rendering** セクション | **Base** | 既定値。Overlay にすると Priority 欄自体が消えます |
| Field of View | **Projection** セクション | **60** | 既定値のまま |
| Projection | **Projection** セクション | Perspective | 既定値 |

**Priority が見つからないときのチェック**

1. Camera コンポーネントの中の **`Output`** という見出しを探し、左の ▶ をクリックして展開
2. それでも無い場合 → **Render Type が `Overlay` になっていませんか？**
   `Base` に戻すと Priority が現れます
3. Inspector 右上の ⋮ メニュー → **Debug** に切り替えると、
   URP でも内部名の `Depth` として数値を直接編集できます（最終手段）

> **Built-in パイプラインで作ってしまった場合**は、そのまま `Depth` 欄が
> Camera コンポーネントの中ほど（Culling Mask のすぐ下あたり）にあります。
> その場合は Depth に -10 を入れてください。`NestedScreens.cs` は
> URP・Built-in の両対応なので、どちらでも動作します。

##### ② ViewCamera を作る

1. 既存の **Main Camera を選択して名前を ViewCamera に変更**（新規作成でも可）
2. **Audio Listener コンポーネントを削除**（右クリック → Remove Component）
   - Audio Listener がシーンに2つあると警告が出て、音が二重／無音になります
3. Camera コンポーネント
   - **Priority**（Output セクション。Built-in なら Depth）は **0** のまま
   - Output Texture は空
   - Tag は **MainCamera** のままにしておくと `Camera.main` が効きます
4. `Add Component` → **CameraSway**
   - Position Amount **0.01**、Rotation Amount **0.5**、Speed **0.35**
   - 揺れが強すぎると入れ子画面が読めなくなります。**0.02 / 1.0 を上限**に
   - ※CameraSway は「前フレームの揺れを引いて今フレームの揺れを足す」差分方式なので、
     `SuikawariGame` のカメラ移動コルーチンと同時に動いても位置がずれていきません

##### ③ カメラ定位置（空オブジェクト3つ）

カメラそのものではなく、**Transform だけを使う空オブジェクト**を3つ作ります。
`SuikawariGame` がこの3点の間で ViewCamera を補間移動させます。

1. Hierarchy 右クリック → `Create Empty` → 名前 **CameraPoses**（整理用の親）
2. その子として3つ Create Empty

| 名前 | Position | Rotation | 使われる場面 |
|---|---|---|---|
| **PoseWide** | (3.8, 1.6, -3.2) | (10, -40, 0) | ①タイトル / ②導入の始点 / ⑥ズームアウトの終点 / ⑦エンディング |
| **PoseGameplay** | (0, 3.0, -0.8) | (38, 0, 0) | ステージ1のプレイ視点（GameplayCamera と同値） |
| **PoseConsole** | ゲーム機画面の斜め前 約0.35m | 画面へ向ける | ステージ2以降のプレイ視点 / ③④⑤ズームインの起点 |

**PoseConsole の作り方（最重要・ここで絵が決まります）**

1. Scene ビューで **GamerPenguin の持つ Console にフォーカス**（Console を選択して F キー）
2. Scene ビューのカメラを動かし、**ゲーム機の画面がフレームの7〜8割**を占める構図を作る
3. **PoseConsole を選択した状態**で `GameObject → Align With View`（Ctrl+Shift+F）
   → Scene ビューの視点がそのまま PoseConsole の Transform になります
4. Inspector で数値を微調整

**完全な正対にしないのが3D感のコツ**です。画面の正面から

- 水平方向に **15°**（やや右へ）
- 垂直方向に **10°**（やや上から見下ろす）

ずらすと、機体の厚み・陰影・周囲の遠近がはっきり出て「本物のゲーム機を
覗き込んでいる」画になります。画面の読みやすさが落ちない **±20°以内**で調整してください。
真正面（0°）はプレイはしやすいですが、絵が平板になり
「ただのRenderTexture貼り付け」に見えてしまいます。

**距離の目安**：画面から **0.30〜0.40m**。
近づけるほど深いステージでもプレイ画面が大きく見えて**易しくなります**（§6-5）。

##### ④ ScreenFocus（ズームインの突入目標）

③④⑤のズームインは、カメラが `ScreenFocus` の位置へ突っ込むことで表現されます。

1. **Console を右クリック → `Create Empty`**（Console の子にする）
2. 名前を **ScreenFocus** に変更
3. Transform を次の値に設定します（**Console のローカル座標**）

| 項目 | 値 | 根拠 |
|---|---|---|
| Position | **(0, 0, 0.0095)** | 画面メッシュは本体原点から Z ≒ +0.0085 m、厚み 0.002 m。その**表面**が 0.0095 m |
| Rotation | **(0, 180, 0)** | +Z を画面の**奥向き**にするため（下記） |
| Scale | (1, 1, 1) | |

4. **回転が最重要**：**+Z 軸（青い矢印）が画面から「奥＝ゲーム機の内側」へ向く**
   状態にします。§5-2b で console の Rotation Y を 180 にした場合は、
   ScreenFocus の Rotation Y は **0** になります（打ち消し合うため）

> ⚠️ **+Z の向きに注意**（旧版の記述から訂正）。
> `SuikawariGame.DollyInto()` は
> 「カメラを `ScreenFocus.position - forward × 0.02m` に置き、`forward` の方向を向かせる」
> という処理です。つまり **forward（+Z）は「プレイヤーから見て画面の奥へ進む向き」**。
> +Z を手前（視聴者側）に向けてしまうと、カメラが機体の内部へめり込んだうえに
> 後ろを向くので、真っ黒な画面や壁抜けになります。
>
> **確認方法**：ScreenFocus を選択し、Scene ビューのギズモを Local 表示にして、
> **青い矢印が画面に突き刺さる方向**を指していればOK。
> 逆だったら Rotation Y に 180 を足してください。

##### ⑤ 被写界深度（任意・効果は大きい）

1. Hierarchy 右クリック → `Volume → Global Volume`
2. `New` で Profile を作成 → `Add Override → Post-processing → Depth of Field`
3. Mode **Bokeh**、Focus Distance = **ViewCamera からゲーム機までの距離**（≒0.35）
4. Aperture 5.6 前後

背景がとろけて「機械を覗き込んでいる」実在感が一気に出ます。
ただし**ステージ1（俯瞰）ではボケすぎる**ので、気になる場合は
Focus Distance を 3.0 くらいの中間値にするか、Volume を弱めてください。

#### 5-4. ここまでの確認

Play を押さずに、Scene ビューだけで次を確認します。

- [ ] PoseConsole の位置から見て、ゲーム機の画面が大きく写り、周囲に
      パラソルと砂浜と海が見えている（＝④⑤で「周囲に風景が残る」の下地）
- [ ] PoseWide の位置から見て、5匹のペンギン・スイカ・パラソル・海・山が
      全部フレームに入っている
- [ ] ScreenFocus の青い矢印が画面の奥を向いている
- [ ] Hierarchy を検索窓で `t:AudioListener` と絞り込み、**1個だけ**である

---

### 6. ゲームの配線

スクリプトを付けて、Inspector の参照を埋めていく工程です。
**未割り当ての参照が1つでもあると `NullReferenceException` で止まります**ので、
下のチェックリストを潰しながら進めてください。

#### 6-1. NestedScreens（先に作る）

`SuikawariGame.Awake()` が `nested.viewCamera` を読むので、こちらを先に用意します。

1. Hierarchy 右クリック → `Create Empty` → 名前 **NestedScreens**
2. `Add Component` → **NestedScreens**
3. 参照を割り当て

| 項目 | 割り当てるもの | 補足 |
|---|---|---|
| Gameplay Camera | **GameplayCamera** | 俯瞰カメラ。RT_Game の描画元 |
| View Camera | **ViewCamera** | 実際に画面へ出るカメラ |
| Console Renderer | **Console の MeshRenderer** | Console 本体（子にメッシュがある場合はその子）をドラッグ |
| Screen Material Name | `ScreenMat` | 既定のまま。マテリアル名の**部分一致**で探します |

4. 設定

| 項目 | 推奨値 | 補足 |
|---|---|---|
| RT Width / Height | **1280 / 720** | 深いステージで重い場合は 960/540 に下げる |
| Screen Brightness | **1.15** | 画面の自発光強度。Bloom と合わせて調整 |

> **Console Renderer の選び方**：FBX をドラッグした場合、Console の下に
> メッシュを持つ子オブジェクトがぶら下がっていることがあります。
> Console を選択して Inspector に **Mesh Renderer が見えなければ**、
> 子オブジェクトを展開して Mesh Renderer を持つものを割り当ててください。
> `NestedScreens.Awake()` はこの Renderer の materials 配列から
> 名前に `ScreenMat` を含むものを探します。

**動作イメージ**（理解しておくとデバッグが早いです）

```
ステージ1: 画面 = RT_Game（自分の映像が映る＝合わせ鏡の1段目）
ステージ2: ViewCamera が PoseConsole に立ち、画面 = RT_Game
ステージ3: NestCam0 が PoseConsole で「画面=RT_Game の状態」を撮って RT_Nest0 へ
          → ViewCamera が見る画面 = RT_Nest0
ステージ5: NestCam0/1/2 の3台が連鎖。画面 = RT_Nest2
```
中間カメラは `SetStage()` が **毎ステージ作り直し**、`SetNestPose()` が
PoseConsole に配置します。手作業でカメラを増やす必要はありません。

#### 6-2. GameRoot（進行管理とエンディング）

1. Hierarchy 右クリック → `Create Empty` → 名前 **GameRoot**、Position (0,0,0)
2. `Add Component` → **SuikawariGame**
   - `[RequireComponent(typeof(AudioSource))]` なので **AudioSource が自動追加**されます
   - その AudioSource の **Play On Awake を必ずオフ**にしてください
     （効果音専用。オンだと起動時に無音クリップが鳴りっぱなしになります）
3. `Add Component` → **EndingDirector**（同じ GameRoot に付けます）

##### SuikawariGame の割り当て

**シーン参照**

| 項目 | 割り当て |
|---|---|
| Player | **PlayerPenguin**（PenguinPlayerController が付いたもの） |
| Melon Whole | **Melon** |
| Melon Half L | **MelonHalfL** |
| Melon Half R | **MelonHalfR** |
| Npc Penguins | Size **4** → NPC_A / NPC_B / NPC_C / NPC_D |
| Nested | **NestedScreens** オブジェクト |
| Ending | **GameRoot**（EndingDirector が付いているので自分自身） |

**カメラ定位置**

| 項目 | 割り当て |
|---|---|
| Pose Wide | **PoseWide** |
| Pose Gameplay | **PoseGameplay** |
| Pose Console | **PoseConsole** |
| Screen Focus | **ScreenFocus** |

**ゲーム設定**

| 項目 | 既定値 | 意味 |
|---|---|---|
| Total Stages | 5 | ステージ数。増やすとカメラ台数も増えて重くなります |
| Time Limit | 10 | 1ステージの制限時間(秒) |
| Delay Per Stage | 0.18 | 1ステージごとに増える入力遅延(秒)。ステージ5で 0.72 秒 |
| Melon Hit Radius | 0.5 | スイカに当たったと判定する距離(m) |
| Penguin Hit Radius | 0.55 | NPC を叩いたと判定する距離(m) |
| Zoom Out Duration | 7 | ⑥のズームアウト秒数 |

**サウンド**（`Assets/Audio` から6つ割り当て）

| 項目 | クリップ |
|---|---|
| Se Swing | swing |
| Se Crack | crack |
| Se Hit Fail | hit_fail |
| Se Clear | clear |
| Se Game Over | gameover |
| Se Cheer | cheer |

**UI**

- Japanese Font：日本語タイトルにしたい場合のみ、日本語 ttf を `Assets/Fonts` に入れて割り当て。
  未設定なら Unity 内蔵の英字フォント（LegacyRuntime.ttf）が使われます

##### EndingDirector の割り当て

| 項目 | 割り当て | 補足 |
|---|---|---|
| Gamer Penguin | **GamerPenguin** | パラソル下のペンギン |
| Console | **Console** | 合流時に砂浜へ置かれます |
| Join Point | **JoinPoint** | 輪の中の合流位置 |
| All Penguins | Size **6** | **順番が重要**（下記） |
| Audio Src | **GameRoot の AudioSource** | SuikawariGame と共有でOK |
| Se Cheer | **cheer** | |

> ⚠️ **All Penguins の順番**：`EndingDirector` は
> **先頭4体を「呼んでいる観客」**として扱います（`SubArray(allPenguins, 0, 4)`）。
> 必ず次の順に入れてください。
>
> `0:NPC_A  1:NPC_B  2:NPC_C  3:NPC_D  4:GamerPenguin  5:PlayerPenguin`
>
> 順番を間違えると、ゲーマーペンギン自身が「自分を呼ぶ」動きになります。

#### 6-3. PenguinPlayerController

1. **PlayerPenguin** を選択 → `Add Component` → **PenguinPlayerController**
2. 参照

| 項目 | 割り当て | 注意 |
|---|---|---|
| Stick Pivot | **StickPivot** | **肩**に置いた空オブジェクト（v3では「手」でした。羽の分離に伴い意味が変わっています） |
| Stick Tip | **StickTip** | 棒の**先端**。ここの位置が当たり判定そのものです |

> **StickPivot は「回転の支点」であって「手」ではありません。**
> 羽（Flipper_R）と棒がどちらもこの下にぶら下がり、
> ここを回すと**羽ごと棒が振れます**（§5-2手順1）。
> Stick Tip の位置がズレていると判定が見た目と合わなくなるので、
> `StickDebugGizmo` で必ず確認してください（§5-2手順4）。

3. 移動設定

| 項目 | 既定値 | 意味 |
|---|---|---|
| Move Speed | 1.4 | 前後移動の速さ(m/s) |
| Turn Speed | 140 | 旋回速度(度/秒) |
| Area Min | (-4, 0.2) | 移動範囲の (X最小, **Z**最小)。Y ではなく Z です |
| Area Max | (4, 7) | 移動範囲の (X最大, **Z**最大) |

> Area の Vector2 は **(X, Z)** を意味します（Y ではありません）。
> スイカが Z=4 にあるので、Z の上限 7 は「スイカを通り越して少し先まで行ける」設定です。
> 難しくしたい場合は範囲を広げ、易しくしたい場合は
> Area Min/Max を (-2, 0.5)〜(2, 5.5) くらいに狭めます。

**入力遅延の仕組み**：入力は毎フレーム時刻付きでキューに積まれ、
`inputDelay` 秒が経過したものだけが適用されます。`inputDelay` は
`SuikawariGame.RunStage()` が `delayPerStage × (Stage - 1)` として毎ステージ設定するので、
**Inspector で手入力する必要はありません**（実行中に値が上書きされます）。

#### 6-4. 待機モーション

NPC_A〜D と GamerPenguin に `PenguinIdleBob` を付けます。

| 項目 | 既定値 | 意味 |
|---|---|---|
| Sway Angle | 4 | 左右の揺れ角度(度) |
| Sway Speed | 1.2 | 揺れの速さ |
| Hop Chance | 0.15 | 毎秒の跳ねる確率 |
| Hop Height | 0.12 | 跳ねる高さ(m) |

個体差を出したい場合は、ペンギンごとに Sway Speed を 1.0 / 1.2 / 1.4 / 0.9 と
少しずつ変えると、群れが機械的に見えなくなります。

> ⚠️ **既知の競合**：`PenguinIdleBob` は毎フレーム `transform.position` を
> **Start 時に記録した基準位置に上書き**します。そのため、
> `SuikawariGame.HopGroup()`（②導入・クリア時の歓喜ジャンプ）や
> `EndingDirector` の合流アニメと**取り合いになり、跳ねて見えません**。
>
> **対処**：`PenguinIdleBob.cs` の `Update()` の先頭に次の1行を足し、
> 演出中は自分を止められるようにしておくのが簡単です。
>
> ```csharp
> void Update()
> {
>     if (!enabled) return;      // ← 実際には enabled=false で Update ごと止まります
> ```
>
> **この問題はまだコード上で直していません。**
> 対処の選択肢と判断材料を **§12-1** にまとめてあるので、そちらを見て
> どちらかを適用してください。⑦のエンディングだけでも効かせておくと、
> 合流アニメがきちんと見えるようになります。

#### 6-5. 実行して確認するチェックリスト

Play を押して、上から順に確認していきます。
**1つ詰まったら §11 のトラブルシューティング**を先に見てください。

> ### ⚠️ 最初に Play する前に：入力システムの設定
>
> Unity 6 / 2022 で新規プロジェクトを作ると、**Active Input Handling が
> 「Input System Package (New)」だけ**になっていることがあります。
> このプロジェクトのスクリプトは**すべて旧 `UnityEngine.Input` API**で
> 書かれているため、そのまま Play すると次の例外が出て**何も動きません**。
>
> ```
> InvalidOperationException: You are trying to read Input using the
> UnityEngine.Input class, but you have switched active Input handling
> to Input System package in Player Settings.
> ```
>
> **直し方（1分・コード変更なし）**
>
> 1. **Edit → Project Settings → Player**
> 2. **Other Settings** を展開 → **Active Input Handling**
> 3. **`Both`** に変更
> 4. **Editor の再起動を求められるので再起動**（これをしないと反映されません）
>
> **影響を受ける箇所**（設定を直さないと下の項目は1つも進みません）
>
> | ファイル | 使っているAPI | 症状 |
> |---|---|---|
> | `GameHud.cs` | `StandaloneInputModule` | **STARTボタンが押せない** |
> | `PenguinPlayerController.cs` | `Input.GetAxisRaw` / `GetKeyDown` | **移動・棒振りができない** |
>
> `Input Manager (Old)` でも直りますが、他パッケージが新システムを
> 要求する場合に備えて **`Both`** が無難です。
>
> > **新 Input System へ正式移行することもできます**が、
> > `GameHud` の EventSystem 生成を `InputSystemUIInputModule` に差し替え、
> > `PenguinPlayerController` の入力処理を全面的に書き直す必要があります。
> > 締切が近い場合は `Both` で回避するのが現実的です。

**先に Scene ビューで確認（Play 前）**

- [ ] PlayerPenguin の Rotation を (0,0,0) にしたとき、**くちばしが +Z を向く**（§5-1b）
- [ ] Flipper_R が **StickPivot の子**にあり、Position が (0,0,0)
- [ ] 棒の根本が**羽の先端に接している**（浮いていない・突き抜けていない）
- [ ] StickTip が**棒の先端**にある
- [ ] GamerPenguin の羽が前方へ回っていて、**ゲーム機が羽に接している**

**Play して確認**

- [ ] ①タイトルが **PoseWide の全景ショット**で表示され、`PENGUIN SUIKAWARI` と
      START ボタンが出る（フェードイン 0.6 秒）
- [ ] START を押すと歓声が鳴り、4匹がホップしながら
      **カメラが 2.6 秒かけて PoseGameplay へ移動**する（②導入）
- [ ] `STAGE 1` の表示のあと 2.2 秒で操作可能になり、左下にタイマーが出る
- [ ] ステージ1は**俯瞰の素の3D**。奥のパラソル下のゲーム機画面に
      **自分の映像がループして映っている**（合わせ鏡状態）
- [ ] Space を押すと、**羽と棒が一緒に**振り上がって振り下ろされる
      （羽だけ／棒だけが動く場合は §5-2手順1〜2 に戻る）
- [ ] Space でスイカを割ると、半分が左右に飛び、`STAGE CLEAR!` が出る
- [ ] クリア後 **カメラがゲーム機へ 2.2 秒で寄り**、さらに 1.5 秒で
      **画面に吸い込まれて**暗転 → ステージ2
- [ ] ステージ2以降は PoseConsole 視点。**画面の外側に砂浜・パラソル・海が3Dで見える**
- [ ] ステージ3以降、画面の中にゲーム機、その中にまたゲーム機…と
      **入れ子が1段ずつ深くなる**（コンテ④⑤）
- [ ] ステージが進むごとに `DELAY 0.18s` `0.36s` … と表示され、
      **操作の効きが目に見えて遅れる**
- [ ] ステージ5クリアで `ALL CLEAR!!` → **7秒かけてゆっくり全景まで引く**（⑥）
- [ ] 4匹が跳ねて呼ぶ → ゲーム機が砂浜に置かれる →
      ゲーマーペンギンがよちよち合流 → 全員でジャンプ（⑦）
- [ ] `CLEAR!` 画面が出て、[CREDITS] でクレジット表示のトグル、
      [TITLE] でタイトルへ戻る（⑧⑨）
- [ ] わざと NPC を叩く → `GAME OVER / PENGUIN DOWN...` → **タイトルへ戻る**
- [ ] 何もせず10秒待つ → `GAME OVER / TIME UP` → **タイトルへ戻る**

#### 6-6. 操作

| キー | 動作 |
|---|---|
| **← → / A・D** | その場で旋回 |
| **↑ ↓ / W・S** | 向いている方向へ前後移動 |
| **Space** | 棒を振る（振りかぶり0.18秒 → 振り下ろし0.10秒 → 判定 → 構えに戻る） |

振っている最中は移動・旋回が止まります（`swinging` フラグ）。
これは「振りかぶってから当たるまでに考える時間がある」ゲーム性のための仕様です。

#### 6-7. 難易度調整

このゲームの難しさは **①入力遅延 ②制限時間 ③画面の見づらさ** の3要素で決まります。

| 調整先 | 場所 | 易しくする | 難しくする |
|---|---|---|---|
| 入力遅延 | SuikawariGame → Delay Per Stage | 0.12 | 0.25 |
| 制限時間 | SuikawariGame → Time Limit | 14 | 8 |
| 当たり判定 | SuikawariGame → Melon Hit Radius | 0.7 | 0.35 |
| 誤爆判定 | SuikawariGame → Penguin Hit Radius | 0.4 | 0.7 |
| 画面の大きさ | **PoseConsole を画面に近づける** | 0.28m | 0.45m |
| 移動範囲 | PenguinPlayerController → Area Min/Max | 狭める | 広げる |

**PoseConsole の距離が一番効きます。** 近づけるほど入れ子の奥のプレイ画面も
大きく見えるので、深いステージでも遊べるようになります。逆に離すと
ステージ4以降は「画面の中の小さな画面」を目を凝らして見ることになり、
一気に難易度が跳ね上がります。

> **展示・動画用のおすすめ設定**：Time Limit **14**、Delay Per Stage **0.15**、
> PoseConsole 距離 **0.32m**。撮影中に失敗してやり直す回数が減ります。

---

### 7. ライティングと空気感

#### 7-1. Directional Light

シーンに最初からある `Directional Light` を選択して設定します。

| 項目 | 値 | 狙い |
|---|---|---|
| Rotation | **(50, -35, 0)** | 高めの夏の日差し。影が短く出ます |
| Color | `#FFF4E0` | わずかに暖色。真っ白より砂浜が生きます |
| Intensity | **1.3** | やや強め。白飛び手前 |
| Shadow Type | Soft Shadows | |
| Shadow Strength | 0.7 | 影を落としすぎない（夏の反射光の再現） |

#### 7-2. 環境光

`Window → Rendering → Lighting` → Environment タブ

- Source: **Skybox**
- Intensity Multiplier: **1.1**（砂浜からの照り返し感）
- Ambient Mode: Baked ではなく **Realtime** でOK（動的オブジェクトが多いため）

#### 7-3. Global Volume（ポストプロセス）

§5-3⑤ で作った Global Volume に Override を追加していきます。

| Override | 設定 | 効果 |
|---|---|---|
| **Bloom** | Intensity **0.4** / Threshold **1.0** | ゲーム機画面の発光が滲む。**入れ子表現の要** |
| **Color Adjustments** | Saturation **+12** / Contrast +5 | 夏らしい鮮やかさ |
| **Vignette** | Intensity **0.2** / Smoothness 0.5 | 視線が画面中央に集まる |
| **Depth of Field** | §5-3⑤ 参照 | 覗き込んでいる実在感 |
| **Tonemapping** | Mode **ACES** | 白飛びを抑えて映画的に |

> **Bloom を効かせすぎない**：Intensity を 0.8 以上にすると、
> 入れ子の階層が深いところで**光が累積して真っ白**になります
> （RTに焼かれたBloomがさらにBloomされるため）。0.3〜0.5 が安全域です。
> 同じ理由で `NestedScreens` の Screen Brightness も 1.3 以上は避けてください。

#### 7-4. URP Asset

`Assets/Settings` の URP Asset（Project Settings → Graphics で使用中のもの）を選択。

- Shadows → **Distance 50**（既定 50。遠景の山まで影を落とす必要はありません）
- Shadows → Cascade Count 2、Soft Shadows **オン**
- Quality → HDR **オン**（Bloom に必要）
- Quality → Anti Aliasing (MSAA) **4x**

#### 7-5. 空

Skybox は既定のプロシージャルでも成立しますが、
**入道雲系の無料アセット**（Asset Store の "Free Skybox" 系）を入れると
絵コンテの夏空に一気に近づきます。

1. Asset Store からインポート
2. `Window → Rendering → Lighting` → Environment → Skybox Material に割り当て
3. Skybox の Rotation を調整して、**入道雲が山の上に来る**構図にする

---

### 8. サウンド設計

#### 8-1. 音の一覧と鳴る場所

| 音 | 鳴る場所 | 実装 | 備考 |
|---|---|---|---|
| ocean_loop | 常時ループ | Ocean の AudioSource | Spatial Blend 0.6 で遠鳴りに |
| cheer | ②導入開始 / クリア時 / ⑦呼びかけ・合流 | SuikawariGame + EndingDirector | 計4箇所で鳴ります |
| swing | Space を押した瞬間 | `PenguinPlayerController.Swing()` | 振りかぶりの開始と同時 |
| crack | スイカが割れた瞬間 | `SuikawariGame.ClearRoutine()` | crack → 0.4秒後 → clear |
| clear | ステージクリア | 同上 | |
| hit_fail | NPC を叩いた | `SuikawariGame.FailRoutine()` | → タイトルへ |
| gameover | 時間切れ | 同上 | → タイトルへ |

#### 8-2. AudioSource の配置方針

- **GameRoot の AudioSource**：効果音すべて（`PlayOneShot` で再生）
  - Play On Awake **オフ**、Loop **オフ**、Spatial Blend **0**（2D）、Volume 1
  - 2D にするのは、ズームインでカメラが移動しても効果音の音量が変わらないようにするためです
- **Ocean の AudioSource**：環境音のみ（3D）
- **Audio Listener は GameplayCamera に1つだけ**

#### 8-3. BGM を足す場合（任意・効果は大きい）

1. GameRoot の子に空オブジェクト **BgmSource** を作り、AudioSource を追加
   - Loop オン、Spatial Blend 0、Volume 0.35（効果音より小さく）
2. タイトル用とプレイ用でクリップを分けるとメリハリが出ます
3. **ズームイン中にピッチを落とす**と「画面の中に入っていく」感が激増します。
   `SuikawariGame.ClearRoutine()` の `DollyInto` 呼び出しの前後に挟みます：

```csharp
// 画面中心へ突っ込む（画面が視界いっぱいになる）
StartCoroutine(PitchTo(0.9f, 1.5f));      // ← 追加
yield return DollyInto(screenFocus, 1.5f);
yield return hud.Fade(1f, 0.18f);
bgmSource.pitch = 1f;                      // ← 追加（次ステージで戻す）
```

```csharp
// SuikawariGame にヘルパーを追加
public AudioSource bgmSource;
IEnumerator PitchTo(float to, float dur)
{
    float from = bgmSource.pitch, t = 0;
    while (t < 1f) { t += Time.deltaTime / dur;
        bgmSource.pitch = Mathf.Lerp(from, to, t); yield return null; }
}
```

#### 8-4. 音量バランスの目安

引きの画（PoseWide）と寄りの画（PoseConsole）で印象が変わるので、
**⑥のズームアウト中に一度通しで聴いて**調整してください。

| 音 | Volume |
|---|---|
| ocean_loop | 0.5 |
| BGM | 0.35 |
| swing / crack | 0.8 |
| cheer | 0.7 |
| clear / gameover | 1.0 |

---

### 9. 撮影と投稿

#### 9-1. 応募要項

- 夏テーマの Unity シーン画像 or 動画（フルHD相当、動画は10秒程度以上）を
  **#Unityサマーチャレンジ** を付けて X に投稿 ＋ 応募フォーム提出
- 締切 **2026年7月24日**

#### 9-2. Unity Recorder の設定

1. `Window → Package Manager` → Unity Registry → **Unity Recorder** をインストール
2. `Window → General → Recorder → Recorder Window`
3. `+ Add Recorder → Movie`
4. 設定

| 項目 | 値 |
|---|---|
| Capture | Game View |
| Output Resolution | **FHD - 1080p** |
| Frame Rate | 60（重ければ 30） |
| Encoder | H.264 |
| Quality | High |
| Output File | `Recordings/suikawari_<take>` |

5. Recorder Window の **START RECORDING** を押すと Play が始まり、録画されます

> **入れ子が深いステージは重い**ので、録画中にフレームが落ちる場合は
> Recorder の `Cap FPS`（Constant フレームレート）を使ってください。
> 実時間より遅くなりますが、出力される動画は滑らかになります。

#### 9-3. 推奨カット構成（合計40秒前後）

| # | 内容 | 尺 | 見せどころ |
|---|---|---|---|
| 1 | ①タイトル → ②導入 | 4秒 | 浜辺の全景とタイトルロゴ |
| 2 | ステージ1プレイ〜クリア → ③ズームイン | 8秒 | **画面に吸い込まれる瞬間**が最大のフック |
| 3 | ステージ4の深い入れ子＋遅延で苦戦 | 8秒 | 入れ子の階層が見える構図で |
| 4 | ⑥ゆっくりズームアウト → ⑦合流ジャンプ | 12秒 | 7秒の引きが効きます。BGMを盛り上げる |
| 5 | 砂浜に置かれたゲーム機の画面ループのアップ | 4秒 | 静かに締める |

**サムネイル（静止画）用のおすすめ**：ステージ3〜4の入れ子が3段見えていて、
かつ周囲に砂浜・パラソル・海が入っている構図。
`Game View → 右クリック → Maximize` してスクリーンショットを撮るか、
Recorder の Image Sequence で1枚だけ書き出します。

#### 9-4. 投稿文のヒント

「画面の中の画面を操作する」という一言が刺さります。
入れ子が深くなるほど操作が遅れる、というルールも合わせて書くと、
動画を見る前に何が起きているか伝わります。

---

### 10. 今後の拡張（⑩ ほか）

#### 10-1. ステージセレクト（コンテ⑩・未定枠）

`GameHud.BuildTitlePanel()` にボタンを足し、`SuikawariGame` 側で
開始ステージを指定できるようにします。

1. **GameHud にボタンを追加**
```csharp
GameObject BuildTitlePanel()
{
    // ...既存のコード...
    MakeButton(p.transform, "START", new Vector2(0.5f, 0.22f), null);
    MakeButton(p.transform, "STAGE3", new Vector2(0.5f, 0.10f), null);  // ← 追加
    p.SetActive(false);
    return p;
}
```

2. **SuikawariGame から開始ステージを指定**
```csharp
// Stage の setter を private から内部用に開ける
public int Stage { get; private set; } = 1;

void GoTitle()
{
    // ...既存のコード...
    hud.ShowTitle(() => StartCoroutine(IntroRoutine()));
    hud.BindStageSelect(3, () => { Stage = 3; StartCoroutine(RunStage()); });
}
```

`IntroRoutine()` を飛ばして直接 `RunStage()` を呼ぶのがポイントです。
`RunStage()` の中で `nested.SetStage(Stage)` と `SnapCamera(poseConsole)` が
呼ばれるので、入れ子もカメラも正しい状態で始まります。

#### 10-2. その他のアイデア

- **クリアタイム表示**：`SuikawariGame` に `float totalTime` を持たせ、
  各ステージの `timeLimit - TimeLeft` を加算。⑧のクリア画面に表示
- **音を頼りに探すヒント演出**：目隠しペンギンなので、スイカに近づくほど
  高い音が鳴る `AudioSource.pitch` の連動。`Update()` で
  `Vector3.Distance(player.position, melonPos)` を見て制御します
- **スイカの位置ランダム化**：`ResetField()` で
  `melonPos = new Vector3(Random.Range(-2f,2f), 0, Random.Range(3f,5.5f))` として
  `melonWhole.transform.position` にも反映。リプレイ性が上がります
- **ステージごとのライティング変化**：深い階層ほど夕方に近づけると、
  「長い時間ゲームをしていた」というストーリーが生まれます
- **ゲーム機の画面にスキャンライン**：ScreenMat に横縞のテクスチャを重ねると
  「ゲーム機の画面である」感じが強まり、入れ子の境界が読み取りやすくなります

---

### 11. トラブルシューティング

#### 表示・入れ子まわり

| 症状 | 原因 | 対処 |
|---|---|---|
| FBX がピンク | シェーダーが Built-in のまま | Extract Materials 後、`Edit → Rendering → Materials → Convert Selected Built-in Materials to URP` |
| **SandMat / OceanMat / MountainMat が見つからない** | **これらは自分で新規作成するマテリアル** | §2-5 の手順で `Assets/Materials` に右クリック → Create → Material で作ります。どこかに存在するものを探す必要はありません |
| マテリアルの Inspector がグレーで編集できない | FBX に埋め込まれたまま | §2-4 の Extract Materials で `.mat` として取り出す |
| 色を変えたのに見た目が変わらない | オブジェクトに割り当てていない | §2-7 の方法でオブジェクトの Mesh Renderer → Materials に割り当てる |
| 海が透けない | Surface Type が Opaque | OceanMat の **Surface Type を Transparent** に変えてからアルファを下げる（§2-5②） |
| ゲーム機の画面が真っ黒 | ScreenMat が見つかっていない | Console Renderer の割り当てを確認。Console の**子**に MeshRenderer がある場合はそちらを指定。Console 選択 → Inspector の Materials 一覧に `ScreenMat` を含む名前があるか確認 |
| 画面に何も映らない（灰色） | GameplayCamera の Output Texture を手動で設定してしまった | **空のまま**にする。`NestedScreens.Awake()` が実行時に設定します |
| **Camera に `Depth` 欄が見当たらない** | **URP では `Priority` に名前が変わっている** | Camera コンポーネントの **Output セクションを展開**し、`Priority` に -10 を入れる。無い場合は Render Type が Overlay になっていないか確認（§5-3①） |
| 入れ子が1段しか見えない | PoseConsole がゲーム機を捉えていない | PoseConsole からゲーム機の画面が7〜8割の大きさで見えるか、Scene ビューで確認。NestCam は PoseConsole と同じ場所に置かれます |
| 入れ子の映像が1フレーム古い | GameplayCamera の Depth が高い | GameplayCamera の **Depth を -10** に。NestCam は自動で `viewCamera.depth - N` になります |
| 深いステージで画面が真っ白 | Bloom と Screen Brightness が累積 | Bloom Intensity を 0.4 以下、Screen Brightness を 1.15 以下に |
| ズームインで壁抜け／真っ黒になる | **ScreenFocus の +Z が逆向き** | +Z（青矢印）が**画面から奥（機体の内側）へ**向いているか確認。逆なら Rotation Y に 180 を加算（§5-3④） |
| 深いステージが重い | RT が階層数ぶん生成される | RT Width/Height を 960/540 に。Resolution（海）を 100 以下に。Total Stages を 4 に減らす |

#### ゲームプレイまわり

| 症状 | 原因 | 対処 |
|---|---|---|
| **`InvalidOperationException: You are trying to read Input using the UnityEngine.Input class...`** | **Active Input Handling が新Input Systemのみ**。同梱スクリプトは旧APIを使用 | Project Settings → Player → Other Settings → **Active Input Handling を `Both`** に変更し、**Editorを再起動**（§6-5冒頭） |
| ボタンが押せない・キー入力が効かない | 同上 | 同上。Console に上の例外が出ていないか確認 |
| 起動直後に NullReferenceException | 参照の割り当て漏れ | Console のエラーをダブルクリックして行番号を確認。`SuikawariGame.Awake()` なら nested / player / melonWhole のどれかが空 |
| **ペンギンを展開しても手のボーンが無い** | 元モデルにボーンが入っていない | 仕様です。代わりに **Flipper_R / Flipper_L** という子オブジェクトに羽が分離されています（§5-1b） |
| **Flipper_R / Flipper_L が見当たらない** | 古いFBXのまま／切り出しに失敗 | Blenderで再実行し、コンソールの `右羽=◯面` を確認。0面なら `FLIPPER_SIDE_RATIO` を 0.5 に下げる（§5-1b） |
| **羽と一緒に胴体まで切れてしまった** | しきい値が小さすぎ | `FLIPPER_SIDE_RATIO` を **0.75** に上げて再実行 |
| **サングラスやくちばしが羽として切れた** | 高さ帯が広すぎ | `FLIPPER_Z_MAX` を **0.70** に下げて再実行 |
| **羽を回すと肩から外れて飛ぶ** | StickPivot の位置が肩とズレている | Flipper_R を StickPivot の子にした後、**Flipper_R の Position を (0,0,0)** にする（§5-2手順1） |
| **羽が横に倒れる／ねじれる** | 回転軸が X ではない | Rotation Y / Z を試して前後に振れる軸を探す。X 以外なら、そもそもペンギンの向きが +Z でない可能性（§5-1b の向き確認） |
| **Rotation Z なら振れるが X だと横薙ぎ** | **ペンギンが +Z を向いていない**（発生確認済み） | 空オブジェクトで包み、メッシュだけ Rotation Y で回して +Z に揃える。§5-2「対処A」。5体すべてに実施 |
| **棒の原点補正がうまくいかない** | 補正を stick 自体に入れている | **StickRoot** を1階層挟み、そこだけで補正する（§5-2）。stick は (0,0,0)/(90,0,0) 固定に |
| **前進キーで横に歩く** | ペンギンが +Z を向いていない | 空オブジェクトの子にしてメッシュだけ回し、親の +Z とくちばしを揃える（§5-1b） |
| **ゲーム機が浮いて見える** | 羽を前へ回していない／高さが合っていない | Flipper_R/L の Rotation X を **-70** 前後にし、ConsoleAnchor を**羽の先端の高さ**へ（§5-2b手順0） |
| **羽が体にめり込む** | 回しすぎ | Rotation X を -60 まで戻す |
| **棒が手の真ん中から突き抜けている** | 棒のメッシュ**原点が中央**にある | stick の Position Z に **+0.26**（長さ 0.52 の半分）。ただし FBX 側にオフセットが焼かれている場合は不要。§5-2手順2 の判定表で見分けます |
| **棒が手から離れて前に浮いている** | オフセットが二重に効いている | stick の Position Z を **-0.26** にして引き戻す |
| **棒が上下に伸びている** | 回転していない | stick の Rotation を **(90, 0, 0)** に |
| **当たって見えるのに割れない／離れているのに当たる** | **StickTip が棒の先端にない**（中央や空中にある） | StickTip を移動ツールで**実際の先端まで動かす**（§5-2手順3）。`StickDebugGizmo` を付けると判定が可視化されて一発で分かります（手順4） |
| 振り下ろしの途中で当たったのに割れない | 判定が1フレームだけだった | **修正済み**。振り下ろし中は毎フレーム判定します（§5-2手順5） |
| 振れば何でも当たってしまう | 判定半径 0.5m が棒の長さ 0.52m に対して大きい | `melonHitRadius` を **0.3** 程度に下げる（§6-7） |
| **ゲーム機が背中側に浮いている** | Z がマイナス（旧版の -0.30） | ConsoleAnchor の **Z をプラス**（体の前）に。参考 (0, 0.45, 0.18)（§5-2b） |
| **ゲーム機が体にめり込む** | Z が小さすぎ | ConsoleAnchor の Z を +0.02 ずつ増やす |
| **ペンギンが画面の裏側を見ている** | console の向きが逆 | console の Rotation Y に 180 を入れる。その場合 ScreenFocus の Rotation Y は 0 に（§5-2b、§5-3④） |
| **`penguin.fbx` / `penguin_blind.fbx` が無い** | 実際の出力名は別 | **`penguin_slim`**（プレイヤー）と **`penguin_slim_sunglasses`**（NPC・ゲーマー）です（§5-1） |
| **スイカが地面に半分埋まっている** | メッシュ原点が球の中心 | Melon の Position Y を **0.14** 前後に上げる（§5-1補足） |
| スイカに当たらない | StickTip の位置がずれている | Play 中に StickPivot の Rotation X を 55 にして、StickTip がスイカに触れるか目視。届かないなら StickPivot の Y を下げる |
| 何をしても当たらない | Melon Hit Radius が小さすぎ | 0.5 → 0.7 で試す |
| すぐに PENGUIN DOWN になる | NPC が近すぎる | NPC の位置をスイカから 1.5m 以上離す。または Penguin Hit Radius を 0.4 に |
| ペンギンが跳ねない | `PenguinIdleBob` が位置を上書き | §6-4 の対処を参照。演出中は `enabled = false` に |
| ⑦で合流アニメが動かない | 同上 + All Penguins の順番 | 先頭4体が NPC_A〜D になっているか確認 |
| 入力遅延が効かない | inputDelay を手入力した | `RunStage()` が毎ステージ上書きします。Inspector 値は無視されて正常 |
| プレイヤーが範囲外へ出る | Area Min/Max が広すぎ | Vector2 は **(X, Z)** です。Y と勘違いしていないか確認 |

#### 音・UI まわり

| 症状 | 原因 | 対処 |
|---|---|---|
| 音が二重／まったく鳴らない | Audio Listener が 0 個か 2 個以上 | Hierarchy の検索窓に `t:AudioListener` と入力して**1個だけ**か確認。ViewCamera 側は削除、GameplayCamera 側を残す |
| 起動時に変な音が鳴る | GameRoot の AudioSource が Play On Awake | **オフ**にする |
| 効果音がズームインで小さくなる | AudioSource が 3D | GameRoot の AudioSource の Spatial Blend を **0（2D）**に |
| 文字がまったく出ない | 古い Unity に LegacyRuntime.ttf がない | `GameHud.cs` の `Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")` を `"Arial.ttf"` に変更 |
| 日本語が豆腐（□）になる | 内蔵フォントに日本語がない | 日本語 ttf を `Assets/Fonts` に入れ、SuikawariGame の **Japanese Font** に割り当て |
| ボタンが押せない | EventSystem がない／Canvas の前に何かある | `GameHud.Build()` が EventSystem を自動生成します。それでも押せない場合は他の Canvas の Sorting Order を 100 未満に |

#### 海まわり

| 症状 | 原因 | 対処 |
|---|---|---|
| 海がカクつく | Resolution が高すぎ | GerstnerOcean の Resolution を **100 以下**に。毎フレーム `RecalculateNormals()` が走るので、ここが一番重い部分です |
| 波がトゲトゲになる | Steepness が大きすぎ | 各 Wave の Steepness を 0.25 以下に。3つの合計が 0.5 を超えないように |
| 停止中は板のまま | メッシュは Awake で生成 | 仕様です。Play すると波打ちます |
| 砂浜が水没する／隙間が空く | Ocean の Y が不適切 | Play 中に Y を 0.05〜0.20 で調整 → 値をメモ → 停止して入力（§4-3） |

---

### 12. 未解決の課題（作業前に把握しておくこと）

v4時点で**まだ直っていない**、あるいは**検証できていない**項目です。
着手前に読んでおくと、原因不明の不具合に時間を取られずに済みます。

#### 12-1. `PenguinIdleBob` が演出アニメと競合する（未修正）

**症状**：②導入・クリア時・⑦エンディングで、ペンギンが跳ねない／合流しない。

**原因**：`PenguinIdleBob.Update()` が毎フレーム
`transform.position = basePos + …` と**位置を上書き**します（`PenguinIdleBob.cs:39`）。
一方 `SuikawariGame.HopGroup()` と `EndingDirector` も同じ Transform を動かすため、
待機モーション側が勝ってしまい、演出が画面上でほぼ見えません。

**対処（どちらか）**：

```csharp
// 案A: EndingDirector.Run() の冒頭で待機モーションを止める
foreach (var p in allPenguins) {
    var bob = p.GetComponent<PenguinIdleBob>();
    if (bob) bob.enabled = false;
}
```

```csharp
// 案B: PenguinIdleBob に「演出中は退く」フラグを持たせる（HopGroupにも効く）
public bool suspended;
void Update() {
    if (suspended) return;
    // …以下そのまま
}
```

**案Bの方が確実**です（②導入のジャンプにも効くため）。
`SuikawariGame.HopGroup()` と `EndingDirector.Run()` の前後で
`suspended` を切り替えてください。

> **なぜ未修正か**：どちらの案でも動きますが、待機モーションを
> 止めるタイミング（演出ごとか、全編通してか）は演出の好み次第のため、
> 判断を残してあります。

#### 12-2. ペンギンの向きが Unity +Z ではない（発生を確認済み）

> **状況**：実機で **StickPivot の Rotation Z（0 → -100程度）で振り下ろせる**ことが
> 確認されました。これは**ペンギンが +Z を向いていない**ことの決定的な証拠です。
> 振りの回転軸は「左右方向の軸」でなければならず、
> +Z を向いていれば X、**+X を向いていると Z** になるためです。

**原因**：スクリプトは**ペンギンが +Z を向いている前提**です
（`PenguinPlayerController` が `transform.forward` へ進み、
`RotateStick` が**ローカルX軸**で振るため）。
一方、生成スクリプトの `FRONT_OVERRIDE = "+X"` が示す通り、
元モデルのくちばしは **Blenderの+X方向**を向いています。

**この状態のまま進めると出る症状**：

| 症状 | 原因 |
|---|---|
| 再生すると棒が**横薙ぎ**になる | スクリプトはローカルX軸でしか振らない |
| **前進キーで横に歩く** | `transform.forward`（+Z）へ進むため |
| NPC の Rotation Y が全部90°ずれる | 表の値は +Z 前提 |
| 羽の回転軸も X ではなくなる | 同じ理由 |

**対処**：§5-2 の「⚠️ 振りのテストで Rotation Z で振れた場合」を参照。
**対処A（空オブジェクトで包んでメッシュだけ回す）が根本解決**で、
振り・移動・NPCの向きが同時に直ります。**5体すべてに同じ処置**が必要です。

> **手戻りを減らすには**：棒や羽の微調整を詰める前に、
> 先に向きを直してください。向きを直すと座標系が変わるため、
> 先に詰めた数値がやり直しになります。

#### 12-3. 羽の切り出ししきい値は未検証

`separate_flippers()` のポリゴン選択（`FLIPPER_SIDE_RATIO` 等）は、
**元モデルの形状に依存**します。既定値 0.62 は一般的なペンギン体型を想定した
見積もりで、実際のモデルで検証はできていません。

**初回実行時は必ず** Blender のシステムコンソールで面数を確認し、
§1-3 の表に従って調整してください。数回の試行が必要な前提で
スケジュールを見ておくと安全です。

#### 12-4. 暫定策：羽が間に合わない場合

締切が近く羽の分離が終わらないときは、`SPLIT_FLIPPERS = False` にして
**羽なしで進めても作品は成立します**。その場合：

- 棒：StickPivot を**羽の先端の位置**に置き（肩ではなく）、
  振りの間 PlayerPenguin 自体を前傾させると「全身で振っている」ように見えます
- ゲーム機：ConsoleAnchor を**羽の先端が触れる高さ**まで下げ、
  Rotation X 25° で上向きに傾けると、浮いて見えにくくなります
- 撮影時は**羽が目立たないアングル**（俯瞰・ゲーム機のアップ）を多めに使う

§9-3 の推奨カットのうち、2・3・5は羽がほぼ映りません。

---

制作がんばってください！🐧🍉


---

# Part 2. テストプレイ後の修正記録

## テストプレイ後の修正手順書
### 「ペンギンたちのスイカ割り」— 検出された6件の不具合と対処

作成日：2026年7月19日（締切 7/24 まで残り5日）

---

### 対応の優先順位

限られた時間で効果を出すため、**この順で着手**してください。
上2つはゲームが成立しないレベル、下2つは見栄えの改善です。

| 順 | 項目 | 深刻度 | 所要 | 種別 |
|---|---|---|---|---|
| 1 | **③ 操作方向が合わない** | 🔴 遊べない | 30分 | シーン再構成 |
| 2 | **② ステージ2以降でConsoleが映らない** | 🔴 進行不能 | 5分 | カメラ設定 |
| 3 | **① Playerが地面に沈む** | 🟠 見た目破綻 | 10分 | コード修正 |
| 4 | **④ スイカが球のまま割れる** | 🟠 演出破綻 | 20分 | 割り当て確認 |
| 5 | **⑥ 海の質感** | 🟡 見栄え | 1〜2時間 | 素材＋コード |
| 6 | **⑤ 空の色を固定したい** | 🟢 記録のみ | 10分 | 設定の保存 |

> **①と③は同じ根（ペンギンの向き・原点）から出ています。**
> ③を先に直すとシーン構造が変わるので、①の座標調整は③の後にしてください。

---

### ③ 操作と進行方向が合わない（最優先）

#### 症状
W/S キーがペンギンの**体の側面**方向に働く。前進しているつもりが横に歩く。

#### 原因（確定）

`PenguinPlayerController.cs:58` は `transform.forward`（＝ローカル **+Z**）へ進みます。

```csharp
Vector3 move = transform.forward * (v * moveSpeed * Time.deltaTime);
```

ところが元モデルのくちばしは **+X 方向**を向いています
（`build_beach_models.py` の `FRONT_OVERRIDE = "+X"`）。
FBX変換後もその向きのままなので、**+Z ＝ ペンギンの真横**になっています。

以前「StickPivot の Rotation **Z** で振り下ろせた」という現象も同じ根です。
振りの回転軸は左右方向の軸である必要があり、
+Z を向いていれば X、**+X を向いていれば Z** になるためです。

#### 影響範囲

このまま進めると、次がすべてズレます。

| 症状 | 原因 |
|---|---|
| W/S で横に歩く | `transform.forward` が横を向いている |
| Space で棒が**横薙ぎ**になる | スクリプトはローカルX軸でしか振らない（`Quaternion.Euler(a,0,0)`） |
| NPC の Rotation Y が全部90°ずれる | 配置表の値は +Z 前提 |
| 羽の回転軸も X ではなくなる | 同じ理由 |

> ⚠️ **手でギズモを回した確認は通っても、Play すると別の動きになります。**
> スクリプトは軸を固定しているためです。

#### 対処：空オブジェクトで包み、メッシュだけ回す

**5体すべて**（PlayerPenguin / NPC_A〜D / GamerPenguin）に同じ処置が必要です。

##### 手順（PlayerPenguin の例）

1. Hierarchy 右クリック → `Create Empty` → 名前を **PlayerPenguin** に変更
   - Position：ペンギンを置きたい場所（例 `(0, 0, 1.5)`）
   - **Rotation は必ず (0, 0, 0)**
2. 今あるペンギンのメッシュ（`penguin_slim`）を、**この空オブジェクトの子**にする
   - 名前を **PenguinMesh** に変えておくと混乱しません
3. 子の **Position を (0, 0, 0)** にする
4. **子の Rotation Y だけ**を回して、**親の +Z 方向にくちばしが向く**ようにする
   - 多くの場合 **-90** または **90**。Scene ビューを真上から見て確認
   - **親は絶対に回さない**でください（親の +Z が「前」の基準になります）
5. **StickPivot / ConsoleAnchor は親（PlayerPenguin）の直下**に作り直す
   - ⚠️ 子のメッシュの下に置くと、メッシュの回転を巻き込んで再びズレます
6. **`PenguinPlayerController` は親**に付ける（子から外す）
7. `StickDebugGizmo` も親に付け直す

##### 完成後の構造

```
PlayerPenguin （空・Rotation (0,0,0)）★ここにコントローラを付ける
├── PenguinMesh      ← Rotation Y だけ回してくちばしを親の +Z へ
│   ├── Flipper_L
│   └── Flipper_R    ← ※StickPivot へ移動させる（下記）
├── StickPivot       ← 肩の位置。ここを回して振る
│   ├── Flipper_R    ← PenguinMesh から移動
│   └── StickRoot (0,0,0.26)
│       └── stick (0,0,0 / 90,0,0)
│           └── StickTip (0,0.26,0)
└── （GamerPenguin の場合）ConsoleAnchor → Console
```

> **Flipper_R の移動について**：羽は FBX の都合で PenguinMesh の子に入っています。
> StickPivot の子へドラッグしてください（Unity は親子付け時に見た目を保ちます）。
> 移動後、**Flipper_R の Position が (0,0,0)** になっていれば肩と一致しています。

##### 確認

1. 親（PlayerPenguin）を選択し、Scene ビューでギズモの **青軸（+Z）がくちばしと同じ方向**
2. Play して **W で前進、S で後退、A/D で旋回**
3. Space で棒が**縦に振り下ろされる**（横薙ぎでない）

##### NPC の Rotation について

包んだ後は、**親の Rotation Y** で向きを付けます（子は触らない）。
配置表の値（NPC_A: 60 など）がそのまま使えるようになります。

---

### ② ステージ2以降で Console が映らない

#### 症状
ステージ2に入るとカメラがゲーマーペンギンの位置に移るが、ゲーム機が見えない。

#### 原因（最有力）：カメラの **Near Clip Plane**

Unity のカメラは既定で **Near Clip Plane = 0.3m**、
つまり **30cm より近いものは描画されません**。

一方このゲームは：

| 対象 | 距離 |
|---|---|
| ゲーム機のサイズ | 幅 **14.5cm** × 高さ 8.5cm |
| PoseConsole からゲーム機まで | **0.28〜0.40m** |
| `DollyInto` の最終位置（③④⑤のズームイン） | **画面の 0.02m 手前** |

**0.3m より近づいた瞬間にゲーム機が消えます。**
ズームイン演出（画面に吸い込まれる部分）も同じ理由で破綻しているはずです。

#### 対処（5分）

1. **ViewCamera** を選択
2. Camera コンポーネント → **Projection** セクションを展開
3. **Clipping Planes → Near** を **0.3 → `0.01`** に変更
4. Far は 1000 のままでOK

> **入れ子カメラも自動で直ります。** `NestedScreens.SetStage()` は
> `cam.CopyFrom(viewCamera)` でカメラ設定を丸ごと複製するため
> （`NestedScreens.cs:108`）、ViewCamera を直せば NestCam0〜N にも反映されます。

5. **GameplayCamera** も同様に Near を **0.01** にしておくと安全です
   （俯瞰なので必須ではありませんが、揃えておくと混乱しません）

#### それでも映らない場合のチェック順

| # | 確認 | 方法 |
|---|---|---|
| 1 | **PoseConsole がゲーム機を向いているか** | PoseConsole を選択 → `GameObject → Align With View` の逆で、Scene ビューをその視点に合わせて確認 |
| 2 | **Console がアクティブか** | Hierarchy で Console のチェックが入っているか |
| 3 | **Console が GamerPenguin ごと画面外にいないか** | GamerPenguin は `(-5.5, 0, 1.2)`。PoseConsole がそこを向いているか |
| 4 | **③の包み直しで ConsoleAnchor がズレていないか** | ConsoleAnchor は**親の直下**。PenguinMesh の子だと回転を巻き込む |

> **PoseConsole の作り直し方**：Scene ビューでゲーム機の画面が
> フレームの7〜8割を占める構図を作り、**PoseConsole を選択した状態**で
> `GameObject → Align With View`（Ctrl+Shift+F）。
> 距離は **0.30〜0.35m** を目安に。近すぎると Near Clip に再び引っかかります。

---

### ① PlayerPenguin が開始すると y=0 に沈む

#### 症状
ゲーム開始前は砂浜に立っているが、Play すると Y が 0 に吸い付く。

#### 原因（確定）

`PenguinPlayerController.Update()` が**毎フレーム Y を上書き**しています。

```csharp
// PenguinPlayerController.cs:64-71
if (Mathf.Abs(v) > 0.01f)
{
    bobPhase += Time.deltaTime * 9f;
    p.y = Mathf.Abs(Mathf.Sin(bobPhase)) * 0.05f;   // ← 移動中は 0〜0.05
    ...
}
else p.y = 0f;                                      // ← 停止中は 0 に固定
transform.position = p;
```

**「地面は y=0、ペンギンの原点は足元」という前提**で書かれています。
シーンで Y を上げて置いていても、Play した瞬間に 0 に落とされます。

#### 対処A：コード修正（推奨）

開始時の Y を「地面の高さ」として記憶し、そこを基準に歩かせます。

```csharp
// フィールドを追加
float groundY;

void Start()
{
    groundY = transform.position.y;   // 配置した高さを地面とみなす
}

void Update()
{
    // …中略…

    if (Mathf.Abs(v) > 0.01f)
    {
        bobPhase += Time.deltaTime * 9f;
        p.y = groundY + Mathf.Abs(Mathf.Sin(bobPhase)) * 0.05f;   // ← groundY を加算
        transform.rotation = Quaternion.Euler(0, transform.eulerAngles.y, Mathf.Sin(bobPhase) * 6f);
    }
    else p.y = groundY;                                            // ← 0 ではなく groundY
    transform.position = p;
}
```

> `SuikawariGame.ResetField()` が `playerStartPos` へ戻すため、
> ステージをまたいでも `groundY` はズレません。

#### 対処B：コードを触らない場合

ペンギンの**メッシュ側**を持ち上げて、親の原点が地面に来るようにします。
③で包み直した後なら簡単です。

1. 親 **PlayerPenguin** の Position Y を **0** にする
2. 子 **PenguinMesh** の Position Y を、足が砂浜に接する高さに上げる

この構造なら、スクリプトが親の Y を 0 にしても見た目は正しくなります。
**③の対処をすでに行っているなら、こちらの方が早いです。**

> **どちらを選ぶか**：③で包み直すなら **対処B**（追加作業ほぼゼロ）。
> 包まない場合は **対処A**。

#### NPC が沈む場合

NPC には `PenguinIdleBob` が付いており、こちらは
`basePos`（Start時の位置）を基準にするため沈みません。
沈むのは `PenguinPlayerController` を持つ PlayerPenguin だけです。

---

### ④ スイカが割れると球が2つ出る

#### 症状
割れた瞬間、断面の赤い半球ではなく**丸いスイカが2個**現れる。

#### 原因の切り分け

Blender 側では正しく半球が作られる実装になっています
（`build_melon_half()` が `bmesh.ops.bisect_plane(..., clear_inner=True)` で
半分を削除し、`holes_fill` で断面を張って `MelonInside` を割り当て）。

そのため、**Unity 側の割り当てミス**の可能性が高いです。

##### ステップ1：FBX の中身を確認する

1. Project ウィンドウで `Assets/Models/watermelon_halves` を**クリック**
2. Inspector 下部のプレビューを見る
3. さらに **▶ を展開**して子オブジェクトを確認

| 見えるもの | 判定 |
|---|---|
| `melon_half_L` と `melon_half_R` の**2つの半球** | ✅ Blenderは正常 → ステップ2へ |
| 丸い球が2つ | ❌ Blender側の問題 → ステップ3へ |

##### ステップ2：Unity の割り当てを直す（こちらが濃厚）

**よくある間違い**：`watermelon.fbx`（丸ごと）を2回ドラッグしてしまっている。

1. Hierarchy から古い MelonHalfL / MelonHalfR を**削除**
2. `Assets/Models/watermelon_halves` を **1回だけ** Hierarchy にドラッグ
   - 名前を **MelonHalves** に変更、Position を Melon と同じ `(0, 0.14, 4)` に
3. **▶ を展開**すると `melon_half_L` と `melon_half_R` の2つの子が出る
4. **この2つの子**をそれぞれ**非アクティブ**にする（親はアクティブのまま）
5. `SuikawariGame` の割り当てを修正

| 項目 | 割り当てるもの |
|---|---|
| Melon Whole | **Melon**（`watermelon` から作ったもの） |
| Melon Half L | **melon_half_L**（← MelonHalves の**子**） |
| Melon Half R | **melon_half_R**（← MelonHalves の**子**） |

> ⚠️ **親（MelonHalves）を割り当てないでください。**
> `SplitMelon()` は L と R を別々に左右へ飛ばすため、
> 同じオブジェクトを2箇所に入れると1個しか動きません。

##### ステップ3：Blender 側だった場合

`watermelon_halves.fbx` が球2つになっていたら、`build_melon_half()` の
`bisect_plane` が効いていません。次を確認してください。

1. Blender でスクリプトを実行し、ビューポートで `melon_half_L` を選択
2. 断面が空いている（半球になっている）か目視
3. なっていない場合、`clear_inner=True` が効いていない可能性があるため、
   `plane_no=(mirror, 0, 0)` の `mirror` に **1 / -1 以外の値**が
   入っていないか確認（`build_melon_half("melon_half_L", 1)` の第2引数）

##### 断面が赤くない場合

断面のポリゴンには `MelonInside` マテリアル（材質インデックス1）が
割り当てられています。Unity で赤くない場合：

1. `melon_half_L` を選択 → Mesh Renderer → Materials
2. **Element 0 = MelonSkin、Element 1 = MelonInside** の2枠あるか確認
3. 枠が1つしかない → Extract Materials をやり直す（§2-4）
4. MelonInside の Base Map を `#E64046` 前後の赤に

---

### ⑤ 空が黄色くなった原因を特定して固定する

#### 「気に入っている」なら、まず記録を取ってください

設定が分からないまま他をいじると、**元に戻せなくなります**。
先に現状を保存します。

1. **Game ビューのスクリーンショットを撮る**（比較用）
2. `Window → Rendering → Lighting` → **Environment タブ**を開き、
   **Skybox Material の欄をスクリーンショット**
3. **Directional Light** を選択し、Inspector をスクリーンショット
4. **Global Volume** を選択し、Profile の中身をスクリーンショット

#### 黄色くなる要因（この3つのどれか）

空の色は**単独の設定ではなく、複数の要素の掛け算**で決まります。
手順書の指示のうち、黄色化に効くのは次の3つです。

| # | 場所 | 設定 | 空への影響 |
|---|---|---|---|
| 1 | **Directional Light** | Color `#FFF4E0`（暖色）／ Intensity **1.3** | プロシージャル空は**太陽光の色で全体が染まります**。暖色＋高強度 → 黄色寄りに |
| 2 | **Lighting → Environment** | Skybox Material | 既定の `Skybox/Procedural` は Atmosphere Thickness が大きいほど黄〜橙に |
| 3 | **Global Volume** | Color Adjustments の Saturation **+12**／Tonemapping **ACES** | 彩度を上げているため、元の黄みが強調される |

**最も効いているのは #1 の Directional Light** です。
手順書 §7-1 で `#FFF4E0`・Intensity 1.3 を指定しており、
プロシージャルスカイボックスはこの光源色を空全体に反映します。

#### 犯人を特定する方法（1つずつ戻して見る）

1. **Directional Light の Color を白 `#FFFFFF` に一時変更** → 空の黄色が消えれば #1 が主因
2. 戻して、**Global Volume を一時的に無効化**（チェックを外す） → 変われば #3 が関与
3. 両方戻して、**Lighting → Environment → Skybox Material を確認**

#### 気に入った状態を固定する方法

プロシージャルスカイボックスは**ライトを動かすと空も変わる**ため、
偶然の色を保つには**専用マテリアルに切り出して固定**します。

1. Project で `Assets/Materials` を右クリック → `Create → Material`
2. 名前を **SkyMat** に変更
3. Inspector 最上部の **Shader** を **`Skybox/Procedural`** に変更
4. 気に入った見た目に近づける（数値を触って調整）

| 項目 | 黄色い夕方寄りにするなら |
|---|---|
| Sun | High Quality |
| Sun Size | 0.04 |
| Atmosphere Thickness | **1.4〜1.8**（大きいほど黄〜橙） |
| Sky Tint | `#B8D8F0`（青め）〜`#E8D8A0`（黄め） |
| Ground | `#C9B896`（砂浜の照り返し） |
| Exposure | 1.2 |

5. `Window → Rendering → Lighting` → Environment → **Skybox Material** に **SkyMat** を割り当て

> これで **Directional Light を動かしても空の基本色は SkyMat 側で保たれます**
> （太陽の位置は光源に追従しますが、大気の色味は固定されます）。

#### 夏空にしたい場合（今の色を変えたいなら）

黄色を抑えて絵コンテの入道雲空に寄せるなら：

- Directional Light の Color を `#FFF8F0`（ほぼ白）に、Intensity を **1.1** に下げる
- Global Volume の Color Adjustments → Saturation を **+6** に下げる
- Atmosphere Thickness を **1.0** に

---

### ⑥ 海のリアリティを上げる

3つの別々の問題が混ざっています。**順に効果が大きい**ので上から着手してください。

#### 問題1：横から見ると透ける（最優先・5分で直る）

##### 原因
`GerstnerOcean` が生成するメッシュは**片面**です。
URP/Lit の既定は **Render Face = Front**（裏面を描画しない）なので、
カメラが水面と同じ高さや水面下に来ると**海が消えます**。

ステージ2以降はカメラが低い位置（ゲーム機の高さ）に来るため、
この症状が出やすくなります。

##### 対処

1. `Assets/Materials/OceanMat` を選択
2. **Surface Options → Render Face** を **`Both`** に変更

これだけで、横からでも水面下からでも海が見えるようになります。

> **併せて確認**：Surface Type が `Transparent` になっていると、
> 半透明同士の描画順で砂浜が透ける場合があります。
> 波の透明感が不要なら **Surface Type を `Opaque` に戻す**方が
> 見た目が安定します（アルファは Base Map の色で調整）。

#### 問題2：波が平面的（30分）

##### 原因
既定の波が3つとも**波長が短く、うねりが足りない**ためです。
遠景の海は「大きなうねりの上に小さな波が乗る」構造で立体的に見えます。

##### 対処：4つ目の「大うねり」を足す

`Ocean` を選択 → GerstnerOcean → **Waves の Size を 4** にして、以下を設定。

| # | Direction | Steepness | Wavelength | 役割 |
|---|---|---|---|---|
| 0 | (1, 0.2) | 0.20 | **22** | ★**大うねり**（追加相当） |
| 1 | (1, 0.2) | 0.22 | 11 | 中うねり |
| 2 | (0.7, 1) | 0.15 | 5.5 | 中波 |
| 3 | (-0.4, 0.8) | 0.10 | 2.8 | さざ波 |

**調整の原則**

- **Steepness の合計を 0.5〜0.67 に収める**。超えると波が自分自身を貫通してトゲになります
- 波長は**倍々に離す**（22 / 11 / 5.5 / 2.8）と自然な重なりになります
- `Time Scale` を **0.8** に下げると、大きな海に見えます（速いと風呂桶に見える）

##### メッシュの解像度

波の形が出ても頂点が粗いとカクつきます。

| 項目 | 値 |
|---|---|
| Size | 80 |
| Resolution | **160**（重ければ 120） |

> Resolution 160 は約2.6万頂点。`Update()` で毎フレーム
> `RecalculateNormals()` が走るため、**ここが最も重い処理**です。
> 入れ子が深いステージでカクつく場合は 100 まで下げてください。

#### 問題3：波打ち際が機械的・引き波がない（1時間）

##### 原因
海が**固定位置**にあり、砂浜との交差線が動かないためです。
実際の波打ち際は、海水が「寄せては返す」ことで線が前後に動きます。

##### 対処A：海全体を前後に揺らす（最も効果的・簡単）

`Ocean` オブジェクトを Z 方向にゆっくり往復させるだけで、
**波打ち際の線が前後に動き、引き波に見えます。**

新規スクリプト `Assets/Scripts/ShoreWash.cs` を作成：

```csharp
using UnityEngine;

/// <summary>
/// 海全体をゆっくり前後させて「寄せては返す」波打ち際を作る。
/// GerstnerOcean と同じオブジェクトに付けるだけ。
/// 波の形状は GerstnerOcean が作るので、こちらは「潮の満ち引き」を担当する。
/// </summary>
public class ShoreWash : MonoBehaviour
{
    [Tooltip("前後に動く幅(m)。砂浜の傾斜がゆるいほど大きく")]
    public float distance = 0.6f;
    [Tooltip("1往復にかかる秒数。6〜10秒がゆったりして見える")]
    public float period = 7f;
    [Tooltip("上下方向のわずかな増減（水位の変化）")]
    public float heightVariation = 0.02f;

    Vector3 basePos;

    void Start() => basePos = transform.position;

    void Update()
    {
        // sin をそのまま使うと往復が均等になるので、
        // 「速く寄せてゆっくり引く」ように非対称なカーブにする
        float t = (Time.time % period) / period;
        float wave = Mathf.Sin(t * Mathf.PI * 2f);
        float shaped = Mathf.Sign(wave) * Mathf.Pow(Mathf.Abs(wave), 0.7f);

        transform.position = basePos
            + Vector3.forward * (-shaped * distance)   // 手前へ寄せる = -Z
            + Vector3.up * (shaped * heightVariation);
    }
}
```

**使い方**

1. `Ocean` を選択 → `Add Component` → **ShoreWash**
2. Distance **0.6**、Period **7**、Height Variation **0.02**
3. Play して波打ち際を確認。動きが足りなければ Distance を 1.0 まで上げる

> **砂浜の傾斜を付けると効果が倍増します。** 現状 Sand は完全な水平なので、
> 海が少し動くだけで波打ち際が大きく移動します。
> Sand の Rotation X を **-1.5°** ほど付けて海側を下げると、
> 動きの量が自然になります。

##### 対処B：波打ち際に泡（フォーム）を足す

線が機械的に見えるのは、**境界がくっきりした直線**だからです。
白い帯を重ねるとぼやけて自然になります。

1. `3D Object → Plane` を作成、名前を **ShoreFoam**
2. Position：Sand と海の交差線あたり（例 `(0, 0.13, 40)`）、
   Scale `(8, 1, 0.6)` … 細長い帯にする
3. 新規マテリアル **FoamMat** を作成
   - Shader：**URP/Lit**
   - Surface Type：**Transparent**
   - Base Map の色：白 `#FFFFFF`、**アルファ 90 前後**
   - Smoothness：0.3
4. **ShoreFoam を Ocean の子にする** → ShoreWash と一緒に前後します

> **より自然にするには**：Base Map に「横に細長い、端がぼやけた白い画像」を
> 入れて Tiling を (6, 1) にすると、泡のムラが出ます。
> 素材がない場合は、アルファを 60 まで下げるだけでも直線感が和らぎます。

##### 対処C：交差線を斜めにする

海岸線と波の向きが平行だと機械的に見えます。

- **Ocean の Rotation Y を 6°** ずらす
- または **Sand の Rotation Y を 3°** ずらす

波が斜めから寄せるようになり、直線感が消えます。

#### 海の改善まとめ（チェックリスト）

- [ ] OceanMat の **Render Face を Both**（透け防止）
- [ ] Waves を **4つ**にして大うねり（波長22）を追加
- [ ] Steepness の合計が **0.67 以下**
- [ ] Time Scale を **0.8** に
- [ ] Resolution を **160**（重ければ120）
- [ ] **ShoreWash.cs** を Ocean に追加（引き波）
- [ ] Sand に **Rotation X -1.5°** の傾斜
- [ ] **ShoreFoam** を Ocean の子として追加
- [ ] Ocean の **Rotation Y を 6°** ずらす

---

### 修正後の再テスト手順

すべて直したら、この順で確認してください。

#### Play 前（Scene ビュー）
- [ ] 親 PlayerPenguin の **+Z（青軸）とくちばしが同じ方向**
- [ ] Flipper_R が StickPivot の子で Position (0,0,0)
- [ ] ViewCamera の **Near Clip = 0.01**
- [ ] MelonHalves の**子2つ**が非アクティブ

#### Play 後
- [ ] **W で前進、S で後退**（横に歩かない）
- [ ] **Space で棒が縦に振り下ろされる**（横薙ぎでない）
- [ ] ペンギンが**砂浜に立っている**（沈まない）
- [ ] スイカを割ると**断面が赤い半球が2つ**左右に飛ぶ
- [ ] クリア後、**ゲーム機に寄って画面に吸い込まれる**（途中で消えない）
- [ ] ステージ2で**ゲーム機が大きく映る**
- [ ] 横からの画で**海が透けない**
- [ ] 波打ち際が**前後に動いている**

---

### 付録：今回の6件と根本原因の対応

| 不具合 | 根本原因 | 種別 |
|---|---|---|
| ③ 操作方向 | 元モデルが +X を向いている | **モデルとコードの前提不一致** |
| ① 沈む | コードが「地面 y=0・原点は足元」を前提 | 同上 |
| ② Console不可視 | Near Clip 0.3m > 被写体距離 | **Unity の既定値** |
| ④ 球が2つ | 割り当てミス（可能性大） | セットアップ |
| ⑤ 空が黄色 | Directional Light の暖色＋高強度 | 手順書の指示どおり（意図せぬ副作用） |
| ⑥ 海 | 片面メッシュ＋固定位置 | 実装の限界 |

**①と③は同じ根**（モデルの向き・原点についてコードが置いた前提）から出ています。
③の包み直しを行えば、①も対処Bで同時に解決します。

---

## 第2ラウンド：③の修正後に出た問題

③（ペンギンの向き）を直した後のテストプレイで見つかった5件です。

| # | 症状 | 原因 | 所要 |
|---|---|---|---|
| A | スイカの断面が透明・種だけ浮く | **断面ポリゴンの法線が内向き** | 2分 |
| B | 空が黄色い（ライトは白なのに） | **ポストプロセス**（Global Volume） | 10分 |
| C | Play すると棒が肩まで上がる | **`ResetState()` の -20° 回転** | 5分 |
| D | ステージ2で画面の位置がずれる | GamerPenguin を包み直して位置が変わった | 10分 |
| E | カモメの声を混ぜたい | 新規機能 | 15分 |

---

### A. スイカの断面が透明で、種だけ浮いている

#### 原因

半球そのものは正しく作られています。問題は**断面（切り口）のポリゴンの向き**です。

`build_beach_models.py` の `build_melon_half()` は、球を半分に切ったあと
`bmesh.ops.holes_fill()` で切り口に蓋をしています。
しかし **`holes_fill` は作った面の法線の向きを保証しません。**
蓋の法線がスイカの**内側**を向くと、Unity の裏面カリングで**描画されず透明**になります。

種（`seed`）は切り口の位置に配置されているので、
蓋が消えると**種だけが宙に浮いて見えます**。まさに今の症状です。

#### 対処A：Unity 側で1分（推奨・すぐ直る）

裏面も描画するように設定するだけです。

1. `Assets/Materials/MelonInside` を選択
2. **Surface Options → Render Face** を **`Both`** に変更

これで法線の向きに関係なく断面が描画されます。
半球は閉じた形ではないので、`Both` にしても不自然にはなりません。

> **断面がまだ赤くない場合**は、マテリアルの割り当て自体を確認してください。
> `melon_half_L` を選択 → Mesh Renderer → Materials に
> **Element 0 = MelonSkin / Element 1 = MelonInside** の**2枠**があるはずです。
> 1枠しかない場合は Extract Materials をやり直します。
> MelonInside の Base Map は `#E64046` 前後の赤に。

#### 対処B：Blender 側で根本解決（再出力が必要）

`build_melon_half()` の `holes_fill` の直後に、法線を計算し直す1行を足します。

```python
caps = bmesh.ops.holes_fill(bm, edges=[e for e in ret["geom_cut"]
                                        if isinstance(e, bmesh.types.BMEdge)])
for f in caps["faces"]:
    f.material_index = 1  # MelonInside

# ↓ この2行を追加：蓋の法線を外向きに揃える
bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
for f in caps["faces"]:
    f.material_index = 1   # recalc で崩れる場合があるので再指定
```

再実行 → FBX を上書きインポートすれば、`Render Face = Front` のままでも
正しく表示されます。

> **どちらを選ぶか**：締切が近いので **対処A で十分**です。
> Blender を再実行すると羽の切り出しなども通るため、
> 他の理由で再出力するタイミングがあれば対処Bも入れておくと綺麗です。

---

### B. 空が黄色い（Directional Light は白なのに）

#### 切り分け：ポストプロセスで確定です

- Unity の設定本体（Lighting / Skybox）は**青**
- Directional Light は **`#FFFFFF`**（白）

この2つが正常なのに Game ビューが黄色いなら、
**残るのは Global Volume のポストプロセスだけ**です。

#### 30秒でできる確認

**Scene ビューと Game ビューを見比べてください。**

| Scene ビュー | Game ビュー | 判定 |
|---|---|---|
| 青 | 黄 | **ポストプロセスで確定**（Scene ビューは既定でポスプロ off） |
| 両方黄 | — | Skybox マテリアル側。Lighting → Environment を確認 |

Scene ビューのツールバーにある**ポストプロセスの切り替えボタン**を
on にすると、Scene ビューも黄色くなるはずです。これで確定します。

#### 犯人の特定（上から順に疑ってください）

**Global Volume** を選択し、Profile の Override を**1つずつ無効化**して
Game ビューの変化を見ます。

| 疑わしい順 | Override | なぜ黄色くなるか |
|---|---|---|
| **1位** | **Tonemapping（ACES）** | ACES は**明るい部分を暖色へ寄せる**特性があります。明るい空＋ACES で黄〜橙に転びます。手順書 §7-3 で ACES を指定していました |
| 2位 | **Color Adjustments** | Saturation +12 で元の黄みが強調される。**Color Filter** が白以外なら直接の原因 |
| 3位 | **White Balance** | Temperature がプラス（暖色）だと全体が黄色く |
| 4位 | **Split Toning / Shadows Midtones Highlights** | Highlights に暖色が入っていると空だけ黄色く |
| 5位 | **Bloom** | Tint が暖色だと明るい空が滲んで黄色く |

**最有力は Tonemapping の ACES** です。まずこれを無効化して確認してください。

#### 気に入った黄色を保ちたい場合

黄色が気に入っているとのことなので、**原因を特定したうえで意図的に固定**します。

1. 上の手順で犯人を特定する
2. その Override の値を**メモまたはスクリーンショット**
3. Volume Profile は `Assets` 内のアセットファイルなので、
   **右クリック → Duplicate** でバックアップを取っておく
   （名前を `SkyYellow_Backup` などに）

これで、他の調整で崩れても戻せます。

> **注意**：ACES が原因だった場合、**入れ子の画面にも同じ効果がかかります**。
> RenderTexture に焼かれた映像がさらに ACES を通るため、
> **階層が深いほど黄色が累積**します。ステージ4〜5で画面が
> 黄色く濁って見えるようなら、Tonemapping を **Neutral** に変えて、
> 黄色は Color Adjustments の Color Filter で意図的に付ける方が制御できます。

#### 夏空に戻したい場合

- Tonemapping を **Neutral** に
- Color Adjustments の Saturation を **+6** に下げる
- それでも黄色いなら White Balance の Temperature を **0** に

---

### C. Play すると棒が肩の位置まで上がる

#### 原因：位置ではなく「回転」が原因です

**スクリプトは StickPivot の位置を一切変更しません。** 変えるのは**回転だけ**です。

```csharp
// PenguinPlayerController.ResetState()
if (stickPivot) stickPivot.localRotation = Quaternion.Euler(-20f, 0, 0);
```

Play した瞬間、StickPivot の **Rotation X が -20° に設定**されます。
エディタ上で Rotation を **0 のまま**にしていると、
**Play の瞬間に -20° 回転し、その分だけ棒が持ち上がります。**

#### 計算で確認

StickPivot（肩）から棒が `(0, -0.3, 0.26)` の位置にぶら下がっているとします。
これを X 軸まわりに -20° 回転させると：

```
y' = -0.3 × cos(-20°) - 0.26 × sin(-20°) = -0.282 + 0.089 = -0.193
z' = -0.3 × sin(-20°) + 0.26 × cos(-20°) =  0.103 + 0.244 =  0.347
```

**Y が -0.3 → -0.193 に上がります（+0.107m）。**
「設定していた 0.15 分ほど上昇した」という体感と一致します。

#### 対処：エディタ上でも -20° にしておく

**Play 時と同じ姿勢でエディタ上でも見えるようにする**のが解決策です。

1. **StickPivot** を選択
2. **Rotation X を `-20`** に設定（Play 時と同じ構えの姿勢にする）
3. **この状態で**、棒（StickRoot）の位置を羽の先端に合わせ直す
4. Play して、**エディタ上の見た目と一致**することを確認

これで「エディタで合わせた位置＝Play時の位置」になります。

> **なぜ -20° なのか**：構え（振り始めの姿勢）の角度です。
> `RotateStick(-20 → -110 → 55 → -20)` の起点になっています。
> 変えたい場合は `ResetState()` と `Swing()` の両方の -20 を
> 揃えて書き換える必要があります（3箇所）。

#### 補足：回転軸が X で合っているか

③の包み直しが正しくできていれば、**X 軸で前後に振れる**はずです。
Play して棒が**横に倒れる**なら、まだ向きが直りきっていません。
親の +Z とくちばしの向きを再確認してください。

---

### D. ステージ2で画面が写したい位置からずれる

#### 原因

**PoseConsole はワールド座標に固定された空オブジェクト**です。
一方 Console は `GamerPenguin → ConsoleAnchor → Console` という親子構造の中にあります。

③の修正で **GamerPenguin を空オブジェクトで包み直した**ため、
ゲーム機のワールド座標が変わり、**PoseConsole が古い位置を向いたまま**になっています。

#### 対処A：PoseConsole を貼り直す（5分）

1. Scene ビューで **Console を選択して F キー**（フォーカス）
2. Scene ビューのカメラを動かし、
   **ゲーム機の画面がフレームの7〜8割**を占める構図を作る
   - 正対させず、**水平15°・上10°** ほどずらすと立体感が出ます
   - 画面からの距離は **0.30〜0.35m**
3. Hierarchy で **PoseConsole を選択**
4. `GameObject → Align With View`（**Ctrl+Shift+F**）
5. Play してステージ2を確認

#### 対処B：PoseConsole をゲーム機の子にする（推奨・再発防止）

**今後 GamerPenguin を動かしても自動で追従**するようになります。

1. **ConsoleAnchor を右クリック → `Create Empty`** → 名前を **PoseConsole** に
   （既存の PoseConsole は削除するか、こちらに置き換え）
2. 対処Aの手順2で作った構図に `Align With View` で合わせる
3. `SuikawariGame` の **Pose Console** 欄に、この新しい PoseConsole を割り当て直す

> **なぜ Console ではなく ConsoleAnchor の子にするか**：
> `EndingDirector` が⑦で `console.SetParent(null)` してゲーム機を砂浜に置くため、
> Console の子にするとカメラ位置まで一緒に倒れてしまいます。
> ConsoleAnchor はペンギン側に残るので安全です。

#### 確認

- [ ] ステージ2でゲーム機の画面が**大きく正面に**映る
- [ ] 画面の周囲に**砂浜・パラソル・海**が見えている（入れ子の外側）
- [ ] クリア時、**画面に吸い込まれるズームイン**が途中で消えない
      （消える場合は ViewCamera の **Near Clip = 0.01** を再確認）

---

### E. 波の音にカモメの声を混ぜる

#### 用意するもの

カモメの鳴き声の音声ファイル（`.wav` / `.mp3`）を **1〜3種類**。
同梱の audio フォルダには入っていないので、フリー素材から入手してください。

- 効果音ラボ、OtoLogic、freesound.org など
- **1〜2秒程度の単発の鳴き声**が扱いやすいです
- 複数種類あると単調さが消えます

`Assets/Audio` にドラッグしてインポートしてください。

#### スクリプト

`Assets/Scripts/AmbientSeagulls.cs` を**作成済み**です。

一定間隔で鳴らすと機械的に聞こえるため、次の4点で自然さを出しています。

| 工夫 | 効果 |
|---|---|
| 鳴る間隔をランダム化（6〜18秒） | パターンが読めない |
| ピッチを毎回 ±12% 変える | 別の個体に聞こえる |
| リスナーの周囲にランダム配置 | 方向感が出る（3D音） |
| たまに2〜3回続けて鳴く | 実際のカモメの鳴き方に近い |

#### セットアップ

1. Hierarchy 右クリック → `Create Empty` → 名前を **Seagulls** に
2. `Add Component` → **AmbientSeagulls**
3. 設定

| 項目 | 推奨値 | 意味 |
|---|---|---|
| **Clips** | Size を音声の数に → 各要素に割り当て | 複数入れるとランダムに選ばれます |
| Interval Min / Max | **6 / 18** | 次に鳴くまでの秒数 |
| Volume | **0.35** | 波の音（0.5）より控えめに |
| Pitch Variation | 0.12 | 個体差 |
| Max Burst | 3 | 続けて鳴く最大回数 |
| Distance | 14 | リスナーからの距離 |
| Height | 7 | 上空の高さ |
| **Listener** | **GameplayCamera** | Audio Listener が付いたカメラ。未設定なら `Camera.main` を自動使用 |

> ⚠️ **Listener には Audio Listener が付いたカメラ**を割り当ててください。
> 手順書では **GameplayCamera** に Audio Listener を置いています（§8-2）。
> ここがズレると、音が聞こえない・方向がおかしくなります。

#### 調整の目安

| 狙い | 設定 |
|---|---|
| もっと賑やかに | Interval Min/Max を **4 / 10** に |
| もっと静かに | Interval Min/Max を **12 / 30** に、Volume を 0.25 |
| 遠くの空で鳴かせる | Distance **25**、Height **12**、Volume 0.25 |
| 単発だけにする | Max Burst を **1** に |

#### 撮影時の注意

§9-3 の推奨カットは合計40秒前後です。
Interval が 6〜18秒だと、**動画中に2〜4回**鳴く計算になります。
狙った場所で鳴かせたい場合は、録画前に Interval Min/Max を
**3 / 6** に一時的に縮めて、鳴った瞬間を使う方法もあります。

---

### 第2ラウンドのチェックリスト

- [ ] MelonInside の **Render Face = Both**（断面が見える）
- [ ] 断面が**赤い**（MelonInside が Element 1 に入っている）
- [ ] Global Volume の **Tonemapping を切って**空の黄色が消えるか確認
- [ ] Volume Profile を **Duplicate してバックアップ**
- [ ] StickPivot の **Rotation X を -20** にしてから棒を合わせ直す
- [ ] Play してエディタ上の棒の位置と**一致**する
- [ ] PoseConsole を **ConsoleAnchor の子**にして貼り直す
- [ ] `SuikawariGame` の **Pose Console** を割り当て直す
- [ ] Seagulls オブジェクトに **AmbientSeagulls** + カモメ音声
- [ ] Listener に **GameplayCamera** を割り当て

---

## 付録：カメラ映像をゲーム機の画面に映す仕組み

`NestedScreens.cs` の動作を、デバッグに必要な粒度で解説します。
**「画面に何かおかしいものが映る」系の不具合は、ほぼこの流れのどこかで説明がつきます。**

---

### 全体像

登場人物は3種類のカメラと、複数の RenderTexture です。

```
GameplayCamera ──[描画]──> RT_Game ─┐
（俯瞰・プレイ映像の源）              │
                                    │ 画面マテリアルに貼る
NestCam0 ──[描画]──> RT_Nest0 ──────┤（描画の直前に差し替え）
NestCam1 ──[描画]──> RT_Nest1 ──────┤
   …                                │
ViewCamera ──[描画]──> 実際の画面 ───┘
（プレイヤーが見るカメラ）
```

**肝は「1枚の画面マテリアルを、カメラごとに描画直前で貼り替えている」点**です。
画面が何枚もあるわけではありません。

---

### ステップ1：初期化（`Awake()`）

```csharp
// ① Console のマテリアル群から名前に "ScreenMat" を含むものを探す
foreach (var m in consoleRenderer.materials)
    if (m.name.Contains(screenMaterialName)) { screenMat = m; break; }
if (screenMat == null) screenMat = consoleRenderer.materials[0];   // ← 見つからない時のフォールバック

// ② 画面を自発光させる（暗所でも画面だけ光る）
screenMat.EnableKeyword("_EMISSION");
screenMat.SetColor("_EmissionColor", Color.white * screenBrightness);

// ③ プレイ映像用のRenderTextureを作り、俯瞰カメラの出力先にする
rtGame = new RenderTexture(rtWidth, rtHeight, 24);   // 既定 1280x720
gameplayCamera.targetTexture = rtGame;               // ← これ以降、俯瞰カメラは画面に出ない

// ④ 「各カメラの描画直前」に割り込むフックを登録（URP/Built-in両対応）
RenderPipelineManager.beginCameraRendering += OnBeginCamSRP;
Camera.onPreRender += OnPreRenderBuiltin;
```

#### ここで起きやすい不具合

| 症状 | 原因 |
|---|---|
| **筐体（本体の灰色部分）に映像が乗る** | ①で `ScreenMat` が見つからず、`materials[0]` に貼っている。Console Renderer の割り当てを確認 |
| 画面が白飛びする | ②の Emission と Bloom が二重に効いている。`screenBrightness` を 1.15 以下に |
| 俯瞰カメラの映像が直接画面に出てしまう | ③で `targetTexture` を**手動でも設定**している。Inspector では**空のまま**にする |

> `consoleRenderer.materials`（`sharedMaterials` ではない）を使っているため、
> **実行時にマテリアルの複製が作られます**。プロジェクトの `.mat` アセットは
> 書き換わらないので、Play を止めれば元に戻ります。

---

### ステップ2：ステージごとにカメラの鎖を組む（`SetStage()`）

ステージが深くなるほど、中間カメラが1台ずつ増えます。

```csharp
Texture current = rtGame;
int extra = Mathf.Max(0, stage - 2);      // ステージ1,2→0台 / 3→1台 / 4→2台 / 5→3台

for (int k = 0; k < extra; k++)
{
    var rt  = new RenderTexture(rtWidth, rtHeight, 24);
    var cam = new GameObject($"NestCam{k}").AddComponent<Camera>();
    cam.CopyFrom(viewCamera);                        // ← ViewCameraの設定を丸ごと複製
    cam.targetTexture = rt;
    cam.depth = viewCamera.depth - (extra - k);      // 深い階層から先に描く

    texForCam[cam] = current;   // このカメラが撮る瞬間、画面に映っているべき映像
    current = rt;               // その出力が、次のカメラから見た「画面の中身」になる
}

texForCam[viewCamera]     = current;   // 実表示カメラが見る画面の中身
texForCam[gameplayCamera] = rtGame;    // 俯瞰視点では画面に自映像（合わせ鏡）
```

#### ステージごとの対応

| ステージ | 中間カメラ | ViewCamera が見る画面の中身 | 見た目 |
|---|---|---|---|
| 1 | 0台 | RT_Game | 俯瞰。奥のゲーム機に自映像がループ |
| 2 | 0台 | RT_Game | ゲーム機の画面にプレイ映像 |
| 3 | 1台 | RT_Nest0 | 画面の中にゲーム機、その中にプレイ映像 |
| 4 | 2台 | RT_Nest1 | 入れ子が3段 |
| 5 | 3台 | RT_Nest2 | 入れ子が4段 |

> **`cam.CopyFrom(viewCamera)` の効果**：Near Clip、FOV、Culling Mask などが
> すべて複製されます。**ViewCamera の Near Clip を 0.01 にすれば、
> 中間カメラにも自動で反映されます**（個別に設定する必要はありません）。

`SetNestPose(poseConsole)` が、これらの中間カメラを
**すべて PoseConsole と同じ位置・向き**に置きます。
つまり「ゲーム機を覗き込む視点」を階層の数だけ重ねているわけです。

---

### ステップ3：毎フレーム、カメラごとに画面を貼り替える

ここが仕組みの核心です。

```csharp
void ApplyFor(Camera cam)
{
    if (texForCam.TryGetValue(cam, out var tex)) SetScreenTexture(tex);
}

void SetScreenTexture(Texture t)
{
    screenMat.mainTexture = t;
    if (screenMat.HasProperty("_BaseMap"))     screenMat.SetTexture("_BaseMap", t);
    if (screenMat.HasProperty("_EmissionMap")) screenMat.SetTexture("_EmissionMap", t);
}
```

**1フレームの中で、次の順に処理が走ります**（ステージ4の例）。

```
① GameplayCamera が描画される直前
   → 画面に RT_Game を貼る → 俯瞰映像を RT_Game へ書き出す

② NestCam0 が描画される直前
   → 画面に RT_Game を貼る → 「画面にプレイ映像が映った世界」を RT_Nest0 へ

③ NestCam1 が描画される直前
   → 画面に RT_Nest0 を貼る → 「その入れ子が映った世界」を RT_Nest1 へ

④ ViewCamera が描画される直前
   → 画面に RT_Nest1 を貼る → これが実際に画面に出る
```

同じ1枚のマテリアルを、**カメラの描画と描画のすきまで差し替え続けている**ため、
1フレームの中に複数の異なる「画面の中身」が同居できます。

#### 描画順が重要な理由

`cam.depth`（URPでは Priority）が**小さいカメラから先に描画**されます。

- 中間カメラ：`viewCamera.depth - (extra - k)` → 自動で -3, -2, -1 …
- **GameplayCamera：手動で -10 にする必要があります**

GameplayCamera の depth が高いと、中間カメラが**前フレームの RT_Game**を
参照することになり、階層ごとに遅延が積み重なります。
（1フレーム程度の遅れは「入力遅延の演出」と噛み合うので許容範囲ですが、
 4段重なると目に見えて遅れます）

---

### ステップ4：画面のUV（今回の「映像が横向き」の原因）

RenderTexture は**そのままマテリアルの Base Map として貼られる**ので、
**画面メッシュのUVの向きがそのまま映像の向きになります。**

#### 何が起きていたか

修正前は、画面が**Cubeの1面**として作られていました。

```python
scr = box("screen", (0, -0.0085, 0), (0.125, 0.002, 0.068), SCREEN())
```

Blender の `primitive_cube_add` が作る既定UVは、**面ごとに向きが揃っていません**。
画面に使った面のUVがたまたま90度回っていたため、
**縦横が入れ替わった映像**が表示されていました。

#### 修正内容（適用済み）

画面を**Plane（板）**で作り直し、UVの向きを確定させました。

```python
def screen_plane(name, loc, width, height, material):
    bpy.ops.mesh.primitive_plane_add(size=1.0, location=loc)
    o = bpy.context.object
    o.scale = (width, height, 1.0)                 # 横幅・縦幅を先に決める
    o.rotation_euler = (math.radians(90), 0, 0)    # 法線 +Z → -Y（正面向き）
    bpy.ops.object.transform_apply(rotation=True, scale=True)
    o.data.materials.append(material)
    return o
```

Plane のUVは **U = ローカルX / V = ローカルY** という素直な対応です。
X軸まわりに90度倒すと：

| UV | ローカル軸 | 倒した後のワールド軸 | 意味 |
|---|---|---|---|
| U（横） | X | **X** | 画面の横方向 |
| V（縦） | Y | **Z** | 画面の上方向 |

これで映像の向きが確定します。

#### 反映手順

1. Blender でスクリプトを**再実行**
2. `export/console.fbx` を Unity の `Assets/Models` に**上書きインポート**
3. シーンの Console を**置き換える**（古いメッシュのままだと直りません）
4. `NestedScreens` の **Console Renderer** を割り当て直す
5. **ScreenFocus** の Position を **(0, 0, 0.0096)** に微調整（板の位置に合わせて）

> **再出力せずに直したい場合**：Console の**画面部分だけを Unity の Quad で作り直す**
> 方法もあります。Console の子に `3D Object → Quad` を作り、画面表面に合わせて
> Scale (0.125, 0.068, 1) で配置。マテリアル名に `ScreenMat` を含むものを割り当て、
> `NestedScreens` の **Console Renderer にこの Quad の Renderer** を指定します。
> Unity の Quad はUVが素直なので、これでも正しい向きになります。

#### 縦横比について

RenderTexture は **1280×720（16:9）**、画面は **0.125 × 0.068（≒16:8.7）**で
ほぼ一致しています。ここがずれると映像が引き伸ばされます。
画面サイズを変えた場合は、`rtWidth` / `rtHeight` も合わせてください。

---

### トラブル早見表（映像まわり）

| 症状 | 見るべき場所 |
|---|---|
| 映像が90度回っている | **画面メッシュのUV**（→ 上記の修正） |
| 映像が引き伸ばされる | RT の縦横比と画面メッシュの縦横比の不一致 |
| 筐体に映像が乗る | `ScreenMat` が見つからず `materials[0]` を使っている |
| 画面が真っ黒 | Console Renderer 未割当／GameplayCamera が無効 |
| **ぼやけた景色が映る** | **Depth of Field の Focus Distance**。Global Volume を一時無効化して確認 |
| 映っている内容が違う | GameplayCamera の位置・向き。**Hierarchyで選択するとScene右下にプレビューが出る** |
| 階層が深いと遅れる | GameplayCamera の depth / Priority を **-10** に |
| 深い階層で白飛びする | Bloom Intensity ≤ 0.4、`screenBrightness` ≤ 1.15 |
| ズームインで画面が消える | ViewCamera の **Near Clip = 0.01** |

---

## 追加機能：エンディング演出とマルチエンド

### 新規ファイル

| ファイル | 役割 |
|---|---|
| `GameEnding.cs` | エンディングの種類（enum）と達成状況の記録（`EndingRegistry`） |
| `SpeechBubble.cs` | ペンギンの頭上に出る吹き出し。実行時に生成、プレハブ不要 |

### 4つのエンディング

| エンド | 条件 | タイトル画面での表示 |
|---|---|---|
| **AllClear** | 全5ステージクリア | `WATERMELON PARTY` |
| **PenguinDown** | 仲間を叩いた | `PENGUIN DOWN` |
| **TimeUp** | 時間切れ | `TIME UP` |
| **SwimAway** | **海に近づいた** | `GONE SWIMMING` |

達成状況は **PlayerPrefs** に保存され、ゲームを再起動しても残ります。
タイトル画面の右下に `ENDINGS 2 / 4` と一覧が出て、
**未達成は `☆ ???`** で伏せられます。

---

### Inspector の設定（追加分）

#### GameRoot → SuikawariGame

| 項目 | 既定値 | 意味 |
|---|---|---|
| **Sea Line Z** | **5.5** | このZ座標より奥へ行くと SwimAway エンド。**波打ち際より少し手前**に設定 |
| Swim Away Duration | 3.5 | 泳いで去る演出の秒数 |
| Clear Menu Auto Return | 10 | クリア画面から自動でタイトルへ戻る秒数。**0で自動復帰しない** |

> ⚠️ **Sea Line Z は必ず実測してください。** 海（Ocean）の位置は
> §4-3 で調整しているので、プロジェクトごとに違います。
> Scene ビューで波打ち際のZ座標を読み、**その1〜1.5m手前**に設定します。
> `PenguinPlayerController` の **Area Max の Z（既定7）より小さく**すること。
> 大きいと移動制限に阻まれて永久に到達できません。

#### GameRoot → EndingDirector

| 項目 | 割り当て／値 | 意味 |
|---|---|---|
| **Caller Penguin** | NPC_A など | 吹き出しで呼ぶ1匹。未設定なら AllPenguins[0] |
| **Call Message** | `おーい！` | 吹き出しの文字 |
| **Bubble Height** | 1.1 | 頭上何mに出すか。**ペンギンの身長に合わせる** |
| Bubble Duration | 2.2 | 表示時間 |
| **Japanese Font** | 日本語ttf | **未設定だと日本語が豆腐（□）になります** |
| **Melon To Eat** | `melon_half_L` | 食べる対象。割れたスイカの片方 |
| Eat Count | 4 | ついばむ回数 |

---

### エンディングの流れ（変更後）

```
ステージ5クリア
  ↓
⑥ 7秒かけてゆっくり全景までズームアウト
  ↓
⑦-1 1匹が吹き出しで「おーい！」と呼ぶ（★新規）
     4匹が跳ねて呼応
  ↓
⑦-2 ゲーマーペンギンがゲーム機を砂浜に置く
  ↓
⑦-3 よちよち歩いて輪に合流
  ↓
⑦-4 全員で歓声ジャンプ
  ↓
⑦-5 全員がスイカの方を向いて、順にぴょこぴょこ食べる（★新規）
  ↓
⑧ CLEAR画面（AllClearエンド解放）
  ↓
⑨ 10秒後に自動でタイトルへ（★新規／ボタンでも戻れる）
```

---

### 併せて直した2つの不具合

#### 1. `PenguinIdleBob` の競合（§12-1の未解決課題）

待機モーションが毎フレーム位置を上書きするため、
ジャンプも合流も見えていませんでした。
`EndingDirector.Run()` の冒頭で**待機モーションを止める**ようにしました。

#### 2. エンディング後にシーンが元に戻らない

タイトルへ戻る仕様にしたことで新たに問題になった点です。
エンディングは**シーンを作り変えます**（ゲーマーペンギンが輪へ移動、
ゲーム機が親から外れて砂浜に置かれる）。
そのままタイトルに戻ると、**その状態のままタイトル画面に映ります**。

対処として `EndingDirector` に初期配置の記録・復元を追加しました。

- `Awake()` で全ペンギンの位置・向きと、ゲーム機の親子関係を記録
- `ResetScene()` で復元し、待機モーションを再開
- `SuikawariGame.GoTitle()` から呼ぶ

このとき `PenguinIdleBob` に **`ResetBase()`** を追加しています。
待機モーションは `Start()` 時点の位置を基準にするため、
**基準を取り直さずに再開すると古い位置へ引き戻されてしまう**ためです。

---

### テスト方法

#### 各エンドの出し方

| エンド | 手順 |
|---|---|
| PenguinDown | わざとNPCの近くで Space |
| TimeUp | 10秒放置 |
| **SwimAway** | **W を押しっぱなしで海へ**（Sea Line Z を超えるまで） |
| AllClear | 5ステージクリア |

#### 達成状況をリセットする

GameRoot を選択 → Inspector の **SuikawariGame を右クリック** →
**「エンディング達成状況をリセット」**

PlayerPrefs に保存されているので、**Play を止めても消えません**。
撮影前にリセットして「0 / 4」から見せると、収集要素が伝わります。

---

### 撮影での見せ方（提案）

マルチエンドは**動画の締めに効きます**。§9-3のカット構成に足すなら：

1. タイトル画面で `ENDINGS 1 / 4` を映す（3秒）
2. 海へ歩いて行って `GONE SWIMMING`（4秒）
3. タイトルに戻り `ENDINGS 2 / 4` に増えるのを見せる（2秒）

「集める要素がある」ことが数秒で伝わります。

---

制作の追い込み、がんばってください！🐧🍉


---

# Part 3. エンディング実装手順

## 「Suika」エンディング完成までの作業手順書

現在地の整理と、エンディングを動かすまでにやることをまとめたものです。
`README_手順書.md`（全体の作り方）と `improve.md`（不具合の直し方）の
**続き**にあたります。

---

### 0. 現在地

#### ✅ 動いているところ

| 項目 | 状態 |
|---|---|
| 入力システム | `Both` に設定済み。キー入力が通る |
| ペンギンの向き | 空オブジェクトで包む対処を実施済み。前進・旋回が正しい |
| 棒の構造 | `StickPivot → StickRoot → stick → StickTip` で確定 |
| 羽の分離 | `Flipper_R / Flipper_L` が動く |
| ステージ進行 | **全5ステージクリアまで到達済み**（エンディングに入れている） |
| 入れ子表示 | ゲーム機の画面に映像が出ている |
| 海 | `ShoreWash.cs` を導入済み |

#### 🔧 未完了（エンディングに必要）

| # | 項目 | 原因 | 対応 |
|---|---|---|---|
| 1 | `allPenguins` が未割り当てでエラー | ペンギンを包み直して参照が切れた | **Step 1** |
| 2 | エンディング演出の設定が未入力 | 今回スクリプトを追加したばかり | **Step 2** |
| 3 | 海エンドの境界が未設定 | `Sea Line Z` は要実測 | **Step 3** |

#### 🎨 未完了（見た目・出せるが直したい）

| # | 項目 | 原因 | 対応 |
|---|---|---|---|
| 4 | スイカの中が空洞 | Blenderの `holes_fill` 不具合（**修正済み・要再出力**） | **Step 4** |
| 5 | 画面の映像が90度回転 | 画面メッシュのUV（**修正済み・要再出力**） | **Step 4** |
| 6 | 画面がぼやける／内容が違う | Depth of Field か ScreenMat | **Step 5** |
| 7 | ステージ2で画面位置がずれる | 包み直しで座標が変わった | **Step 5** |
| 8 | 棒の判定が厳しい | 振り下ろし角55°が肩ピボットに合わない | **Step 6** |

---

### 作業順（この順でやってください）

```
Step 1  参照の割り当て直し        30分  ← これをやらないとエンディングが動かない
Step 2  エンディングの設定        20分
Step 3  海エンドの境界を測る      10分
Step 4  Blender再出力            30分
Step 5  画面まわりの調整          30分
Step 6  棒の判定調整              10分
Step 7  通しテスト                30分
```

**Step 1〜3 でエンディングが動きます。** Step 4以降は見栄えの改善なので、
時間がなければ後回しにできます。

---

## Step 1：参照の割り当て直し（最優先・30分）

ペンギンを空オブジェクトで包み直したため、**Inspector の参照が切れています**。
1つ直すたびに次のエラーで止まるので、**まとめて全部埋めてください**。

### 1-1. GameRoot → SuikawariGame

| 欄 | 割り当てるもの |
|---|---|
| Player | **新しい親の PlayerPenguin**（`PenguinPlayerController` が付いている方） |
| Melon Whole | Melon |
| Melon Half L | **melon_half_L**（MelonHalves の**子**） |
| Melon Half R | **melon_half_R**（MelonHalves の**子**） |
| **Npc Penguins** | Size **4** → **新しい親の** NPC_A / NPC_B / NPC_C / NPC_D |
| Nested | NestedScreens |
| Ending | GameRoot（自分自身） |
| Pose Wide / Gameplay / Console / Screen Focus | 各空オブジェクト |
| サウンド6種 | swing / crack / hit_fail / clear / gameover / cheer |
| **Game Title** | **`Suika`**（新規追加。既定でこの値） |
| Japanese Font | 日本語ttf（吹き出しに必要） |

### 1-2. GameRoot → EndingDirector

| 欄 | 割り当てるもの |
|---|---|
| Gamer Penguin | **新しい親の GamerPenguin** |
| Console | **Console 本体**（ConsoleAnchor ではない） |
| Join Point | JoinPoint |
| **All Penguins** | Size **6**（順番厳守・下記） |
| Audio Src | GameRoot の AudioSource |
| Se Cheer | cheer |

> ⚠️ **All Penguins の順番**
>
> ```
> 0: NPC_A   1: NPC_B   2: NPC_C   3: NPC_D   4: GamerPenguin   5: PlayerPenguin
> ```
>
> `EndingDirector` は**先頭4体を「呼ぶ側」**として扱います
> （`SubArray(allPenguins, 0, 4)`）。入れ違うとゲーマーペンギンが
> 自分自身を呼ぶ動きになります。
> **すべて「新しい親」の方**を入れてください（メッシュの子ではありません）。

### 1-3. PlayerPenguin → PenguinPlayerController

| 欄 | 割り当てるもの |
|---|---|
| Stick Pivot | StickPivot |
| Stick Tip | StickTip |

### 1-4. NestedScreens

| 欄 | 確認 |
|---|---|
| Gameplay Camera | GameplayCamera |
| View Camera | ViewCamera |
| Console Renderer | **Console の MeshRenderer**（子にある場合はその子） |

### 1-5. 確認方法

各オブジェクトを選択し、Inspector を上から見て
**「None (○○)」や「Missing」の欄が1つも無い**ことを確認します。

> **Play せずに確認する裏技**：Hierarchy の検索欄に `t:PenguinIdleBob` などと
> 入れると、そのコンポーネントを持つオブジェクトだけが絞り込まれます。
> 5体すべてに付いているかの確認に使えます。

---

## Step 2：エンディング演出の設定（20分）

今回追加した吹き出しと「スイカを食べる場面」の設定です。

### 2-1. EndingDirector の追加項目

| 欄 | 設定値 | 備考 |
|---|---|---|
| **Caller Penguin** | **NPC_A** | 吹き出しで呼ぶ1匹。未設定なら AllPenguins[0] |
| **Call Message** | `おーい！` | 好きな文字に変更可 |
| **Bubble Height** | **1.1** | 頭上何mに出すか。**ペンギンの身長に合わせる** |
| Bubble Duration | 2.2 | 表示時間（秒） |
| **Japanese Font** | **日本語ttf** | ⚠️ 未設定だと「おーい！」が□になります |
| **Melon To Eat** | **melon_half_L** | 食べる対象。割れたスイカの片方 |
| Eat Count | 4 | ついばむ回数 |

### 2-2. 日本語フォントの用意

まだ入れていない場合：

1. 日本語の `.ttf` を用意（Noto Sans JP、M PLUS など。商用利用可のもの）
2. `Assets/Fonts` フォルダを作ってドラッグ
3. **SuikawariGame の Japanese Font** と
   **EndingDirector の Japanese Font** の**両方**に割り当て

> 英語のままでよければ `Call Message` を `Hey!` などにすれば
> フォント未設定でも表示されます。

### 2-3. 吹き出しの高さを合わせる

`Bubble Height` はペンギンの身長に依存します。

1. Play してエンディングまで進める（時間がかかる場合は Step 2-4 参照）
2. 吹き出しが**頭の上に浮いているか**確認
3. 低すぎる → 値を上げる／高すぎる → 下げる

### 2-4. エンディングだけを早くテストする方法

毎回5ステージ遊ぶのは大変なので、**一時的にステージ数を減らします**。

1. SuikawariGame の **Total Stages を `1`** にする
2. Play → 1ステージ クリアするだけでエンディングに入る
3. 演出を確認・調整
4. **確認が終わったら必ず `5` に戻す**

---

## Step 3：海エンドの境界を測る（10分）

`Sea Line Z` は**プロジェクトごとに違う**ので、必ず実測してください。

### 3-1. 波打ち際のZ座標を調べる

1. **Play を押す**（海のメッシュは実行時に生成されます）
2. Scene ビューに切り替え、**真上から**見る
3. 海と砂浜が交差している線のあたりに**マウスを合わせ**、
   Scene ビュー左下に出る座標を読む
   - もしくは、空オブジェクトを一時的に作って波打ち際まで動かし、
     その **Position Z を読む**方法が確実です
4. 読んだ値をメモ（例：`6.8`）

### 3-2. 値を決める

```
Sea Line Z = 波打ち際のZ − 1.0〜1.5
```

例：波打ち際が 6.8 なら **`5.5` 前後**。

**プレイヤーが海に入る直前で発動する**ようにするのが狙いです。
ぴったり波打ち際にすると、水に浸かる前に終わってしまいます。

### 3-3. 到達できるか確認する

| 確認 | 場所 |
|---|---|
| `Sea Line Z` < `Area Max` の Z | PenguinPlayerController の **Area Max**（既定 `(4, 7)` の**7**） |

⚠️ **Sea Line Z が Area Max の Z 以上だと、移動制限に阻まれて
永久に到達できません。** 必ず小さくしてください。

### 3-4. テスト

Play して **W を押しっぱなし**で海へ向かいます。

- 海の方を向いて泳ぎ出せば成功
- 何も起きない → `Sea Line Z` が大きすぎる（Area Max に阻まれている）
- すぐ発動してしまう → `Sea Line Z` が小さすぎる

---

## Step 4：Blender再出力（30分）

**スイカの断面**と**画面のUV**の修正を反映します。
スクリプトは修正済みなので、再実行するだけです。

### 4-1. 再実行

1. Blender で `beach_models.blend` を開く
2. Scripting タブ → 修正済みの `build_beach_models.py` を貼り直す
   （`Assets/Models/build_beach_models.py` が最新版です）
3. **Alt+P** で実行
4. **Window → Toggle System Console** でログを確認

### 4-2. ログで確認すること

```
melon_half_L: 断面 ◯ 面を作成      ← 0 でなければOK
melon_half_R: 断面 ◯ 面を作成
右羽=124面 / 左羽=118面            ← 極端に偏っていなければOK
```

**断面が 0 面なら失敗**です。そのまま教えてください。

### 4-3. Unityへ反映

1. `export/watermelon_halves.fbx` と `export/console.fbx` を
   `Assets/Models` へ**上書きドラッグ**
2. ⚠️ **シーンに置いてある古いオブジェクトは自動で更新されません。**
   - **MelonHalves を削除 → 新しいFBXをドラッグし直す**
   - **Console を削除 → 新しいFBXをドラッグし直す**
3. 置き直したので、**参照を割り当て直す**
   - SuikawariGame の Melon Half L / R
   - EndingDirector の Console / Melon To Eat
   - NestedScreens の Console Renderer
   - ScreenFocus を作り直し（Position `(0, 0, 0.0096)` / Rotation `(0, 180, 0)`）

> **再出力を避けたい場合**（時間がないとき）
> - スイカ：`MelonInside` の **Render Face を Both** にすれば断面が見えます
>   （それでも空洞なら、薄い円盤を子として貼る方法が `improve.md` にあります）
> - 画面：Console の子に Unity の **Quad** を作り、画面表面に配置して
>   `ScreenMat` を割り当て、`Console Renderer` にその Quad を指定

---

## Step 5：画面まわりの調整（30分）

### 5-1. Near Clip Plane（まだなら必須）

1. **ViewCamera** → Camera → **Projection** → **Clipping Planes → Near** を **`0.01`**
2. **GameplayCamera** も同様に

> ゲーム機は幅14.5cmしかなく、カメラは0.3m前後まで寄ります。
> 既定の Near = 0.3 では**ゲーム機が消えます**。
> 入れ子カメラは `CopyFrom(viewCamera)` で複製されるので、
> ViewCamera を直せば全階層に反映されます。

### 5-2. 「ぼやけた景色が映る」の切り分け

**まず GameplayCamera を Hierarchy で選択**してください。
Scene ビューの右下に**カメラプレビュー**が出ます。

| プレビュー | 原因 | 対処 |
|---|---|---|
| ペンギンが正しく映っている | 表示側の問題 | 5-3 へ |
| 変な方向を向いている | カメラの位置ずれ | Position `(0, 3.0, -0.8)` / Rotation `(38, 0, 0)` |

### 5-3. Depth of Field を疑う

Global Volume の **Depth of Field を一時的に無効化**して Play。
改善すれば原因はこれです。

- **外してしまう**のが一番簡単
- 残すなら **Focus Distance を 3.0** 前後の中間値に
  （0.35 のままだとステージ1で全部ボケます）

### 5-4. ScreenMat を疑う

筐体（灰色の部分）に映像が乗っている場合、
`NestedScreens` が `ScreenMat` を見つけられず `materials[0]` に貼っています。

1. Console を選択 → Inspector の **Materials** に
   `ScreenMat` を含む名前があるか確認
2. 無ければ、**子オブジェクトの MeshRenderer** を
   `Console Renderer` に割り当て直す

### 5-5. PoseConsole の貼り直し

包み直しでゲーム機のワールド座標が変わっているので、貼り直します。

1. **ConsoleAnchor を右クリック → Create Empty** → 名前 **PoseConsole**
   （ConsoleAnchor の子にすると、今後ペンギンを動かしても追従します）
2. Scene ビューで Console にフォーカス（F）し、
   **画面がフレームの7〜8割**になる構図を作る
   - 正対させず、水平15°・上10°ほどずらすと立体感が出ます
   - 距離は **0.30〜0.35m**
3. PoseConsole を選択して **Ctrl+Shift+F**（Align With View）
4. **SuikawariGame の Pose Console** に割り当て直す

> Console の子ではなく **ConsoleAnchor の子**にするのは、
> エンディングで `console.SetParent(null)` してゲーム機を砂浜に置くとき、
> カメラ位置まで一緒に倒れないようにするためです。

---

## Step 6：棒の判定調整（10分）

現状「当たりにくい」のは、**振り下ろし角55°が肩ピボットに合っていない**ためです。

| ピボット位置 | 55°時の先端 | 前方リーチ |
|---|---|---|
| 手（旧設計） | y -0.43 | **0.30m** |
| 肩（現設計） | y -0.60 | **0.05m** ← ほぼ真下 |

### 対処A：判定半径を上げる（1分・コード変更なし）

SuikawariGame → **Melon Hit Radius** を `0.5` → **`0.75`**

### 対処B：振り下ろし角を浅くする（正しい修正）

`PenguinPlayerController.Swing()` の **55f を 15f に**（2箇所）。

```csharp
yield return RotateStick(-110f, 15f, 0.10f, true);   // 55f → 15f
yield return new WaitForSeconds(0.15f);
yield return RotateStick(15f, -20f, 0.25f);          // 55f → 15f
```

15°なら先端は **y≈0.10 / z≈0.47** で、スイカの高さに近く前方に46cm届きます。

> **この変更はまだ適用していません。** 希望があれば直します。

### 確認

`StickDebugGizmo` を PlayerPenguin に付け、**Scene ビューを見ながら** Play。
🟢 緑の球が 🔵 水色の球（スイカ中心）を飲み込む瞬間があればOKです。

---

## Step 7：通しテスト（30分）

### 7-1. 各エンディングを1回ずつ出す

| エンド | 手順 | 確認 |
|---|---|---|
| **PenguinDown** | NPCの近くで Space | `PENGUIN DOWN` → タイトルへ |
| **TimeUp** | 10秒放置 | `TIME UP` → タイトルへ |
| **SwimAway** | W押しっぱなしで海へ | 泳いで去る → `GONE SWIMMING` |
| **AllClear** | 5ステージクリア | エンディング全編 → `CLEAR!` |

### 7-2. タイトル画面の確認

- [ ] タイトルが **`Suika`** と表示される
- [ ] 右下に **`ENDINGS ○ / 4`** が出る
- [ ] 達成したエンドが **★ + 名前**、未達成が **☆ ???**
- [ ] エンドを出すたびに数が増える

### 7-3. エンディング全編の確認

- [ ] ⑥ 7秒かけてゆっくり全景まで引く
- [ ] ⑦-1 **吹き出しで「おーい！」**が出る（頭上の高さが自然か）
- [ ] ⑦-2 ゲーム機が砂浜に置かれる
- [ ] ⑦-3 ゲーマーペンギンが**よちよち歩いて合流**する
- [ ] ⑦-4 全員でジャンプ
- [ ] ⑦-5 **全員がスイカの方を向いてついばむ**
- [ ] ⑧ `CLEAR!` 画面
- [ ] ⑨ **10秒後に自動でタイトルへ戻る**

### 7-4. ループの確認（重要）

**タイトルに戻ったあと、もう一度遊べるか**を必ず確認してください。

- [ ] ゲーマーペンギンが**パラソルの下に戻っている**
- [ ] ゲーム機が**ペンギンの手に戻っている**（砂浜に落ちたままでない）
- [ ] ペンギンたちが**待機モーションで揺れている**
- [ ] STARTを押すと**普通にステージ1が始まる**

> ここが崩れる場合、`EndingDirector.ResetScene()` が
> 正しく呼ばれていないか、`Awake()` 時点で参照が未割り当てです。
> Step 1 に戻って確認してください。

### 7-5. 達成状況のリセット

撮影前に「0 / 4」から見せたい場合：

GameRoot を選択 → **SuikawariGame を右クリック** →
**「エンディング達成状況をリセット」**

> PlayerPrefs に保存されているので、**Play を止めても消えません。**

---

## 積み残し（出品には必須でないもの）

| 項目 | 状態 | 判断 |
|---|---|---|
| 空が黄色い | 原因未特定（Tonemapping ACES が最有力） | 気に入っているならそのままで可 |
| 波のリアリティ | ShoreWash 導入済み。フォームは未 | 余力があれば |
| カモメの声 | スクリプト作成済み、音声未入手 | フリー素材を入れるだけ |
| ステージセレクト | 未実装 | 締切後で可 |

---

## 最終チェックリスト（提出前）

### 動作
- [ ] タイトル `Suika` が表示される
- [ ] 4つのエンドすべてが出せる
- [ ] エンディング後、タイトルに戻って**もう一度遊べる**
- [ ] ステージ5まで入れ子が深くなる
- [ ] ズームインで画面に吸い込まれる（途中で消えない）

### 見た目
- [ ] スイカの断面が赤い
- [ ] ゲーム機の画面の映像が正しい向き
- [ ] 画面にプレイ映像が映っている（ぼやけていない）
- [ ] 海が横から透けない

### 提出
- [ ] Unity Recorder で **FHD 1080p / 10秒以上**
- [ ] **#Unityサマーチャレンジ** を付けてXに投稿
- [ ] 応募フォーム提出

> **投稿文のヒント**：「画面の中の画面を操作する」の一言が刺さります。
> 入れ子が深くなるほど操作が遅れることと、
> **エンディングが4種類ある**ことも書くと興味を持たれます。

---

がんばってください！🐧🍉
