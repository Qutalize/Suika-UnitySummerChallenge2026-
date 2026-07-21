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