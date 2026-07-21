using System.Collections;
using UnityEngine;

/// <summary>
/// 全ステージクリア後のエンディング。
/// パラソル下のゲーマーペンギンがゲーム機を置き、仲間に呼ばれてスイカ割りの輪へ合流、
/// 全員でぴょんぴょん跳ねて終わり。
///
/// セットアップ: GameRoot等に付け、Inspectorで参照を割り当てる。
///  - gamerPenguin : パラソル下のペンギン
///  - console      : ペンギンが持っているゲーム機（合流時に砂浜へ置かれる）
///  - joinPoint    : 輪の中の合流位置（空オブジェクトを melon の手前に置く）
///  - allPenguins  : 観客4匹 + ゲーマー + 目隠しペンギン（最後に全員で跳ねる）
/// </summary>
public class EndingDirector : MonoBehaviour
{
    public Transform gamerPenguin;
    public Transform console;
    public Transform joinPoint;
    public Transform[] allPenguins;
    public AudioSource audioSrc;
    public AudioClip seCheer;

    [Header("呼びかけの吹き出し")]
    [Tooltip("誰が呼ぶか。未設定なら allPenguins[0] が呼びます")]
    public Transform callerPenguin;
    [Tooltip("吹き出しに出す文字")] public string callMessage = "おーい！";
    [Tooltip("呼びかけの音。未設定なら seCheer を使う（call.wav を割り当てる）")]
    public AudioClip seCall;
    [Tooltip("吹き出しを出す高さ(m)。ペンギンの身長に合わせる")] public float bubbleHeight = 1.1f;
    [Tooltip("吹き出しの表示時間(秒)")] public float bubbleDuration = 2.2f;
    [Tooltip("日本語フォント(任意)。未設定なら英字。SuikawariGameと同じものを割当")]
    public Font japaneseFont;

    [Header("向きの補正")]
    [Tooltip("ゲーマーペンギンの正面と +Z のズレ(度)。真正面を向くときの Rotation Y を入れる")]
    public float gamerYawOffset = -90f;
    [Tooltip("その他のペンギンの正面と +Z のズレ(度)。包み直し済みなら 0")]
    public float penguinYawOffset = 0f;

    [Header("スイカを食べる場面")]
    [Tooltip("割れたスイカ（左）")] public Transform melonToEat;
    [Tooltip("割れたスイカ（右）")] public Transform melonToEat2;
    [Tooltip("スイカを囲む輪の半径(m)")] public float eatCircleRadius = 0.85f;
    [Tooltip("何回ついばむか")] public int eatCount = 6;
    [Tooltip("食べる音（もぐもぐ／しゃりしゃり）")] public AudioClip seEat;
    [Tooltip("スイカが消えるまでの秒数")] public float melonVanishDuration = 1.2f;

    // --- エンディングはシーンを作り変えてしまうため、開始前の状態を控えておく ---
    Vector3[] penguinPos; Quaternion[] penguinRot;
    Transform consoleParent; Vector3 consoleLocalPos; Quaternion consoleLocalRot;
    Vector3 melonScale, melonScale2;
    bool captured;

    /// <summary>そのペンギンの「正面と +Z のズレ」を返す</summary>
    float YawOffsetFor(Transform p)
    {
        return (p != null && p == gamerPenguin) ? gamerYawOffset : penguinYawOffset;
    }

    /// <summary>ズレを考慮して target の方を向かせる</summary>
    void FaceTowards(Transform p, Vector3 target)
    {
        Vector3 dir = target - p.position; dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        p.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(0f, YawOffsetFor(p), 0f);
    }

    void Awake() => CaptureInitialState();

