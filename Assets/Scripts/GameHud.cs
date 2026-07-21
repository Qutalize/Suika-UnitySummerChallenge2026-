using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 画面遷移用UI（絵コンテ①⑧⑨対応）。
///  - タイトル画面: ゲームタイトル + [はじめる]
///  - クリア画面: CLEAR + [クレジット] [タイトルへ]
///  - プレイ中HUD: 左下のタイマーボックス / 中央メッセージ / 黒フェード
/// すべて実行時に自動生成。日本語表示にしたい場合は SuikawariGame の
/// Japanese Font に任意のフォントアセットを割り当てる。
/// </summary>
public class GameHud : MonoBehaviour
{
    Canvas canvas;
    Font font;
    Image fadeImage;
    Text messageText;
    RectTransform timerBox;
    Text timerText;
    GameObject titlePanel, clearPanel;
    GameObject endingsPage, howToPage, creditsPage;
    Text endingListText;
    float msgUntil;

    string gameTitle = "Suika";
    string creditsBody = "";

    public static GameHud Create(Font customFont, string title = "Suika", string credits = null)
    {
        var go = new GameObject("GameHud");
        var hud = go.AddComponent<GameHud>();
        hud.font = customFont != null
            ? customFont
            : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (!string.IsNullOrEmpty(title)) hud.gameTitle = title;
        if (!string.IsNullOrEmpty(credits)) hud.creditsBody = credits;
        hud.Build();
        return hud;
    }

    void Build()
    {
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        gameObject.AddComponent<GraphicRaycaster>();

        if (FindObjectOfType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        // ---- タイマーボックス（左下）----
        timerBox = NewRect("TimerBox", Vector2.zero, Vector2.zero);
        timerBox.pivot = Vector2.zero;
        timerBox.anchoredPosition = new Vector2(24, 24);
        timerBox.sizeDelta = new Vector2(360, 64);
        timerBox.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.72f);
        timerText = NewText(timerBox, "", 34, TextAnchor.MiddleCenter);
        Stretch(timerText.rectTransform);
        timerBox.gameObject.SetActive(false);

        // ---- 中央メッセージ ----
        messageText = NewText((RectTransform)canvas.transform, "", 100, TextAnchor.MiddleCenter);
        Stretch(messageText.rectTransform);

        // ---- タイトルパネル（①）----
        titlePanel = BuildTitlePanel();
        // ---- クリアパネル（⑧）----
        clearPanel = BuildClearPanel();

        // ---- タイトルのボタンから開くサブページ ----
        // titlePanel より後に作ることで、開いたときタイトルの上に重なる
        endingsPage = BuildEndingsPage();
        howToPage   = BuildHowToPage();
        creditsPage = BuildCreditsPage();

        // ---- フェード（最前面）----
        var fr = NewRect("Fade", Vector2.zero, Vector2.one);
        fadeImage = fr.gameObject.AddComponent<Image>();
        fadeImage.color = new Color(0, 0, 0, 0);
        fadeImage.raycastTarget = false;
    }

    GameObject BuildTitlePanel()
    {
        var p = NewRect("TitlePanel", Vector2.zero, Vector2.one).gameObject;
        BuildStripedTitle((RectTransform)p.transform, gameTitle, 190,
                          new Vector2(0, 0.5f), new Vector2(1, 0.95f));

        // ボタンは浜辺やペンギンを隠さないよう小ぶりにして画面下へ寄せる。
        // START だけ少し大きくして主役だと分かるようにし、
        // サブページ入口は控えめな小ボタンで下端に一列に並べる。
        // （エンディング一覧は常駐させず [ENDINGS] を押したときだけ開く）
        var startSize = new Vector2(230, 66);
        var subSize   = new Vector2(190, 48);
        MakeButton(p.transform, "START", new Vector2(0.5f, 0.20f), null, startSize, 34);
        MakeButton(p.transform, "HOW TO PLAY", new Vector2(0.34f, 0.075f), null, subSize, 24);
        MakeButton(p.transform, "ENDINGS",     new Vector2(0.5f,  0.075f), null, subSize, 24);
        MakeButton(p.transform, "CREDITS",     new Vector2(0.66f, 0.075f), null, subSize, 24);

        p.SetActive(false);
        return p;
    }

