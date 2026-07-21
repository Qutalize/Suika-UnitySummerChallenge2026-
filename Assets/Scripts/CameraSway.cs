using UnityEngine;

/// <summary>
/// 手持ちカメラ風の微妙な揺れ（ブリージング）。
/// ViewCamera に付けるだけでOK。SuikawariGame のカメラ移動コルーチンと
/// 共存できるよう「前フレームの揺れを引いて今フレームの揺れを足す」差分方式。
/// ゲーム機を覗き込むステージ2以降で特に3Dの実在感が出ます。
/// </summary>
public class CameraSway : MonoBehaviour
{
    [Tooltip("位置の揺れ幅(m)。0.005〜0.02程度")] public float positionAmount = 0.01f;
    [Tooltip("回転の揺れ幅(度)")] public float rotationAmount = 0.5f;
    [Tooltip("揺れの速さ")] public float speed = 0.35f;

    Vector3 lastPosOffset;
    Vector3 lastRotOffset;

    void LateUpdate()
    {
        float t = Time.time * speed;
        Vector3 posOffset = new Vector3(
            (Mathf.PerlinNoise(t, 0.0f) - 0.5f),
            (Mathf.PerlinNoise(0.0f, t) - 0.5f),
            (Mathf.PerlinNoise(t, t) - 0.5f)) * 2f * positionAmount;
        Vector3 rotOffset = new Vector3(
            (Mathf.PerlinNoise(t + 13.7f, 0.0f) - 0.5f),
            (Mathf.PerlinNoise(0.0f, t + 71.3f) - 0.5f),
            0f) * 2f * rotationAmount;

        transform.position += posOffset - lastPosOffset;
        transform.rotation *= Quaternion.Euler(rotOffset - lastRotOffset);

        lastPosOffset = posOffset;
        lastRotOffset = rotOffset;
    }
}