    /// <summary>タイトルへ戻ったときに元通りにできるよう、初期配置を記録する</summary>
    void CaptureInitialState()
    {
        if (captured || allPenguins == null) return;

        penguinPos = new Vector3[allPenguins.Length];
        penguinRot = new Quaternion[allPenguins.Length];
        for (int i = 0; i < allPenguins.Length; i++)
        {
            if (allPenguins[i] == null) continue;
            penguinPos[i] = allPenguins[i].position;
            penguinRot[i] = allPenguins[i].rotation;
        }

        if (console != null)
        {
            consoleParent   = console.parent;
            consoleLocalPos = console.localPosition;
            consoleLocalRot = console.localRotation;
        }

        // スイカは食べ終わりに縮めて消すので、元の大きさを覚えておく
        if (melonToEat  != null) melonScale  = melonToEat.localScale;
        if (melonToEat2 != null) melonScale2 = melonToEat2.localScale;

        captured = true;
    }

    /// <summary>
    /// エンディング後にタイトルへ戻るとき、浜辺を最初の状態に戻す。
    /// これを呼ばないと、ゲーマーペンギンが輪の中に残り、
    /// ゲーム機が砂浜に置かれたままタイトル画面に映ってしまう。
    /// </summary>
    public void ResetScene()
    {
        StopAllCoroutines();
        if (!captured) return;

        for (int i = 0; i < allPenguins.Length; i++)
        {
            if (allPenguins[i] == null) continue;
            allPenguins[i].SetPositionAndRotation(penguinPos[i], penguinRot[i]);
        }

        if (console != null)
        {
            console.SetParent(consoleParent);
            console.localPosition = consoleLocalPos;
            console.localRotation = consoleLocalRot;
        }

        // 食べて消したスイカの大きさを元に戻す。
        // これを忘れると、2周目以降スイカが Scale 0 のまま見えなくなる。
        // （表示/非表示は SuikawariGame.ResetField() が管理するのでここでは触らない）
        if (melonToEat  != null) melonToEat.localScale  = melonScale;
        if (melonToEat2 != null) melonToEat2.localScale = melonScale2;

        // 待機モーションを再開する。位置を戻したあとに基準を取り直さないと、
        // PenguinIdleBob が古い基準位置へ引き戻してしまう
        foreach (var p in allPenguins)
        {
            if (p == null) continue;
            var bob = p.GetComponent<PenguinIdleBob>();
            if (bob) { bob.ResetBase(); bob.enabled = true; }
        }
    }

    public void Play(System.Action onDone) => StartCoroutine(Run(onDone));

    IEnumerator Run(System.Action onDone)
    {
        // 待機モーションが位置を上書きしてしまうため、演出中は止める
        SetIdleBobs(false);

        yield return new WaitForSeconds(1.0f);

        // 1) 仲間が吹き出しで呼ぶ（1匹が声をかけ、4匹が跳ねて呼応する）
        Transform caller = callerPenguin != null ? callerPenguin
                         : (allPenguins.Length > 0 ? allPenguins[0] : null);
        if (caller != null)
        {
            SpeechBubble.Show(caller, callMessage, bubbleHeight, japaneseFont, bubbleDuration);
            // 吹き出しと同時に鳴らす。呼びかけ音は clear/cheer とは別の音にして、
            // 「クリアの音がまた鳴った」と感じさせない
            AudioClip call = seCall != null ? seCall : seCheer;
            if (audioSrc && call) audioSrc.PlayOneShot(call);
        }

        yield return Hop(SubArray(allPenguins, 0, 4), 2, 0.15f);
        yield return new WaitForSeconds(0.5f);

        // 2) ゲーム機を砂浜に置く
        if (console)
        {
            console.SetParent(null);
            Vector3 down = console.position; down.y = 0.06f;
            float t = 0;
            Vector3 from = console.position;
            Quaternion fromR = console.rotation;
            Quaternion toR = Quaternion.Euler(90, gamerPenguin.eulerAngles.y, 0); // 画面を上に寝かせる
            while (t < 1f)
            {
                t += Time.deltaTime / 0.6f;
                console.position = Vector3.Lerp(from, down, t);
                console.rotation = Quaternion.Slerp(fromR, toR, t);
                yield return null;
            }
        }

        // 3) よちよち合流
        yield return WaddleTo(gamerPenguin, joinPoint.position, 1.3f);

        // 4) 全員で歓声ジャンプ
        // 呼びかけ(上)から数秒しか経っていないため、ここでは歓声を鳴らさない。
        // 歓声は「呼びかけ」と「食べ終わり」の2回に絞ってメリハリを付ける。
        yield return Hop(allPenguins, 4, 0.25f);

        // 5) みんなでスイカを食べる
        yield return EatTogether();

        onDone?.Invoke();
    }

