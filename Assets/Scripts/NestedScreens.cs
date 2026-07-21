using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// ゲーム機の画面に「画面の中の画面の中…」を作る3D入れ子レンダラー。
///
/// 仕組み（絵コンテ④⑤の構造）:
///   RT_Game   = ゲームプレイ俯瞰カメラの映像（最深部）
///   RT_Nest1  = ゲーム機前カメラが「画面にRT_Gameを表示した状態」を撮った映像
///   RT_Nest2  = 同じく「画面にRT_Nest1を表示した状態」…
///   viewCamera(実際の表示) は最後のRTを画面に表示した状態のワールドを映す
///   → 画面の周囲には前ステージの風景（パラソル・砂浜）が実3Dで残る
///
/// カメラごとに画面マテリアルのテクスチャを描画直前に差し替えることで実現
/// （URPは RenderPipelineManager、Built-inは Camera.onPreRender の両対応）。
///
/// セットアップ:
///  - 空オブジェクトに付け、Inspectorで gameplayCamera / viewCamera /
///    consoleRenderer（ScreenMatを含むゲーム機のRenderer）を割り当てる
/// </summary>
public class NestedScreens : MonoBehaviour
{
    [Header("参照")]
    public Camera gameplayCamera;                 // フィールド俯瞰（プレイ映像）
    public Camera viewCamera;                     // プレイヤーが実際に見るカメラ
    public Renderer consoleRenderer;              // ゲーム機のRenderer
    public string screenMaterialName = "ScreenMat";

    [Header("設定")]
    public int rtWidth = 1280;
    public int rtHeight = 720;
    [Tooltip("画面の自発光強度")] public float screenBrightness = 1.15f;

    Material screenMat;
    RenderTexture rtGame;
    readonly List<Camera> nestCams = new List<Camera>();
    readonly List<RenderTexture> nestRTs = new List<RenderTexture>();
    readonly Dictionary<Camera, Texture> texForCam = new Dictionary<Camera, Texture>();

    void Awake()
    {
        // 統合メッシュの中から ScreenMat のインスタンスを探す
        foreach (var m in consoleRenderer.materials)
            if (m.name.Contains(screenMaterialName)) { screenMat = m; break; }
        if (screenMat == null)
        {
            Debug.LogWarning("ScreenMat が見つかりません。先頭マテリアルを使用します");
            screenMat = consoleRenderer.materials[0];
        }
        screenMat.EnableKeyword("_EMISSION");
        if (screenMat.HasProperty("_EmissionColor"))
            screenMat.SetColor("_EmissionColor", Color.white * screenBrightness);

        rtGame = new RenderTexture(rtWidth, rtHeight, 24) { name = "RT_Game", antiAliasing = 2 };
        gameplayCamera.targetTexture = rtGame;

        RenderPipelineManager.beginCameraRendering += OnBeginCamSRP;
        Camera.onPreRender += OnPreRenderBuiltin;

        SetStage(1);
    }

    void OnDestroy()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCamSRP;
        Camera.onPreRender -= OnPreRenderBuiltin;
        if (rtGame) rtGame.Release();
        foreach (var rt in nestRTs) if (rt) rt.Release();
    }

    void OnBeginCamSRP(ScriptableRenderContext ctx, Camera cam) => ApplyFor(cam);
    void OnPreRenderBuiltin(Camera cam) => ApplyFor(cam);

    void ApplyFor(Camera cam)
    {
        if (texForCam.TryGetValue(cam, out var tex)) SetScreenTexture(tex);
    }

    void SetScreenTexture(Texture t)
    {
        screenMat.mainTexture = t;
        if (screenMat.HasProperty("_BaseMap")) screenMat.SetTexture("_BaseMap", t);
        if (screenMat.HasProperty("_EmissionMap")) screenMat.SetTexture("_EmissionMap", t);
    }

    /// <summary>
    /// ステージに応じて入れ子カメラを組み直す。
    /// stage1: viewCameraは俯瞰位置でワールドを直接映す（画面はRT_Game表示）
    /// stage2: viewCameraがゲーム機前、画面 = RT_Game
    /// stage3: 中間カメラ1台 … stageN: 中間カメラN-2台
    /// </summary>
    public void SetStage(int stage)
    {
        foreach (var c in nestCams) if (c) Destroy(c.gameObject);
        nestCams.Clear();
        foreach (var rt in nestRTs) if (rt) rt.Release();
        nestRTs.Clear();
        texForCam.Clear();

        Texture current = rtGame;
        int extra = Mathf.Max(0, stage - 2);
        for (int k = 0; k < extra; k++)
        {
            var rt = new RenderTexture(rtWidth, rtHeight, 24) { name = $"RT_Nest{k}" };
            var go = new GameObject($"NestCam{k}");
            var cam = go.AddComponent<Camera>();
            cam.CopyFrom(viewCamera);
            cam.targetTexture = rt;
            cam.depth = viewCamera.depth - (extra - k);   // 深い階層から順に描画
            texForCam[cam] = current;                      // このカメラが写す瞬間の画面内容
            nestCams.Add(cam);
            nestRTs.Add(rt);
            current = rt;
        }
        texForCam[viewCamera] = current;      // 実表示カメラが写す画面内容
        texForCam[gameplayCamera] = rtGame;   // 俯瞰視点では画面に自映像(1F遅れ)が映る
    }

    /// <summary>中間カメラ群を「ゲーム機を眺める定位置」へ置く</summary>
    public void SetNestPose(Transform pose)
    {
        foreach (var c in nestCams)
            c.transform.SetPositionAndRotation(pose.position, pose.rotation);
    }
}