    /// <summary>
    /// スイカ柄のタイトル文字。緑・黒・赤の縦縞を、文字の形に切り抜いて作る。
    ///
    /// Unity の Text は1つの文字列に複数の色を塗れないため、次の手順で作る:
    ///   1) 同じ文字を Mask にする（showMaskGraphic = false で文字自体は描かない）
    ///   2) その子として縦縞の Image を並べる
    ///   → 縞が文字の形だけ残り、スイカの皮のような見た目になる
    ///
    /// 空を背景にすると緑が沈むので、奥から順に
    /// 「影文字 → 白フチ文字 → 縞文字」と3枚重ねて輪郭を立てる。
    /// </summary>
    void BuildStripedTitle(RectTransform parent, string label, int size,
                           Vector2 anchorMin, Vector2 anchorMax)
    {
        // --- 影（背景から浮かせるための下敷き）---
        // 白フチより外側に出るよう、フチの太さより大きくずらす
        var shadow = NewText(parent, label, size, TextAnchor.MiddleCenter);
        var sr = shadow.rectTransform;
        sr.anchorMin = anchorMin; sr.anchorMax = anchorMax;
        sr.offsetMin = new Vector2(11f, -11f); sr.offsetMax = new Vector2(11f, -11f);
        shadow.color = new Color(0f, 0f, 0f, 0.5f);
        var so = shadow.GetComponent<Outline>(); if (so) Destroy(so);

        // --- 白フチ ---
        // Outline は文字メッシュを4方向へずらして複製するので、
        // 「白い文字」に「白いアウトライン」を掛けると一回り太った白文字になる。
        // その上に同じ位置・同じ大きさの縞文字を重ねると、
        // はみ出した白だけが縁取りとして残る。
        var rim = NewText(parent, label, size, TextAnchor.MiddleCenter);
        var rr = rim.rectTransform;
        rr.anchorMin = anchorMin; rr.anchorMax = anchorMax;
        rr.offsetMin = Vector2.zero; rr.offsetMax = Vector2.zero;
        rim.color = Color.white;
        var ro = rim.GetComponent<Outline>();
        ro.effectColor = Color.white;
        ro.effectDistance = new Vector2(6f, 6f);   // フチの太さ

        // --- 縞を切り抜くための文字（これ自体は描画されない）---
        var maskText = NewText(parent, label, size, TextAnchor.MiddleCenter);
        var mr = maskText.rectTransform;
        mr.anchorMin = anchorMin; mr.anchorMax = anchorMax;
        mr.offsetMin = Vector2.zero; mr.offsetMax = Vector2.zero;
        var mo = maskText.GetComponent<Outline>(); if (mo) Destroy(mo);
        var mask = maskText.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        // --- 縦縞（文字の形に切り抜かれて見える）---
        Color[] palette =
        {
            new Color(0.07f, 0.08f, 0.09f),   // 黒（縞）
            new Color(0.15f, 0.55f, 0.22f),   // 緑（皮）
            new Color(0.86f, 0.20f, 0.26f),   // 赤（果肉）
        };
        const int stripes = 50;               // 縞の細かさ。増やすほど細くなる
        for (int i = 0; i < stripes; i++)
        {
            var s = NewRect("Stripe" + i,
                            new Vector2(i / (float)stripes, 0f),
                            new Vector2((i + 1) / (float)stripes, 1f),
                            mr);
            s.gameObject.AddComponent<Image>().color = palette[i % palette.Length];
        }
    }