    /// <summary>割れたスイカの中心（2つある場合はその中間）</summary>
    Vector3 MelonCenter()
    {
        if (melonToEat != null && melonToEat2 != null)
            return (melonToEat.position + melonToEat2.position) * 0.5f;
        if (melonToEat != null) return melonToEat.position;
        if (melonToEat2 != null) return melonToEat2.position;
        return Vector3.zero;
    }

    /// <summary>
    /// 全員が割れたスイカのまわりに輪になって集まり、ついばんで、
    /// 最後にスイカが無くなる。
    /// </summary>
    IEnumerator EatTogether()
    {
        if (melonToEat == null && melonToEat2 == null) yield break;

        Vector3 center = MelonCenter();

        // --- 1) スイカを囲むように輪になって集まる ---
        int n = allPenguins.Length;
        int walking = 0;
        for (int i = 0; i < n; i++)
        {
            var p = allPenguins[i];
            if (p == null) continue;

            // 円周上に均等配置。今いる向きに近いスロットへ行くと交差しにくい
            float ang = (i / (float)n) * Mathf.PI * 2f;
            Vector3 slot = center + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * eatCircleRadius;

            walking++;
            StartCoroutine(WaddleThen(p, slot, 1.1f, () => walking--));
        }
        while (walking > 0) yield return null;

        // --- 2) 全員スイカの方を向く ---
        foreach (var p in allPenguins)
            if (p != null) FaceTowards(p, center);
        yield return new WaitForSeconds(0.35f);

        // --- 3) ついばむ ---
        var basePos = new Vector3[n];
        var toMelon = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            if (allPenguins[i] == null) continue;
            basePos[i] = allPenguins[i].position;
            Vector3 d = center - basePos[i]; d.y = 0f;
            toMelon[i] = d.sqrMagnitude > 0.0001f ? d.normalized : Vector3.zero;
        }

        float basePitch = audioSrc != null ? audioSrc.pitch : 1f;
        float dur = eatCount * 0.5f, t = 0f, nextSound = 0f;

        while (t < dur)
        {
            t += Time.deltaTime;

            for (int i = 0; i < n; i++)
            {
                var p = allPenguins[i];
                if (p == null) continue;
                // 個体ごとに位相をずらし、一斉に動かないようにする
                float phase = (t / 0.5f + i * 0.35f) * Mathf.PI;
                float dip = Mathf.Max(0f, Mathf.Sin(phase));
                p.position = basePos[i]
                           + Vector3.down * (dip * 0.06f)       // 頭を下げる
                           + toMelon[i]   * (dip * 0.10f);      // スイカへ寄る
            }

            // 食べる音。少しずつピッチを変えて、複数匹が食べている感じを出す
            if (audioSrc != null && seEat != null && t >= nextSound)
            {
                audioSrc.pitch = Random.Range(0.9f, 1.15f);
                audioSrc.PlayOneShot(seEat, 0.7f);
                nextSound = t + Random.Range(0.28f, 0.5f);
            }
            yield return null;
        }

        if (audioSrc != null) audioSrc.pitch = basePitch;
        for (int i = 0; i < n; i++)
            if (allPenguins[i] != null) allPenguins[i].position = basePos[i];

        // --- 4) スイカが無くなる ---
        yield return VanishMelon();

