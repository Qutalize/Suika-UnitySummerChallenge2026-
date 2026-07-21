using UnityEngine;

/// <summary>
/// このゲームのエンディング（結末）の種類。
/// 追加したい場合はここに1つ足し、EndingRegistry の Title / Hint にも対応を書く。
/// </summary>
public enum GameEnding
{
    /// <summary>全ステージクリア → 仲間とスイカを食べる（トゥルーエンド）</summary>
    AllClear,
    /// <summary>仲間のペンギンを叩いてしまった</summary>
    PenguinDown,
    /// <summary>制限時間切れ</summary>
    TimeUp,
    /// <summary>海に近づいて泳いで行ってしまった</summary>
    SwimAway,
    /// <summary>スイカと間違えてビーチボールを叩いてしまった</summary>
    BallHit,
}

/// <summary>
/// エンディングの達成状況を記録・参照する。
/// PlayerPrefs に保存するので、ゲームを再起動しても達成状況が残る。
/// タイトル画面（GameHud）が、ここを見て「どのエンドを達成したか」を表示する。
///
/// 達成状況を消したいときは、GameRoot の SuikawariGame を右クリック →
/// 「エンディング達成状況をリセット」を実行する。
/// </summary>
public static class EndingRegistry
{
    const string KeyPrefix = "suikawari.ending.";

    /// <summary>表示順。タイトル画面はこの順に並べる</summary>
    public static readonly GameEnding[] All =
    {
        GameEnding.AllClear,
        GameEnding.PenguinDown,
        GameEnding.TimeUp,
        GameEnding.SwimAway,
        GameEnding.BallHit,
    };

    public static int TotalCount { get { return All.Length; } }

    public static int UnlockedCount
    {
        get
        {
            int n = 0;
            foreach (var e in All) if (IsUnlocked(e)) n++;
            return n;
        }
    }

    /// <summary>達成として記録する（すでに達成済みなら何もしない）</summary>
    public static void Unlock(GameEnding e)
    {
        if (IsUnlocked(e)) return;
        PlayerPrefs.SetInt(KeyPrefix + e, 1);
        PlayerPrefs.Save();
    }

    public static bool IsUnlocked(GameEnding e)
    {
        return PlayerPrefs.GetInt(KeyPrefix + e, 0) == 1;
    }

    /// <summary>すべての達成状況を消す（テスト用）</summary>
    public static void ResetAll()
    {
        foreach (var e in All) PlayerPrefs.DeleteKey(KeyPrefix + e);
        PlayerPrefs.Save();
    }

    /// <summary>エンディング名（達成後にタイトル画面へ表示される）</summary>
    public static string Title(GameEnding e)
    {
        switch (e)
        {
            case GameEnding.AllClear:    return "WATERMELON PARTY";
            case GameEnding.PenguinDown: return "PENGUIN DOWN";
            case GameEnding.TimeUp:      return "TIME UP";
            case GameEnding.SwimAway:    return "GONE SWIMMING";
            case GameEnding.BallHit:     return "WRONG BALL";
            default:                     return e.ToString();
        }
    }

    /// <summary>ゲームオーバー時に画面へ出す短い説明</summary>
    public static string Description(GameEnding e)
    {
        switch (e)
        {
            case GameEnding.AllClear:    return "スイカを食べた";
            case GameEnding.PenguinDown: return "仲間を叩いてしまった";
            case GameEnding.TimeUp:      return "時間切れ";
            case GameEnding.SwimAway:    return "海へ泳いで行ってしまった";
            case GameEnding.BallHit:     return "ビーチボールを叩いてしまった";
            default:                     return "";
        }
    }
}
