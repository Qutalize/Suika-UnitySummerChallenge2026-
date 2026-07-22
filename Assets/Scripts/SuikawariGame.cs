using System.Collections;
using UnityEngine;

/// <summary>
/// スイカ割りゲーム全体の進行管理（絵コンテ①〜⑨準拠）。
///
///  ① タイトル [はじめる]
///  ② 導入アニメ（4匹がスイカ割りを楽しむ / 1匹はゲーム中）→ ステージ1
///  ③ ステージ1クリア後、ゲーム機の画面へズームインするアニメ → ステージ2
///  ④⑤ ステージ3〜5: クリアごとにさらに画面内へズーム。周囲に前の風景が残る
///  ⑥ ステージ5クリアで全画面からゆっくりズームアウト（ステージ1の画面まで）
///  ⑦ 4匹に呼ばれてスイカ割りに参加するエンディングアニメ
///  ⑧ クリア画面 [クレジット] [タイトルへ]
///  ⑨ タイトルへ戻る
///  ※ ゲームオーバー時はどのステージでも①タイトルへ戻る
///
/// カメラは viewCamera 1台を3つの定位置(空オブジェクト)の間で動かします:
///  - poseWide     : 浜辺全景（タイトル・導入・エンディング用）
///  - poseGameplay : フィールド俯瞰と同じ構図（ステージ1で使用）
///  - poseConsole  : ゲーム機の画面がほどよく収まる位置（ステージ2以降）
///  - screenFocus  : ゲーム機画面の中心に置く空オブジェクト（ズームインの目標）
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class SuikawariGame : MonoBehaviour
{
    public static SuikawariGame I { get; private set; }

    [Header("シーン参照")]
    public PenguinPlayerController player;
    public GameObject melonWhole;
    public GameObject melonHalfL;
    public GameObject melonHalfR;
    public Transform[] npcPenguins;
    [Tooltip("スイカと間違えて叩いてしまうビーチボール。未設定ならこのエンドは起きない")]
    public Transform beachBall;
    public NestedScreens nested;
    public EndingDirector ending;

    [Header("カメラ定位置")]
    public Transform poseWide;
    public Transform poseGameplay;
    public Transform poseConsole;
    public Transform screenFocus;

    [Header("ゲーム設定")]
    public int totalStages = 5;
    public float timeLimit = 10f;
    public float delayPerStage = 0.18f;
    public float melonHitRadius = 0.5f;
    public float penguinHitRadius = 0.55f;
    [Tooltip("ビーチボールの当たり判定の半径(m)")] public float ballHitRadius = 0.4f;
    [Tooltip("⑥のズームアウトにかける秒数（ゆっくりと）")] public float zoomOutDuration = 7f;

    [Header("マルチエンド")]
    [Tooltip("このZ座標より奥（海側）へ行くと『泳いで行ってしまう』エンド。波打ち際より少し手前に置く")]
    public float seaLineZ = 5.5f;
    [Tooltip("泳いで去っていく演出の秒数")] public float swimAwayDuration = 3.5f;
    [Tooltip("クリア画面を出してからタイトルへ自動で戻るまでの秒数。0なら自動で戻らない")]
    public float clearMenuAutoReturn = 10f;

    [Header("サウンド")]
    public AudioClip seSwing;
    public AudioClip seCrack;
    public AudioClip seHitFail;
    public AudioClip seClear;
    public AudioClip seGameOver;
    public AudioClip seCheer;

    [Tooltip("呼びかけの声。導入の掛け合いで使う（call.wav）")]
    public AudioClip seCall;

    [Header("BGM")]
    [Tooltip("ALL CLEAR画面で1回だけ流す音楽（Assets/Audio/last_sound）")]
    public AudioClip bgmAllClear;
    [Range(0f, 1f)] public float bgmVolume = 0.7f;

    [Header("スイカの割れ方")]
    [Tooltip("割れた半分を何度倒して断面を上に向けるか。" +
             "断面が下を向いてしまう場合は符号を反転させる（-90）")]
    public float melonFaceUpAngle = 90f;
    [Tooltip("左右に分かれる距離(m)。スイカを大きくしたら合わせて広げる")]
    public float melonSplitDistance = 0.33f;
    [Tooltip("割れた半分を砂浜からどれだけ持ち上げるか(m)。埋まって見えるときに増やす")]
    public float melonHalfLift = 0.06f;

    [Header("導入の掛け合い（②）")]
    [Tooltip("話しかけるNPC。未設定なら掛け合いを飛ばす（NPC_B）")]
    public Transform introCaller;
    [Tooltip("ゲームに夢中で気のない返事をするペンギン")]
    public Transform introGamerPenguin;
    [Tooltip("NPCの台詞")] public string introCallMessage = "Let's play!";
    [Tooltip("ゲーマーの返事。ゲームに夢中で上の空という体")]
    public string introGamerReply = "...";
    [Tooltip("吹き出しを出す高さ(m)。EndingDirectorと同じ値にすると揃う")]
    public float introBubbleHeight = 0.8f;
    [Tooltip("吹き出し1つあたりの表示時間(秒)")] public float introBubbleDuration = 2f;

    [Header("撮影モード（動画用）")]
    [Tooltip("ONにすると、ゲームシステム的な文字（タイマー/STAGE/CLEAR/ボタン等）を一切出さず、" +
             "タイトルのスイカ文字を見せた状態からしばらくして自動でステージへ移り、" +
             "指定ステージ分プレイ→エンディング→タイトルへ、を無限にループする。")]
    public bool recordingMode = false;
    [Tooltip("撮影モードで遊ぶステージ数（通常時の totalStages とは別に指定できる）")]
    public int recordingStages = 3;
    [Tooltip("タイトルのスイカ文字を見せてから自動でステージへ移るまでの秒数")]
    public float recordingTitleHold = 10f;

    [Header("UI")]
    [Tooltip("タイトル画面に出すゲーム名")] public string gameTitle = "Suika";
    [Tooltip("日本語フォント(任意)。未設定なら内蔵フォント(英字)")] public Font japaneseFont;

    [Tooltip("CREDITSページの本文。【】の箇所を自分の情報に書き換えてください")]
    [TextArea(12, 30)]
    public string creditsText =
        "「ペンギンたちのスイカ割り」\n" +
        "Unityサマーチャレンジ2026 応募作品\n" +
        "\n" +
        "── 由来 ──\n" +
        "【この作品を作ろうと思ったきっかけを書く】\n" +
        "画面の中の画面を操作する、入れ子構造のスイカ割り。\n" +
        "\n" +
        "── STAFF ──\n" +
        "Game Design / Art / Program ： Quta\n" +
        "\n" +
        "── ASSETS ──\n" +
        "ペンギンモデル ： Seaeees 様（改変して使用）\n" +
        "その他のモデル ： Blenderで自作\n" +
        "効果音・BGM ： 【入手元のサイト名を書く】\n" +
        "\n" +
        "── 参照 ──\n" +
        "【参考にしたページのURLを書く】\n" +
        "\n" +
        "Made with Unity / Blender / Claude\n" +
        "#Unityサマーチャレンジ";

    public int Stage { get; private set; } = 1;
    public float TimeLeft { get; private set; }

    /// <summary>エンディングへ入るまでのステージ数。撮影モードでは短くできる</summary>
    int StagesToClear => recordingMode ? Mathf.Max(1, recordingStages) : totalStages;

    enum State { Title, Intro, Play, Transition, Ending, ClearMenu, GameOver }
    State state;

    AudioSource audioSrc;
    GameHud hud;
    Camera viewCam;

    Vector3 playerStartPos; Quaternion playerStartRot;
    Vector3 melonPos;
    // 割れた半分は演出で倒すので、やり直したときに戻せるよう初期姿勢を控えておく
    Quaternion melonHalfLRot, melonHalfRRot;
    // 叩かれて転がるので、やり直したときに戻せるよう控えておく
    Vector3 ballStartPos; Quaternion ballStartRot;
    AudioSource bgmSrc;

    void Awake()
    {
        I = this;
        audioSrc = GetComponent<AudioSource>();
        hud = GameHud.Create(japaneseFont, gameTitle, creditsText);
        viewCam = nested.viewCamera;

        playerStartPos = player.transform.position;
        playerStartRot = player.transform.rotation;
        melonPos = melonWhole.transform.position;
        melonHalfLRot = melonHalfL.transform.rotation;
        melonHalfRRot = melonHalfR.transform.rotation;
        if (beachBall != null)
        {
            ballStartPos = beachBall.position;
            ballStartRot = beachBall.rotation;
        }

        // エンディング曲は効果音とは別系統で鳴らす（途中でタイトルに戻ったら止めたいため）
        bgmSrc = gameObject.AddComponent<AudioSource>();
        bgmSrc.playOnAwake = false;
        bgmSrc.loop = false;   // ALL CLEAR画面で1回だけ流す
        bgmSrc.volume = bgmVolume;
        bgmSrc.spatialBlend = 0f;   // BGMなので定位させない
    }

    void Start() => GoTitle();

    // ================= ① タイトル =================
    void GoTitle()
    {
        StopAllCoroutines();
        if (bgmSrc) bgmSrc.Stop();   // エンディング曲をタイトルへ持ち越さない
        state = State.Title;
        Stage = 1;
        ResetField();
        if (ending) ending.ResetScene();   // エンディングで動かした浜辺を元に戻す
        player.enabled = false;
        hud.SetTimerVisible(false);
        nested.SetStage(1);
        SnapCamera(poseWide);
        StartCoroutine(hud.Fade(0f, 0.6f));

        if (recordingMode)
        {
            // 撮影モード：ボタンは出さずスイカ文字だけ見せ、少し経ったら自動で始める
            hud.ShowTitle(null, showButtons: false);
            StartCoroutine(RecordingAutoStart());
        }
        else
        {
            hud.ShowTitle(() => StartCoroutine(IntroRoutine()));
        }
    }

    /// <summary>撮影モード：タイトルを見せてから自動でステージへ移る</summary>
    IEnumerator RecordingAutoStart()
    {
        yield return new WaitForSeconds(recordingTitleHold);
        yield return IntroRoutine();
    }

    // ================= ② 導入アニメ =================
    IEnumerator IntroRoutine()
    {
        state = State.Intro;
        hud.HideMenus();
        audioSrc.PlayOneShot(seCheer);
        StartCoroutine(HopGroup(1.8f, 0.18f));                 // 4匹がスイカ割りを楽しむ

        yield return IntroTalk();                              // 誘われても上の空なゲーマー

        yield return MoveCamera(poseWide, poseGameplay, 2.6f); // ゲーム開始の構図へ
        StartCoroutine(RunStage());
    }

    /// <summary>
    /// 導入の掛け合い。NPCが「一緒に遊ぼう」と誘うが、ゲーマーペンギンは
    /// ゲームに夢中で「…」としか返さない。
    /// このあと画面の中へ潜っていく主役が誰なのかを、ここで印象づける。
    /// 演出はエンディングの呼びかけ（EndingDirector）と同じ吹き出しを使う。
    /// </summary>
    IEnumerator IntroTalk()
    {
        if (introCaller == null || introGamerPenguin == null) yield break;

        yield return new WaitForSeconds(0.6f);

        // 1) NPCが声をかける
        SpeechBubble.Show(introCaller, introCallMessage,
                          introBubbleHeight, japaneseFont, introBubbleDuration);
        if (seCall != null) audioSrc.PlayOneShot(seCall);
        yield return new WaitForSeconds(introBubbleDuration * 0.8f);

        // 2) 少し間を置いてから返す。すぐ返さないことで「気づいていない」感じが出る
        SpeechBubble.Show(introGamerPenguin, introGamerReply,
                          introBubbleHeight, japaneseFont, introBubbleDuration);
        yield return new WaitForSeconds(introBubbleDuration * 0.9f);
    }

    // ================= ステージ進行 =================
    IEnumerator RunStage()
    {
        state = State.Intro;
        ResetField();
        player.enabled = false;
        player.inputDelay = delayPerStage * (Stage - 1);
        nested.SetStage(Stage);
        nested.SetNestPose(poseConsole);
        SnapCamera(Stage == 1 ? poseGameplay : poseConsole);

        // 撮影モードでは STAGE 表示を出さず、短い間だけ置いて始める
        if (recordingMode)
        {
            yield return new WaitForSeconds(0.6f);
        }
        else
        {
            hud.ShowMessage($"STAGE {Stage}" + (Stage > 1 ? $"\nDELAY {player.inputDelay:0.00}s" : ""), 2.0f);
            yield return new WaitForSeconds(2.2f);
        }

        state = State.Play;
        player.enabled = true;
        if (!recordingMode) hud.SetTimerVisible(true);
        TimeLeft = timeLimit;
        while (state == State.Play)
        {
            // 撮影モードでは制限時間・海エンドとも無効。スイカを割るまで自由に遊べる
            if (!recordingMode)
            {
                TimeLeft -= Time.deltaTime;
                hud.SetTimer(TimeLeft, Stage);

                if (TimeLeft <= 0f) Fail(GameEnding.TimeUp);
                // 海に入ってしまうと、そのまま泳いで行ってしまう
                else if (player.transform.position.z > seaLineZ) Fail(GameEnding.SwimAway);
            }

            yield return null;
        }
    }

    public void OnSwingImpact(Vector3 tipPos)
    {
        if (state != State.Play) return;
        foreach (var p in npcPenguins)
        {
            Vector3 c = p.position + Vector3.up * 0.4f;
            if (Vector3.Distance(tipPos, c) < penguinHitRadius) { Fail(GameEnding.PenguinDown); return; }
        }
        // スイカと同じくらいの大きさの丸いものが浜辺にもう1つある。
        // 目隠しのまま叩くと、当然こちらに当たることもある
        if (beachBall != null &&
            Vector3.Distance(tipPos, beachBall.position) < ballHitRadius)
        {
            StartCoroutine(BounceBall());
            Fail(GameEnding.BallHit);
            return;
        }
        if (Vector3.Distance(tipPos, melonPos + Vector3.up * 0.15f) < melonHitRadius)
            StartCoroutine(ClearRoutine());
    }

    public void PlaySwing() => audioSrc.PlayOneShot(seSwing);

    // ================= ③④⑤ クリア → ズームイン遷移 =================
    IEnumerator ClearRoutine()
    {
        state = State.Transition;
        player.enabled = false;
        hud.SetTimerVisible(false);

        SplitMelon();
        audioSrc.PlayOneShot(seCrack);
        yield return new WaitForSeconds(0.4f);

        // スイカを割った瞬間の手応えはどのステージでも同じにする。最終ステージも
        // ここは STAGE CLEAR で、ALL CLEAR はスイカを食べ終えてから出す。
        // seClear と seCheer を同時に鳴らすと音が団子になり、
        // さらにエンディングでも歓声が続くため単調になる。ここは seClear だけにする。
        audioSrc.PlayOneShot(seClear);
        // 撮影モードでは「STAGE CLEAR」の文字を出さない（音だけ）
        if (!recordingMode) hud.ShowMessage("STAGE CLEAR!", 1.6f);
        StartCoroutine(HopGroup(1.4f, 0.2f));
        yield return new WaitForSeconds(1.8f);

        if (Stage >= StagesToClear)
        {
            yield return ZoomOutAndEnding();   // ⑥⑦⑧
            yield break;
        }

        // --- ゲーム機の画面へズームイン ---
        if (Stage == 1)
            yield return MoveCamera(poseGameplay, poseConsole, 2.2f); // ③ パラソルのゲーム機へ

        // 画面中心へ突っ込む（画面が視界いっぱいになる）
        yield return DollyInto(screenFocus, 1.5f);
        yield return hud.Fade(1f, 0.18f);

        Stage++;
        StartCoroutine(RunStage());            // RunStage内でposeConsoleへスナップ
        yield return hud.Fade(0f, 0.35f);
    }

    // ================= ⑥⑦⑧ ズームアウト → エンディング → クリア画面 =================
    IEnumerator ZoomOutAndEnding()
    {
        state = State.Ending;
        // ⑥ すべての画面からゆっくりズームアウト（入れ子が画面内に残ったまま引く）
        hud.ShowMessage("", 0f);
        yield return MoveCamera(poseConsole, poseWide, zoomOutDuration);

        // ⑦ 呼ばれて合流し、みんなでスイカを食べるアニメ
        bool done = false;
        ending.Play(() => done = true);
        while (!done) yield return null;
        yield return new WaitForSeconds(1.0f);

        // 撮影モード：ALL CLEAR画面もBGMも実績記録も出さず、静かにタイトルへ戻ってループ
        if (recordingMode)
        {
            yield return FadeToTitle();
            yield break;
        }

        EndingRegistry.Unlock(GameEnding.AllClear);   // トゥルーエンド達成

        // ⑧ ALL CLEAR画面 → ⑨ タイトルへ
        state = State.ClearMenu;

        // 全ステージ制覇のごほうび曲。ALL CLEAR の表示と同時に、1回だけ流す
        if (bgmAllClear != null)
        {
            bgmSrc.clip = bgmAllClear;
            bgmSrc.volume = bgmVolume;
            bgmSrc.Play();
        }

        hud.ShowClearMenu(() => { StartCoroutine(FadeToTitle()); });

        // 放置しても自動でタイトルへ戻る（展示・動画撮影で止まらないように）
        if (clearMenuAutoReturn > 0f)
            StartCoroutine(AutoReturnToTitle(clearMenuAutoReturn));
    }

    IEnumerator AutoReturnToTitle(float delay)
    {
        float t = 0f;
        while (t < delay)
        {
            t += Time.deltaTime;
            if (state != State.ClearMenu) yield break;   // ボタンで戻った場合は何もしない
            yield return null;
        }
        if (state == State.ClearMenu) yield return FadeToTitle();
    }

    IEnumerator FadeToTitle()
    {
        yield return hud.Fade(1f, 0.5f);
        GoTitle();
    }

    // ================= ゲームオーバー（→①タイトルへ） =================
    void Fail(GameEnding ending)
    {
        if (state != State.Play) return;
        // 撮影モードでは失敗させない（仲間やボールを叩いても止めない）。
        // ゲームオーバーの文字も出さず、撮影が途切れないようにする
        if (recordingMode) return;
        state = State.GameOver;
        EndingRegistry.Unlock(ending);          // タイトル画面の「達成したエンド」に記録
        StartCoroutine(FailRoutine(ending));
    }

    IEnumerator FailRoutine(GameEnding ending)
    {
        player.enabled = false;
        hud.SetTimerVisible(false);

        // 海へ向かった場合は、泳いで去っていく様子を見せてから終わる
        if (ending == GameEnding.SwimAway)
        {
            audioSrc.PlayOneShot(seGameOver);
            yield return SwimAwayRoutine();
        }
        else
        {
            // 「叩いてはいけないものを叩いた」系は専用の失敗音にする
            bool wrongHit = ending == GameEnding.PenguinDown || ending == GameEnding.BallHit;
            audioSrc.PlayOneShot(wrongHit ? seHitFail : seGameOver);
        }

        hud.ShowMessage($"{EndingRegistry.Title(ending)}\n{EndingRegistry.Description(ending)}", 2.4f);
        yield return new WaitForSeconds(2.6f);
        yield return hud.Fade(1f, 0.6f);
        GoTitle();
    }

    /// <summary>海に入ってしまったプレイヤーが、そのまま泳いで沖へ去っていく</summary>
    IEnumerator SwimAwayRoutine()
    {
        Transform p = player.transform;

        // 1) 海の方（+Z）を向く
        Quaternion from = p.rotation;
        Quaternion to = Quaternion.LookRotation(Vector3.forward, Vector3.up);
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / 0.5f;
            p.rotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0, 1, Mathf.Clamp01(t)));
            yield return null;
        }

        // 2) 水面でぷかぷか上下しながら沖へ遠ざかる
        Vector3 start = p.position;
        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / swimAwayDuration;
            float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            Vector3 pos = start + Vector3.forward * (e * 14f);
            // 泳ぎ始めると体が水面まで沈み、波で上下する
            pos.y = Mathf.Lerp(start.y, 0.02f, Mathf.Clamp01(t * 3f))
                  + Mathf.Sin(Time.time * 5f) * 0.035f;
            p.position = pos;
            // 泳ぎに合わせて体を左右に振る
            p.rotation = Quaternion.Euler(0f, Mathf.Sin(Time.time * 3f) * 8f, 0f);
            yield return null;
        }
    }

    /// <summary>テスト用：エンディングの達成状況を消す（Inspectorで右クリック）</summary>
    [ContextMenu("エンディング達成状況をリセット")]
    public void ResetEndings()
    {
        EndingRegistry.ResetAll();
        Debug.Log("エンディングの達成状況をリセットしました");
    }

    // ================= 演出パーツ =================
    void SplitMelon()
    {
        melonWhole.SetActive(false);
        melonHalfL.SetActive(true);
        melonHalfR.SetActive(true);
        StartCoroutine(PopHalf(melonHalfL.transform, Vector3.left));
        StartCoroutine(PopHalf(melonHalfR.transform, Vector3.right));
    }

    /// <summary>
    /// 割れた半分を外へ弾きつつ、断面（切り口）が上を向くように倒す。
    ///
    /// 半分は左右(ワールドX)に分かれるので、割れた直後の断面は横を向いている。
    /// ワールドZ軸まわりに90°倒すと ±X の断面が +Y（真上）を向く。
    /// 左半分と右半分では倒す向きが逆になるため、dir から符号を決める。
    /// </summary>
    IEnumerator PopHalf(Transform h, Vector3 dir)
    {
        Vector3 start = melonPos;
        // 倒すと断面が下端になるぶん砂に埋まって見えるので、少し持ち上げて着地させる
        Vector3 end = melonPos + dir * melonSplitDistance + Vector3.up * melonHalfLift;

        float side = Vector3.Dot(dir, Vector3.right);            // 左 -1 / 右 +1
        Quaternion from = h.rotation;
        // モデル本来の向きを保ったまま、ワールドZ軸で倒す
        Quaternion to = Quaternion.Euler(0f, 0f, -side * melonFaceUpAngle) * from;

        float t = 0;
        while (t < 1f)
        {
            t += Time.deltaTime / 0.35f;
            float e = 1f - (1f - t) * (1f - t);
            h.position = Vector3.Lerp(start, end, e) + Vector3.up * (0.35f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t)));
            h.rotation = Quaternion.Slerp(from, to, e);
            yield return null;
        }
        h.rotation = to;   // 断面が上を向いた状態で静止させる
    }

    /// <summary>
    /// 叩かれたビーチボールが弾んで転がっていく。
    /// 当たったのに何も起きないと「判定が正しいのか」が伝わらないため、
    /// 短くても手応えのある反応を返す。
    /// </summary>
    IEnumerator BounceBall()
    {
        Transform b = beachBall;
        Vector3 start = b.position;
        // 叩かれた方向（プレイヤーから見て奥）へ転がす
        Vector3 away = (start - player.transform.position); away.y = 0f;
        away = away.sqrMagnitude > 0.0001f ? away.normalized : Vector3.forward;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / 1.1f;
            float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            Vector3 pos = start + away * (e * 1.8f);
            // 2回弾んでから落ち着く
            pos.y = start.y + Mathf.Abs(Mathf.Sin(e * Mathf.PI * 2f)) * 0.45f * (1f - e);
            b.position = pos;
            b.Rotate(Vector3.right, 360f * Time.deltaTime, Space.World);
            yield return null;
        }
    }

    IEnumerator HopGroup(float duration, float height)
    {
        float t = 0;
        var basePos = new Vector3[npcPenguins.Length];
        for (int i = 0; i < npcPenguins.Length; i++) basePos[i] = npcPenguins[i].position;
        while (t < duration)
        {
            t += Time.deltaTime;
            for (int i = 0; i < npcPenguins.Length; i++)
                npcPenguins[i].position = basePos[i] +
                    Vector3.up * Mathf.Abs(Mathf.Sin((t * 3.2f + i * 0.4f) * Mathf.PI)) * height;
            yield return null;
        }
        for (int i = 0; i < npcPenguins.Length; i++) npcPenguins[i].position = basePos[i];
    }

    void ResetField()
    {
        melonWhole.SetActive(true);
        melonHalfL.SetActive(false);
        melonHalfR.SetActive(false);
        // 前回倒した分を戻さないと、やり直すたびに90°ずつ回ってしまう
        melonHalfL.transform.rotation = melonHalfLRot;
        melonHalfR.transform.rotation = melonHalfRRot;
        // 転がっていったボールも定位置へ
        if (beachBall != null) beachBall.SetPositionAndRotation(ballStartPos, ballStartRot);
        player.transform.SetPositionAndRotation(playerStartPos, playerStartRot);
        player.ResetState();
    }

    // ================= カメラ移動 =================
    void SnapCamera(Transform pose) =>
        viewCam.transform.SetPositionAndRotation(pose.position, pose.rotation);

    IEnumerator MoveCamera(Transform from, Transform to, float dur)
    {
        float t = 0;
        while (t < 1f)
        {
            t += Time.deltaTime / dur;
            float e = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t));
            viewCam.transform.position = Vector3.Lerp(from.position, to.position, e);
            viewCam.transform.rotation = Quaternion.Slerp(from.rotation, to.rotation, e);
            yield return null;
        }
    }

    /// <summary>画面中心(screenFocus)へ加速しながら突入するドリー</summary>
    IEnumerator DollyInto(Transform focus, float dur)
    {
        Vector3 fromP = viewCam.transform.position;
        Quaternion fromR = viewCam.transform.rotation;
        // 画面の少し手前(2cm)まで寄る
        Vector3 toP = focus.position + focus.forward * -0.02f;
        Quaternion toR = Quaternion.LookRotation(focus.forward, Vector3.up);
        float t = 0;
        while (t < 1f)
        {
            t += Time.deltaTime / dur;
            float e = t * t * t;   // 加速（吸い込まれる感じ）
            viewCam.transform.position = Vector3.Lerp(fromP, toP, Mathf.Clamp01(e));
            viewCam.transform.rotation = Quaternion.Slerp(fromR, toR, Mathf.Clamp01(t * t));
            yield return null;
        }
    }
}