    /// <summary>
    /// タイトルから開くサブページの共通の骨組み。
    /// 全画面の暗幕 +（見出し・本文を載せた）カード + [BACK] を作る。
    ///
    /// 砂浜や空の上に白抜き文字を直接重ねると、背景と色が混ざって読めなくなる。
    /// そのため本文は必ず「ほぼ不透明な明るいカード」の上に置き、
    /// 縁取りを外した濃い文字色で表示する（ボタンと同じ配色）。
    ///
    /// 本文の Text は out で返すので、エンディング一覧のように
    /// 開くたびに中身が変わるページは後から書き換えられる。
    /// </summary>
    GameObject BuildPage(string name, string heading, string body, out Text bodyText)
    {
        var p = NewRect(name, Vector2.zero, Vector2.one).gameObject;
        // 背景の浜辺を沈める暗幕。カードの明るさを引き立てる役目もある
        p.AddComponent<Image>().color = new Color(0.02f, 0.05f, 0.10f, 0.82f);

        // --- 文字を載せるカード ---
        var card = NewRect("Card", new Vector2(0.13f, 0.17f), new Vector2(0.87f, 0.93f), p.transform);
        card.gameObject.AddComponent<Image>().color = new Color(0.98f, 0.97f, 0.93f, 0.97f);

        var h = NewText(card, heading, 72, TextAnchor.MiddleCenter);
        var hr = h.rectTransform;
        hr.anchorMin = new Vector2(0.04f, 0.84f); hr.anchorMax = new Vector2(0.96f, 0.99f);
        hr.offsetMin = Vector2.zero; hr.offsetMax = Vector2.zero;
        DarkText(h, new Color(0.10f, 0.12f, 0.16f));

        // --- スクロールする本文 ---
        // 文字を小さくして詰め込むと読めなくなるため、サイズは固定にして
        // カードからあふれた分はスクロールで読ませる。
        // Viewport からはみ出した部分は RectMask2D で隠れる。
        var viewport = NewRect("Viewport", new Vector2(0.06f, 0.04f), new Vector2(0.94f, 0.82f), card);
        viewport.gameObject.AddComponent<RectMask2D>();

        bodyText = NewText(viewport, body, 30, TextAnchor.UpperCenter);
        var br = bodyText.rectTransform;
        // 上端に貼り付けて下へ伸びる形にする。これがスクロールの中身になる
        br.anchorMin = new Vector2(0f, 1f);
        br.anchorMax = new Vector2(1f, 1f);
        br.pivot     = new Vector2(0.5f, 1f);
        br.offsetMin = Vector2.zero; br.offsetMax = Vector2.zero;
        bodyText.lineSpacing = 1.25f;
        bodyText.verticalOverflow = VerticalWrapMode.Overflow;
        DarkText(bodyText, new Color(0.18f, 0.19f, 0.22f));

        // 行数に応じて本文の高さが決まる＝スクロールできる長さが決まる
        var fitter = bodyText.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = br;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 35f;

        // --- スクロールバー（続きがあることを示す。収まりきる場合は自動で隠れる）---
        var barRect = NewRect("Scrollbar", new Vector2(0.955f, 0.04f), new Vector2(0.978f, 0.82f), card);
        barRect.gameObject.AddComponent<Image>().color = new Color(0.80f, 0.78f, 0.72f, 0.55f);
        var bar = barRect.gameObject.AddComponent<Scrollbar>();
        bar.direction = Scrollbar.Direction.BottomToTop;

        var handle = NewRect("Handle", Vector2.zero, Vector2.one, barRect);
        var handleImg = handle.gameObject.AddComponent<Image>();
        handleImg.color = new Color(0.35f, 0.38f, 0.42f, 0.9f);
        bar.targetGraphic = handleImg;
        bar.handleRect = handle;

        scroll.verticalScrollbar = bar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

        MakeButton(p.transform, "BACK", new Vector2(0.5f, 0.085f), null);

        p.SetActive(false);
        return p;
    }

    /// <summary>明るいカードの上に置く文字。白抜き＋縁取りをやめ、濃い色にする</summary>
    void DarkText(Text t, Color c)
    {
        t.color = c;
        var o = t.GetComponent<Outline>();
        if (o) Destroy(o);
    }

    GameObject BuildEndingsPage()
    {
        var p = BuildPage("EndingsPage", "ENDINGS", "", out endingListText);
        endingListText.alignment = TextAnchor.UpperLeft;

        // 集めた記録を消すボタン。破壊的な操作なので赤みを付けて区別する
        MakeButton(p.transform, "RESET", new Vector2(0.79f, 0.085f), null);
        var img = p.transform.Find("Btn_RESET").GetComponent<Image>();
        img.color = new Color(0.96f, 0.80f, 0.78f, 0.95f);
        return p;
    }

