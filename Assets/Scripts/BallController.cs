using UnityEngine;

public class BallController : MonoBehaviour
{
    private Rigidbody rb;

    [Header("停止の設定")]
    public float stopThreshold = 0.5f;  // 強制停止させる速度の閾値

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        // 落下中（垂直速度が一定以上）は停止判定を行わない
        if (Mathf.Abs(rb.linearVelocity.y) > 0.1f)
        {
            return;
        }

        float speed = rb.linearVelocity.magnitude;

        // 低速時に摩擦のような減速を加える
        if (speed < 2.0f && speed > 0)
        {
            rb.linearVelocity *= 0.95f;
            rb.angularVelocity *= 0.95f;
        }

        // 閾値以下になったら物理演算をスリープさせて完全に停止させる
        if (speed < stopThreshold && speed > 0.01f)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.Sleep();
        }
    }
}