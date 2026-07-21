using UnityEngine;

/// <summary>
/// 観客ペンギン・ゲーマーペンギン用の待機モーション。
/// ゆっくり体を揺らし、たまに小さく跳ねます。各ペンギンに付けるだけでOK。
/// </summary>
public class PenguinIdleBob : MonoBehaviour
{
    public float swayAngle = 4f;      // 左右の揺れ角度
    public float swaySpeed = 1.2f;
    public float hopChance = 0.15f;   // 毎秒の跳ねる確率
    public float hopHeight = 0.12f;

    float phase;
    float hopT = 1f;
    Vector3 basePos;
    float baseYaw;

    void Start()
    {
        phase = Random.value * 10f;
        ResetBase();
    }

    /// <summary>
    /// 揺れの基準位置を現在地に取り直す。
    /// 演出でペンギンを動かしたあと待機モーションを再開するときに呼ぶ。
    /// これを呼ばずに再開すると、古い基準位置へ引き戻されてしまう。
    /// </summary>
    public void ResetBase()
    {
        basePos = transform.position;
        baseYaw = transform.eulerAngles.y;
        hopT = 1f;
    }

    void Update()
    {
        phase += Time.deltaTime * swaySpeed;
        float roll = Mathf.Sin(phase) * swayAngle;

        if (hopT >= 1f && Random.value < hopChance * Time.deltaTime) hopT = 0f;
        float y = 0f;
        if (hopT < 1f)
        {
            hopT += Time.deltaTime / 0.35f;
            y = Mathf.Sin(Mathf.Clamp01(hopT) * Mathf.PI) * hopHeight;
        }

        transform.position = basePos + Vector3.up * y;
        transform.rotation = Quaternion.Euler(0, baseYaw, roll);
    }
}