    /// <summary>
    /// 実績リセットボタンの割り当て。1回目の押下では確認に切り替わり、
    /// もう一度押して初めて消える。集めた記録が誤操作で消えないようにするため。
    /// ページを開き直すと確認状態は解除される。
    /// </summary>
    void BindResetButton()
    {
        var btnTf = endingsPage.transform.Find("Btn_RESET");
        var label = btnTf.GetComponentInChildren<Text>();
        var btn = btnTf.GetComponent<Button>();
        bool armed = false;
        label.text = "RESET";

        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() =>
        {
            if (!armed)
            {
                armed = true;
                label.text = "SURE?";
                return;
            }
            EndingRegistry.ResetAll();
            armed = false;
            label.text = "RESET";
            RefreshEndingList();
        });
    }

    GameObject BuildHowToPage()
    {
        Text unused;
        return BuildPage("HowToPage", "HOW TO PLAY",
            "W / S ： 前へ進む・後ろへ下がる\n" +
            "A / D ： その場で左右に向きを変える\n" +
            "SPACE ： 棒を振り下ろす\n" +
            "\n" +
            "目隠しのペンギンを動かして、スイカを割ろう。\n" +
            "ステージが進むほど、操作が遅れて伝わるようになる。\n" +
            "\n" +
            "時間切れ・仲間を叩く・海に入る と失敗。\n" +
            "失敗の仕方によってエンディングが変わる。",
            out unused);
    }

    GameObject BuildCreditsPage()
    {
        // 由来や参照URLを書き足して長くなっても、BuildPage が
        // スクロールできる本文を作るので、ここでの調整は不要
        Text unused;
        return BuildPage("CreditsPage", "CREDITS", creditsBody, out unused);
    }

    /// <summary>
    /// 達成済みのエンディングを一覧表示する。未達成は「???」で伏せて、
    /// 全部集めたくなるようにする。ENDINGSページを開くたびに更新される。
    /// </summary>
    void RefreshEndingList()
    {
        if (endingListText == null) return;

        var sb = new System.Text.StringBuilder();
        sb.Append($"COLLECTED   {EndingRegistry.UnlockedCount} / {EndingRegistry.TotalCount}\n");
        foreach (var e in EndingRegistry.All)
        {
            // 達成済みのものだけ、どうやって辿り着いたかを添える
            sb.Append(EndingRegistry.IsUnlocked(e)
                ? $"\n★ {EndingRegistry.Title(e)}\n     {EndingRegistry.Description(e)}\n"
                : "\n☆ ???\n\n");
        }
        endingListText.text = sb.ToString();
    }

    GameObject BuildClearPanel()
    {
        var p = NewRect("ClearPanel", Vector2.zero, Vector2.one).gameObject;
        // 全ステージ制覇後にしか出ない画面なので、ここが最終的なクリア表示になる
        var t = NewText((RectTransform)p.transform, "ALL CLEAR", 140, TextAnchor.MiddleCenter);
        var tr = t.rectTransform;
        tr.anchorMin = new Vector2(0, 0.55f); tr.anchorMax = new Vector2(1, 0.95f);
        tr.offsetMin = Vector2.zero; tr.offsetMax = Vector2.zero;

        // クレジットはタイトルと同じページを使い回す（ShowClearMenu で開く）
        MakeButton(p.transform, "CREDITS", new Vector2(0.38f, 0.25f), null);
        MakeButton(p.transform, "TITLE", new Vector2(0.62f, 0.25f), null);

        p.SetActive(false);
        return p;
    }

    // ---------------- 公開API ----------------

    public void ShowTitle(Action onStart)
    {
        HideMenus();
        titlePanel.SetActive(true);
        BindButton(titlePanel, "START", onStart);
        BindButton(titlePanel, "HOW TO PLAY", () => OpenPage(howToPage));
        // 開くたびに作り直す。直前のプレイで解放したエンドがすぐ反映される
        BindButton(titlePanel, "ENDINGS", () =>
        {
            RefreshEndingList();
            OpenPage(endingsPage);
            BindResetButton();   // 確認状態を毎回リセットしてから開く
        });
        BindButton(titlePanel, "CREDITS", () => OpenPage(creditsPage));
    }

    public void ShowClearMenu(Action onTitle)
    {
        HideMenus();
        clearPanel.SetActive(true);
        BindButton(clearPanel, "CREDITS", () => OpenPage(creditsPage));
        BindButton(clearPanel, "TITLE", onTitle);
    }

    /// <summary>サブページを開く。[BACK] を押すと閉じて呼び出し元の画面に戻る</summary>
    void OpenPage(GameObject page)
    {
        page.SetActive(true);
        BindButton(page, "BACK", () => page.SetActive(false));

        // 開き直したときは先頭から読めるよう、スクロール位置を上端へ戻す。
        // ForceUpdateCanvases を挟むのは、レイアウトが確定する前に
        // 位置を指定しても反映されないため。
        var scroll = page.GetComponentInChildren<ScrollRect>(true);
        if (scroll != null)
        {
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = 1f;
        }
    }

    public void HideMenus()
    {
        titlePanel.SetActive(false);
        clearPanel.SetActive(false);
        endingsPage.SetActive(false);
        howToPage.SetActive(false);
        creditsPage.SetActive(false);
    }

    public void SetTimerVisible(bool v) => timerBox.gameObject.SetActive(v);

    public void SetTimer(float timeLeft, int stage)
    {
        timerText.text = $"STAGE {stage}   TIME {Mathf.Max(0, timeLeft):0.0}";
        timerText.color = timeLeft < 3f ? new Color(1f, 0.35f, 0.35f) : Color.white;
    }

    public void ShowMessage(string msg, float duration)
    {
        messageText.text = msg;
        msgUntil = Time.time + duration;
    }

    /// <summary>黒フェード。to=1で暗転、0で明転</summary>
    public System.Collections.IEnumerator Fade(float to, float dur)
    {
        float from = fadeImage.color.a, t = 0;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.01f, dur);
            fadeImage.color = new Color(0, 0, 0, Mathf.Lerp(from, to, t));
            yield return null;
        }
        fadeImage.color = new Color(0, 0, 0, to);
    }

    void Update()
    {
        if (messageText.text.Length > 0 && Time.time > msgUntil) messageText.text = "";
    }

    // ---------------- helpers ----------------

    void MakeButton(Transform parent, string label, Vector2 anchor, Action onClick,
                    Vector2? size = null, int fontSize = 44)
    {
        var r = NewRect("Btn_" + label, anchor, anchor, parent);
        r.sizeDelta = size ?? new Vector2(360, 96);
        var img = r.gameObject.AddComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.92f);
        var btn = r.gameObject.AddComponent<Button>();
        if (onClick != null) btn.onClick.AddListener(() => onClick());
        var t = NewText(r, label, fontSize, TextAnchor.MiddleCenter);
        t.color = new Color(0.1f, 0.1f, 0.12f);
        var o = t.GetComponent<Outline>(); if (o) Destroy(o);
        Stretch(t.rectTransform);
    }

    void BindButton(GameObject panel, string label, Action onClick)
    {
        var btn = panel.transform.Find("Btn_" + label).GetComponent<Button>();
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => onClick());
    }

    Text NewText(RectTransform parent, string txt, int size, TextAnchor anchor)
    {
        var go = new GameObject("Text");
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.font = font;
        t.text = txt;
        t.fontSize = size;
        t.fontStyle = FontStyle.Bold;
        t.alignment = anchor;
        t.color = Color.white;
        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0, 0, 0, 0.9f);
        outline.effectDistance = new Vector2(3, -3);
        return t;
    }

    RectTransform NewRect(string name, Vector2 aMin, Vector2 aMax, Transform parent = null)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var r = (RectTransform)go.transform;
        r.SetParent(parent != null ? parent : canvas.transform, false);
        r.anchorMin = aMin; r.anchorMax = aMax;
        r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        return r;
    }

    void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
    }
}
