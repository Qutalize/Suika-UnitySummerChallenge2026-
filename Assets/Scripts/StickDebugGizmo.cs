using UnityEngine;

/// <summary>
/// 棒の当たり判定を Scene ビューに可視化するデバッグ用スクリプト（開発中のみ使用）。
///
/// 「棒が当たって見えるのに割れない」「離れているのに当たる」といった
/// 判定のズレは、ほぼ StickTip の位置ズレが原因です。
/// このスクリプトを PlayerPenguin に付けると、
///   ・緑の球 = スイカに当たる範囲 (melonHitRadius)
///   ・赤の球 = ペンギンに当たる範囲 (penguinHitRadius)
///   ・黄の線 = StickPivot から StickTip への「棒の芯」
/// が Scene ビューに表示され、見た目と判定が一致しているか目視できます。
///
/// 使い方:
///  1. PlayerPenguin に Add Component
///  2. Scene ビューを開いたまま Play（Game ビューではなく Scene ビューを見ます）
///  3. Space で振り、緑の球がスイカに重なる瞬間があるか確認
///
/// 完成後はコンポーネントを削除するか、チェックを外してください。
/// （OnDrawGizmos はエディタ専用なのでビルドには影響しません）
/// </summary>
[RequireComponent(typeof(PenguinPlayerController))]
public class StickDebugGizmo : MonoBehaviour
{
    [Tooltip("棒の芯（Pivot→Tip）を線で描く")] public bool drawStickLine = true;
    [Tooltip("当たり判定の球を描く")] public bool drawHitSpheres = true;
    [Tooltip("実行中でなくても描く")] public bool drawInEditMode = true;

    void OnDrawGizmos()
    {
        if (!drawInEditMode && !Application.isPlaying) return;

        var ctrl = GetComponent<PenguinPlayerController>();
        if (ctrl == null || ctrl.stickTip == null) return;

        // SuikawariGame が未生成のエディタ編集中でも既定値で描けるようにする
        float melonR = 0.5f, penguinR = 0.55f;
        var game = FindObjectOfType<SuikawariGame>();
        if (game != null)
        {
            melonR = game.melonHitRadius;
            penguinR = game.penguinHitRadius;
        }

        Vector3 tip = ctrl.stickTip.position;

        if (drawStickLine && ctrl.stickPivot != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(ctrl.stickPivot.position, tip);
            Gizmos.DrawSphere(ctrl.stickPivot.position, 0.02f);   // 回転中心（手）
        }

        if (drawHitSpheres)
        {
            Gizmos.color = new Color(0.2f, 1f, 0.2f, 0.25f);      // スイカ判定
            Gizmos.DrawSphere(tip, melonR);
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.15f);      // ペンギン誤爆判定
            Gizmos.DrawWireSphere(tip, penguinR);
        }

        // 判定に使われる「スイカの中心」も描く（SuikawariGame と同じ +0.15m）
        if (game != null && game.melonWhole != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(game.melonWhole.transform.position + Vector3.up * 0.15f, 0.03f);
        }
    }
}
