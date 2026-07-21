using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ペンギンの頭上に出る吹き出し。エンディングで「おーい！」と呼ぶ演出に使う。
///
/// 3Dワールド上に World Space Canvas を作り、常にカメラの方を向かせる（ビルボード）。
/// 見た目はすべて実行時に生成するので、プレハブの用意は不要。
///
/// 使い方:
///   SpeechBubble.Show(npcTransform, "おーい！", 1.1f, font, 2.0f);
/// </summary>
public class SpeechBubble : MonoBehaviour
{
    Camera targetCam;
    CanvasGroup group;

    /// <summary>
    /// 吹き出しを出す。duration 秒後に自動で消える。
    /// </summary>
    /// <param name="target">誰の頭上に出すか</param>
    /// <param name="message">表示する文字</param>
    /// <param name="height">対象の原点から何m上に出すか（ペンギンの身長に合わせる）</param>
    /// <param name="font">日本語を出す場合は日本語フォントを渡す</param>
    /// <param name="duration">表示時間（秒）</param>
    public static SpeechBubble Show(Transform target, string message, float height,
                                    Font font, float duration = 2f)
    {
        var go = new GameObject("SpeechBubble");
        go.transform.SetParent(target, false);
        go.transform.localPosition = Vector3.up * height;

        var bubble = go.AddComponent<SpeechBubble>();
        bubble.Build(message, font);
        bubble.StartCoroutine(bubble.Life(duration));
        return bubble;
    }

    // --- 見た目の調整値 ---
    const int   FontSize   = 54;
    const float PadX       = 70f;   // 文字の左右の余白（px）
    const float PadY       = 52f;   // 文字の上下の余白（px）
    const float MinWidth   = 300f;  // これより小さくはならない
    const float MinHeight  = 160f;
    const float TailSize   = 44f;   // しっぽの大きさ
    const float WorldScale = 0.0016f;

    void Build(string message, Font font)
    {
        // --- World Space Canvas ---
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        var rt = (RectTransform)transform;
        // ワールド空間なので、1ユニット=1mに合わせて小さく縮める
        rt.localScale = Vector3.one * WorldScale;

        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;

        // --- 吹き出しの本体 ---
        var bg = NewChild("BG", rt);
        var bgImg = bg.gameObject.AddComponent<Image>();
        bgImg.color = new Color(1f, 1f, 1f, 0.96f);

        // --- 文字 ---
        var txt = NewChild("Text", bg);
        var t = txt.gameObject.AddComponent<Text>();
        t.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.text = message;
        t.fontSize = FontSize;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.color = new Color(0.12f, 0.12f, 0.14f);

        // --- 文字の実寸を測って吹き出しを広げる ---
        // 固定サイズだと、長い台詞や日本語フォントで文字が枠からはみ出したり
        // 余白が詰まって読みにくくなるため、毎回テキストに合わせて作る。
        float w = Mathf.Max(MinWidth,  t.preferredWidth  + PadX);
        float h = Mathf.Max(MinHeight, t.preferredHeight + PadY);

        bg.sizeDelta = new Vector2(w, h);
        bg.anchoredPosition = new Vector2(0f, TailSize * 0.35f);

        // 文字は吹き出しいっぱいに広げて中央寄せ（余白は上で確保済み）
        txt.anchorMin = Vector2.zero; txt.anchorMax = Vector2.one;
        txt.offsetMin = Vector2.zero; txt.offsetMax = Vector2.zero;

        // --- しっぽ（正方形を45度回して三角のように見せる）---
        var tail = NewChild("Tail", rt);
        tail.sizeDelta = new Vector2(TailSize, TailSize);
        tail.anchoredPosition = new Vector2(-w * 0.18f, -h * 0.5f + TailSize * 0.5f);
        tail.localRotation = Quaternion.Euler(0, 0, 45);
        var tailImg = tail.gameObject.AddComponent<Image>();
        tailImg.color = new Color(1f, 1f, 1f, 0.96f);
        tail.SetAsFirstSibling();   // 本体の後ろに回して継ぎ目を隠す

        rt.sizeDelta = new Vector2(w, h + TailSize);

        targetCam = Camera.main;
    }

    RectTransform NewChild(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var r = (RectTransform)go.transform;
        r.SetParent(parent, false);
        return r;
    }

    void LateUpdate()
    {
        // カメラが切り替わる場合に備えて毎フレーム取り直す
        if (targetCam == null) targetCam = Camera.main;
        if (targetCam == null) return;

        // ビルボード：常にカメラの方を向く（文字が裏返らないよう forward を合わせる）
        transform.rotation = Quaternion.LookRotation(
            transform.position - targetCam.transform.position, Vector3.up);
    }

    IEnumerator Life(float duration)
    {
        // ぽんっと出る（少し大きくなってから戻る）
        Vector3 baseScale = transform.localScale;
        yield return Scale(baseScale * 0.4f, baseScale * 1.12f, 0.14f, 0f, 1f);
        yield return Scale(baseScale * 1.12f, baseScale, 0.08f, 1f, 1f);

        yield return new WaitForSeconds(Mathf.Max(0f, duration - 0.42f));

        // すっと消える
        yield return Scale(baseScale, baseScale * 0.7f, 0.2f, 1f, 0f);
        Destroy(gameObject);
    }

    IEnumerator Scale(Vector3 from, Vector3 to, float dur, float aFrom, float aTo)
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / dur;
            float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            transform.localScale = Vector3.Lerp(from, to, e);
            group.alpha = Mathf.Lerp(aFrom, aTo, e);
            yield return null;
        }
    }
}
