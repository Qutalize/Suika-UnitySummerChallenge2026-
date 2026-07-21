using UnityEngine;

/// <summary>
/// 波の音に、たまにカモメの鳴き声を混ぜる環境音スクリプト。
///
/// 一定間隔で鳴らすと「タイマーで鳴っている」と分かってしまうため、
///  ・鳴る間隔をランダム化
///  ・ピッチを毎回わずかに変える（別の個体に聞こえる）
///  ・鳴る位置をリスナーの周囲にランダム配置（方向感が出る）
///  ・たまに2〜3回続けて鳴かせる（実際のカモメは連続して鳴く）
/// という4点で「自然にたまに鳴る」状態を作ります。
///
/// セットアップ:
///  1. 空のGameObject「Seagulls」を作り、このスクリプトを付ける
///  2. Clips に カモメの鳴き声を1〜3種類割り当てる
///  3. Listener に GameplayCamera（Audio Listener が付いたカメラ）を割り当てる
///     ※未設定なら Camera.main を自動で探します
/// </summary>
public class AmbientSeagulls : MonoBehaviour
{
    [Header("音源")]
    [Tooltip("カモメの鳴き声。複数入れるとランダムに選ばれます")]
    public AudioClip[] clips;

    [Header("鳴る間隔（秒）")]
    [Tooltip("次に鳴くまでの最短時間")] public float intervalMin = 6f;
    [Tooltip("次に鳴くまでの最長時間")] public float intervalMax = 18f;

    [Header("鳴き方")]
    [Tooltip("1回の鳴き声の音量")] [Range(0f, 1f)] public float volume = 0.35f;
    [Tooltip("ピッチのばらつき（±この値）。個体差に聞こえます")]
    [Range(0f, 0.3f)] public float pitchVariation = 0.12f;
    [Tooltip("続けて鳴く最大回数。1なら常に単発")] [Range(1, 4)] public int maxBurst = 3;
    [Tooltip("続けて鳴くときの間隔")] public float burstGap = 0.45f;

    [Header("鳴る位置")]
    [Tooltip("リスナーからの距離。遠いほど小さく遠くから聞こえます")]
    public float distance = 14f;
    [Tooltip("リスナーより何m上空で鳴かせるか")] public float height = 7f;
    [Tooltip("音を聞く基準。未設定なら Camera.main を使います")]
    public Transform listener;

    float nextTime;

    void Start()
    {
        if (listener == null && Camera.main != null) listener = Camera.main.transform;
        ScheduleNext();
    }

    void Update()
    {
        if (clips == null || clips.Length == 0) return;
        if (Time.time < nextTime) return;

        StartCoroutine(Caw());
        ScheduleNext();
    }

    void ScheduleNext() =>
        nextTime = Time.time + Random.Range(intervalMin, Mathf.Max(intervalMin, intervalMax));

    System.Collections.IEnumerator Caw()
    {
        // 1回の「鳴き」で1〜maxBurst回続ける。同じ個体が鳴くので位置は固定
        Vector3 pos = RandomSkyPosition();
        int count = Random.Range(1, maxBurst + 1);
        float pitch = 1f + Random.Range(-pitchVariation, pitchVariation);

        for (int i = 0; i < count; i++)
        {
            PlayOneShot3D(clips[Random.Range(0, clips.Length)], pos, pitch);
            if (i < count - 1)
                yield return new WaitForSeconds(burstGap * Random.Range(0.8f, 1.3f));
        }
    }

    Vector3 RandomSkyPosition()
    {
        Vector3 origin = listener != null ? listener.position : transform.position;
        float angle = Random.Range(0f, Mathf.PI * 2f);
        return origin
             + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance
             + Vector3.up * height;
    }

    /// <summary>
    /// AudioSource.PlayClipAtPoint と違い、ピッチと減衰を指定できる版。
    /// 一時的なGameObjectを作り、再生が終わったら自動で破棄します。
    /// </summary>
    void PlayOneShot3D(AudioClip clip, Vector3 pos, float pitch)
    {
        if (clip == null) return;

        var go = new GameObject("SeagullCry");
        go.transform.position = pos;
        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.volume = volume;
        src.pitch = pitch;
        src.spatialBlend = 1f;                 // 完全に3D（方向が分かる）
        src.rolloffMode = AudioRolloffMode.Linear;
        src.minDistance = 5f;
        src.maxDistance = distance * 3f;
        src.Play();

        // ピッチを変えると再生時間も変わるので、その分を見込んで破棄
        Destroy(go, clip.length / Mathf.Max(0.01f, pitch) + 0.2f);
    }
}