        // ここではジングルを鳴らさない。
        // 直後に ALL CLEAR 画面の last_sound が始まるため、鳴らすと曲が二重になる。
        // 食べ終わりから last_sound までの静けさが、そのまま曲の前振りになる。
        yield return new WaitForSeconds(0.6f);
    }

    /// <summary>WaddleTo を並行実行するためのラッパー</summary>
    IEnumerator WaddleThen(Transform pen, Vector3 target, float speed, System.Action onDone)
    {
        yield return WaddleTo(pen, target, speed);
        onDone?.Invoke();
    }

    /// <summary>食べ終わったスイカを縮めて消す</summary>
    IEnumerator VanishMelon()
    {
        Transform[] pieces = { melonToEat, melonToEat2 };
        Vector3[] from = { melonScale, melonScale2 };

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.01f, melonVanishDuration);
            float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            for (int i = 0; i < pieces.Length; i++)
            {
                if (pieces[i] == null) continue;
                pieces[i].localScale = Vector3.Lerp(from[i], Vector3.zero, e);
            }
            yield return null;
        }
        foreach (var piece in pieces)
            if (piece != null) piece.gameObject.SetActive(false);
    }

    /// <summary>
    /// PenguinIdleBob は毎フレーム位置を上書きするため、
    /// 演出中は止めないとジャンプも合流も見えなくなる。
    /// </summary>
    void SetIdleBobs(bool on)
    {
        foreach (var p in allPenguins)
        {
            if (p == null) continue;
            var bob = p.GetComponent<PenguinIdleBob>();
            if (bob) bob.enabled = on;
        }
    }

    /// <summary>
    /// よちよち歩いて target まで移動する。
    ///
    /// 高さについて: 以前は「地面 = y 0」と決め打ちしていたため、
    /// メッシュの原点が足元にないペンギン（配置時に y を上げてあるもの）が
    /// 歩き始めた瞬間に地面へ沈み込んでいた。
    /// 現在は「歩き出したときの y」を地面の高さとみなし、そこを基準に上下させる。
    /// </summary>
    IEnumerator WaddleTo(Transform pen, Vector3 target, float speed)
    {
        float groundY = pen.position.y;   // 配置されている高さを地面とみなす
        target.y = 0;
        float phase = 0;
        while (Vector3.Distance(new Vector3(pen.position.x, 0, pen.position.z), target) > 0.15f)
        {
            Vector3 dir = target - pen.position; dir.y = 0;
            if (dir.sqrMagnitude < 0.0001f) break;

            // モデルの正面と +Z のズレを足してから向きを合わせる
            Quaternion look = Quaternion.LookRotation(dir)
                            * Quaternion.Euler(0f, YawOffsetFor(pen), 0f);
            pen.rotation = Quaternion.RotateTowards(pen.rotation, look, 240f * Time.deltaTime);

            phase += Time.deltaTime * 9f;
            // 進行方向は「向いている方向」ではなく目標方向を使う。
            // モデルの正面と +Z がずれていても横歩きにならないようにするため。
            Vector3 p = pen.position + dir.normalized * (speed * Time.deltaTime);
            p.y = groundY + Mathf.Abs(Mathf.Sin(phase)) * 0.06f;
            pen.position = p;
            pen.rotation = Quaternion.Euler(0, pen.eulerAngles.y, Mathf.Sin(phase) * 7f);
            yield return null;
        }
        pen.rotation = Quaternion.Euler(0, pen.eulerAngles.y, 0);
        Vector3 ground = pen.position; ground.y = groundY; pen.position = ground;
    }

    IEnumerator Hop(Transform[] pens, int count, float height)
    {
        var basePos = new Vector3[pens.Length];
        for (int i = 0; i < pens.Length; i++) basePos[i] = pens[i].position;
        float dur = count * 0.45f, t = 0;
        while (t < dur)
        {
            t += Time.deltaTime;
            for (int i = 0; i < pens.Length; i++)
                pens[i].position = basePos[i] +
                    Vector3.up * Mathf.Abs(Mathf.Sin((t / 0.45f + i * 0.12f) * Mathf.PI)) * height;
            yield return null;
        }
        for (int i = 0; i < pens.Length; i++) pens[i].position = basePos[i];
    }

    Transform[] SubArray(Transform[] arr, int start, int len)
    {
        len = Mathf.Min(len, arr.Length - start);
        var r = new Transform[len];
        System.Array.Copy(arr, start, r, 0, len);
        return r;
    }
}
