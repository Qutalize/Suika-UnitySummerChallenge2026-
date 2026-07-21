using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 目隠しペンギン（プレイヤー）の操作。
/// ←→/A・D: 旋回、↑↓/W・S: 前後移動、Space: 棒を振る。
/// ステージが進むほど inputDelay が増え、入力が「遅れて」反映される＝画面の入れ子表現。
///
/// セットアップ:
///  - 目隠しペンギン(penguin_blind)に付ける
///  - 子に空オブジェクト "StickPivot"（肩の位置, 例 localPos (0.25, 0.5, 0)）を作り、
///    その子として stick.fbx を配置（棒の根本がピボットに来るように）
///  - stick の先端に空オブジェクト "StickTip" を作り stickTip に割り当てる
/// </summary>
public class PenguinPlayerController : MonoBehaviour
{
    [Header("参照")]
    public Transform stickPivot;
    public Transform stickTip;

    [Header("移動")]
    public float moveSpeed = 1.4f;
    public float turnSpeed = 140f;
    [Tooltip("移動可能範囲 (X最小, Z最小)")] public Vector2 areaMin = new Vector2(-4f, 0.2f);
    [Tooltip("移動可能範囲 (X最大, Z最大)")] public Vector2 areaMax = new Vector2(4f, 7f);

    [Header("遅延（SuikawariGameが設定）")]
    public float inputDelay = 0f;

    struct Cmd { public float time; public float h, v; public bool swing; }
    readonly Queue<Cmd> queue = new Queue<Cmd>();
    bool swinging;
    float bobPhase;

    void Update()
    {
        // 1) 生の入力を時刻付きでキューへ
        queue.Enqueue(new Cmd
        {
            time = Time.time,
            h = Input.GetAxisRaw("Horizontal"),
            v = Input.GetAxisRaw("Vertical"),
            swing = Input.GetKeyDown(KeyCode.Space)
        });

        // 2) delay 経過した入力だけ取り出して適用
        float h = 0, v = 0; bool swing = false;
        while (queue.Count > 0 && Time.time - queue.Peek().time >= inputDelay)
        {
            var c = queue.Dequeue();
            h = c.h; v = c.v; swing |= c.swing;
        }

        if (swinging) return;

        transform.Rotate(0, h * turnSpeed * Time.deltaTime, 0);
        Vector3 move = transform.forward * (v * moveSpeed * Time.deltaTime);
        Vector3 p = transform.position + move;
        p.x = Mathf.Clamp(p.x, areaMin.x, areaMax.x);
        p.z = Mathf.Clamp(p.z, areaMin.y, areaMax.y);

        // よちよち歩き（移動中だけ上下+ロール）
        if (Mathf.Abs(v) > 0.01f)
        {
            bobPhase += Time.deltaTime * 9f;
            p.y = Mathf.Abs(Mathf.Sin(bobPhase)) * 0.05f;
            transform.rotation = Quaternion.Euler(0, transform.eulerAngles.y, Mathf.Sin(bobPhase) * 6f);
        }
        else p.y = 0f;
        transform.position = p;

        if (swing) StartCoroutine(Swing());
    }

    IEnumerator Swing()
    {
        swinging = true;
        SuikawariGame.I.PlaySwing();

        // 振りかぶる → 振り下ろす → 戻す
        yield return RotateStick(-20f, -110f, 0.18f);         // 振りかぶり
        yield return RotateStick(-110f, 55f, 0.10f, true);    // 振り下ろし（この間ずっと判定）
        yield return new WaitForSeconds(0.15f);
        yield return RotateStick(55f, -20f, 0.25f);           // 構えに戻す
        swinging = false;
    }

    /// <summary>
    /// 棒を from→to へ回す。checkHit=true の間は毎フレーム先端の位置で当たり判定を行う。
    ///
    /// 1フレームだけを見ると、振り下ろしの「終了姿勢(55°)」でしかスイカを判定できず、
    /// 棒がスイカを通過する途中に当たっていても取りこぼす（＝当たったのに割れない、
    /// 逆に見た目が離れているのに当たる）。振り下ろし中を連続で見ることで、
    /// 見た目と判定が一致する。
    ///
    /// 命中/誤爆すると SuikawariGame 側の state が Play 以外へ移るため、
    /// OnSwingImpact を毎フレーム呼んでも二重に成立することはない。
    /// </summary>
    IEnumerator RotateStick(float from, float to, float dur, bool checkHit = false)
    {
        float t = 0;
        while (t < 1f)
        {
            t += Time.deltaTime / dur;
            float a = Mathf.Lerp(from, to, Mathf.SmoothStep(0, 1, Mathf.Clamp01(t)));
            stickPivot.localRotation = Quaternion.Euler(a, 0, 0);
            if (checkHit) SuikawariGame.I.OnSwingImpact(stickTip.position);
            yield return null;
        }
    }

    public void ResetState()
    {
        swinging = false;
        queue.Clear();
        StopAllCoroutines();
        if (stickPivot) stickPivot.localRotation = Quaternion.Euler(-20f, 0, 0);
    }
}
