using UnityEngine;

public class GoalArea : MonoBehaviour
{
    [Header("設定")]
    public string targetTag = "Ball";
    public float goalRadius = 2.0f;       // ゴール判定の半径
    public float attractionRadius = 8.0f; // 吸い込み有効範囲
    public float attractionForce = 25f;  // 引き寄せる力の強度
    public float damping = 0.95f;        // 進入時の減速係数

    [Header("ゴール判定追加")]
    public float timeToClear = 2.0f;    // 静止判定の必要時間
    private float stayTimer = 0.0f;

    private bool isCleared = false;
    private GameObject ball;

    void Start()
    {
        ball = GameObject.FindWithTag(targetTag);
    }

    void Update()
    {
        stayTimer += Time.deltaTime;

        if (isCleared || ball == null) return;

        Rigidbody rb = ball.GetComponent<Rigidbody>();
        if (rb == null) return;

        // ボールとの距離を計算（3次元的な直線距離）
        float distance = Vector3.Distance(transform.position, ball.transform.position);

        // 範囲内にボールがあれば吸い込み処理を実行
        if (distance <= attractionRadius)
        {
            // 中心へ向かうベクトル
            Vector3 direction = (transform.position - ball.transform.position).normalized;

            // 中心で静止しやすくするために速度を減衰させる
            rb.linearVelocity *= damping;
            rb.angularVelocity *= damping;

            // 力を加える（3次元的に中心へ引き寄せる）
            rb.AddForce(direction * attractionForce, ForceMode.Acceleration);

            // ゴールエリア内での滞在時間を計測
            if (distance <= goalRadius)
            {
                stayTimer += Time.deltaTime;

                isCleared = true;
                OnGoal();
            }
            else
            {
                // 判定エリアから出たらタイマーをリセット
                stayTimer = 0.0f;
            }
        }
    }

    void OnGoal()
    {
        Rigidbody rb = ball.GetComponent<Rigidbody>();
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // 最終的な位置を中心に固定
        ball.transform.position = transform.position;

        // リザルト処理の呼び出し
        Object.FindFirstObjectByType<GameManager>().OnGoal();
    }

    // エディタ上にギズモで範囲を可視化（緑が吸い込み範囲、赤がゴール判定）
    void OnDrawGizmos()
    {
        // デバッグ用に判定範囲をギズモで描画
        // 吸い込み範囲
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, attractionRadius);
        // ゴール判定
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, goalRadius);
    }
}