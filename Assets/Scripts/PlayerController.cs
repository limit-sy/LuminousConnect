using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System.Collections;
using TMPro;
using UnityEngine.SceneManagement;

/// <summary>
/// プレイヤーの移動、ジャンプ、エイム、ショット全般を制御するクラス
/// Input System (Send Messages) を採用
/// </summary>

public class PlayerController : MonoBehaviour
{
    [Header("基本コンポーネント")]
    public GameObject ball;
    public Transform stickPivot;
    public Transform shootPoint;
    public Slider powerSlider;

    [Header("移動設定")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 10f;
    public float stickRotationSpeed = 80f;
    public float jumpHeight = 2.0f;    // ジャンプする高さ
    private float verticalVelocity = 0f; // 現在の上下方向の速度
    private float gravity = -9.81f;    // 重力の強さ

    [Header("ショット設定")]
    public float maxShotPower = 40f;
    public float powerFillSpeed = 1.2f;
    public float shotEnableDistance = 2.0f; // ボールを打てる距離の閾値

    [Header("2DエイムUI設定")]
    public RectTransform aimGuide2D;  // 2Dの円枠
    public RectTransform aimCursor2D; // 2Dの動く点
    public float cursorSpeed = 200f;
    public float guideRadius = 100f;

    [Header("3Dエイムガイド設定")]
    public GameObject aimGuide3D;    // 3Dの円枠
    public Transform aimCursor3D;   // 3Dの動く点
    public float minGuideDist = 0.7f; // ガイドの最短配置距離
    public float maxGuideDist = 1.5f; // ガイドの最長配置距離
    public float guideSafetyBuffer = 0.15f; // ボール表面からのマージン

    [Header("ビジュアル・演出")]
    [SerializeField] private Transform stickTip;
    [SerializeField] private Transform visualAdjuster;
    public MeshRenderer ballRenderer;
    [ColorUsage(true, true)] public Color baseEmissionColor; // 初期の輝き（HDRカラー）

    [Header("警告UI設定")]
    // 警告用（ふわっと消えるパネル）
    public CanvasGroup warningCG;
    public TextMeshProUGUI warningText; // パネル内の文字を書き換える場合

    // 内部状態管理用
    private CharacterController controller;
    private TrajectoryPredictor predictor;
    private Vector2 moveInput;
    private bool isAiming = false;
    private bool isCharging = false;
    private float chargeTime = 0f;
    private Vector2 cursorOffset;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        predictor = GetComponent<TrajectoryPredictor>();

        if (powerSlider != null) powerSlider.gameObject.SetActive(false);

        if (aimGuide3D != null) aimGuide3D.SetActive(false);

        // 初期ポーズの設定
        UpdateStickPose();
    }

    void Start()
    {
        if (warningCG != null) warningCG.gameObject.SetActive(false);
        // 初期のボールの輝きをセット
        if (GameManager.gameManager != null)
        {
            UpdateBallGlow(0, GameManager.gameManager.maxStrokes);
        }
    }

    void OnMove(InputValue value)
    {
        if (DialogueManager.IsTalking) return;
        moveInput = value.Get<Vector2>();
    }

    void OnAim(InputValue value)
    {
        if (DialogueManager.IsTalking) return;

        if (isAiming)
        {
            // エイム解除と同時にチャージもリセット
            if (isCharging)
            {
                isCharging = false;
                chargeTime = 0;
                if (powerSlider != null) powerSlider.gameObject.SetActive(false);
            }

            isAiming = false;
            UpdateStickPose();
            return;
        }

        // ボールとの距離をチェックしてエイム可否を判定
        float distance = Vector3.Distance(transform.position, ball.transform.position);

        if (distance <= shotEnableDistance)
        {
            isAiming = true;
            UpdateStickPose();
        }
        else
        {
            // 範囲外の場合は警告演出を表示
            if (warningCG != null)
            {
                if (warningText != null) warningText.text = "※ボールから離れすぎています";
                StartCoroutine(ShowWarningRoutine());
            }
        }
    }

    private IEnumerator ShowWarningRoutine()
    {
        warningCG.transform.localPosition = Vector3.zero;

        // 表示（Alphaを1にする）
        warningCG.alpha = 1f;
        warningCG.gameObject.SetActive(true);

        // 1.5秒間そのまま表示を維持
        yield return new WaitForSeconds(1.5f);

        // 0.5秒かけてフェードアウト
        float fadeDuration = 0.5f;
        float currentTime = 0f;

        while (currentTime < fadeDuration)
        {
            currentTime += Time.deltaTime;
            // 1.0 から 0.0 へ徐々に変化させる
            warningCG.alpha = Mathf.Lerp(1f, 0f, currentTime / fadeDuration);

            // ふわっと浮きながら消える
            warningCG.transform.localPosition += new Vector3(0, 0.5f, 0);
            yield return null; // 1フレーム待つ
        }

        // 完全に消えたらオブジェクトを非アクティブにする
        warningCG.gameObject.SetActive(false);
    }

    void OnShot(InputValue value)
    {
        if (DialogueManager.IsTalking) return;

        if (!isAiming) return;

        if (!isCharging)
        {
            isCharging = true;
            chargeTime = 0;
            powerSlider.gameObject.SetActive(true);
        }
        else
        {
            LaunchBall();
            isCharging = false;
            powerSlider.gameObject.SetActive(false);
        }
    }

    void OnJump(InputValue value)
    {
        if (DialogueManager.IsTalking) return;

        // エイム中はジャンプできないように制限
        if (isAiming) return;

        if (controller.isGrounded)
        {
            // 指定した高さに基づいて初速を計算
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }
    }

    void Update()
    {
        // タイトルシーンや会話中は制御を無効化
        if (SceneManager.GetActiveScene().name == "TitleScene")
        {
            return;
        }

        if (DialogueManager.IsTalking) return;

        if (isAiming)
        {
            HandleStickAiming();
            // チャージ中なら現在値を、そうでなければ最大値を予測線に反映
            float previewPower = isCharging ? (powerSlider.value * maxShotPower) : maxShotPower;
            UpdateTrajectory(previewPower);
        }
        else
        {
            HandleMovement();
            predictor.HideArc();
        }

        // パワーゲージのピンポン運動制御
        // エイム中かつチャージ中のときだけ、ゲージを動かす
        if (isAiming && isCharging)
        {
            chargeTime += powerFillSpeed * Time.deltaTime;
            powerSlider.value = Mathf.PingPong(chargeTime, 1.0f);
        }
        else if (!isAiming)
        {
            // エイム中でなくなったら、念のためゲージを非表示にする
            if (powerSlider != null && powerSlider.gameObject.activeSelf)
                powerSlider.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// エイム状態に応じたスティックの持ち方、ガイドの表示切り替え
    /// </summary>
    private void UpdateStickPose()
    {
        if (isAiming)
        {
            if (aimGuide3D != null) aimGuide3D.SetActive(true);
            if (aimGuide2D != null) aimGuide2D.gameObject.SetActive(true);

            // UI等の初期化処理
            cursorOffset = Vector2.zero;

            // ボールとの位置関係に基づきガイドを動的に配置
            if (aimGuide3D != null)
            {
                // プレイヤーから見たボールへの方向ベクトルを取得
                Vector3 dirToBall = (ball.transform.position - transform.position).normalized;

                // ボールの半径を取得（Scaleの半分を半径とする）
                float ballRadius = ball.transform.localScale.x * 0.5f;

                // ボールの中心から、プレイヤー側に「半径 + 余白」分だけ戻った位置を計算。これにより常にボールの「表面」より手前に配置
                Vector3 targetWorldPos = ball.transform.position - dirToBall * (ballRadius + guideSafetyBuffer);

                // 計算したワールド座標を、プレイヤーから見たローカル座標に変換して適用
                Vector3 targetLocalPos = transform.InverseTransformPoint(targetWorldPos);

                // Z軸（前後）を Clamp して、プレイヤーにめり込みすぎないように制限
                float finalZ = Mathf.Clamp(targetLocalPos.z, minGuideDist, maxGuideDist);

                aimGuide3D.transform.localPosition = new Vector3(0, 0, finalZ);
            }

            if (aimCursor3D != null && stickPivot != null)
            {
                stickPivot.localRotation = Quaternion.Euler(0, 180, 0);
            }
        }
        else
        {
            if (aimGuide3D != null) aimGuide3D.SetActive(false);

            if (aimGuide2D != null) aimGuide2D.gameObject.SetActive(false);

            // 通常時のポーズ（脇差）
            stickPivot.localRotation = Quaternion.Euler(-20, 0, 0.4f);
        }
    }

    /// <summary>
    /// エイム中のスティック入力をカーソル位置とモデルの回転に反映
    /// </summary>
    private void HandleStickAiming()
    {
        if (isCharging) return;

        cursorOffset += moveInput * cursorSpeed * Time.deltaTime;

        // 円形可動範囲内にクランプ
        if (cursorOffset.magnitude > guideRadius)
        {
            cursorOffset = cursorOffset.normalized * guideRadius;
        }

        // 2D UIの更新
        if (aimCursor2D != null) aimCursor2D.anchoredPosition = cursorOffset;

        // 3Dカーソルの更新
        if (aimCursor3D != null)
        {
            float visualScale = 0.005f;
            float posX = cursorOffset.x * visualScale;
            float posY = -cursorOffset.y * visualScale;

            aimCursor3D.localPosition = new Vector3(posX, -1.5f, posY);

            // カーソル位置に合わせてスティックを視覚的に傾ける
            float rotX = cursorOffset.y / guideRadius * 25f;   // 上下の傾き
            float rotY = cursorOffset.x / guideRadius * 25f;    // 左右の傾き

            // スティックの回転を適用
            stickPivot.localRotation = Quaternion.Euler(rotX, 180 + rotY, 0);
        }
    }

    // ショットの実行
    private void LaunchBall()
    {
        Rigidbody ballRb = ball.GetComponent<Rigidbody>();
        Vector3 baseDir = transform.forward;

        // // カーソルのズレをショット角度の補正値に変換
        float sensitivity = 0.3f; // 角度の変化の強さ
        Vector3 upAdjust = transform.up * (-cursorOffset.y / guideRadius) * sensitivity;
        Vector3 rightAdjust = transform.right * (-cursorOffset.x / guideRadius) * sensitivity;

        // 最終的な発射ベクトル。基本方向に上下左右の補正を足して正規化
        Vector3 shotDir = (baseDir + upAdjust + rightAdjust).normalized;

        // 最大パワーまたは現在のパワーで発射
        float finalPower = isCharging ? (powerSlider.value * maxShotPower) : maxShotPower;

        // ボールの勢いをリセットしてから飛ばす
        ballRb.linearVelocity = Vector3.zero;
        ballRb.angularVelocity = Vector3.zero;

        // 計算した方向に力を加える
        ballRb.AddForce(shotDir * finalPower, ForceMode.Impulse);

        // 打数を加算
        if (GameManager.gameManager != null)
        {
            GameManager.gameManager.AddStroke();
        }

        // ショット後のリセット処理
        isAiming = false;       // エイムモード終了
        isCharging = false;     // チャージ中ならリセット
        predictor.HideArc();    // 予測線を消す
        UpdateStickPose();      // スティックのポーズ（脇差）とガイドの非表示を更新

        if (powerSlider != null) powerSlider.gameObject.SetActive(false);
    }

    // ボールの輝きを打数に応じて減らす
    public void UpdateBallGlow(int current, int max)
    {
        if (ballRenderer == null) return;

        // 残りの打数の割合を計算 (1.0 から 0.0 へ)
        // 受け取った値を使って計算
        float ratio = Mathf.Clamp01((float)(max - current) / max);

        // エミッションカラー（輝き）を計算
        // 打つほど黒（輝きなし）に近づく
        Color finalColor = baseEmissionColor * ratio;

        // マテリアルに反映
        ballRenderer.material.SetColor("_EmissionColor", finalColor);

        // リアルタイム反映を確実にするための更新
        DynamicGI.SetEmissive(ballRenderer, finalColor);
    }

    private void UpdateTrajectory(float power)
    {
        // キャラクターの正面を基準にする
        Vector3 baseDir = transform.forward;

        float sensitivity = 0.3f;
        Vector3 upAdjust = transform.up * (-cursorOffset.y / guideRadius) * sensitivity;
        Vector3 rightAdjust = transform.right * (-cursorOffset.x / guideRadius) * sensitivity;

        Vector3 shotDir = (baseDir + upAdjust + rightAdjust).normalized;

        // 予測線を描画
        predictor.RenderArc(ball.transform.position, shotDir * power);
    }

    private void HandleMovement()
    {
        Vector3 camF = Camera.main.transform.forward;
        Vector3 camR = Camera.main.transform.right;
        camF.y = 0; camR.y = 0;

        // 水平方向の移動計算
        Vector3 move = (camF.normalized * moveInput.y + camR.normalized * moveInput.x) * moveSpeed;

        // 地面チェックと垂直方向の速度リセット
        if (controller.isGrounded && verticalVelocity < 0)
        {
            // 地面にいたら、蓄積された重力を少しマイナスで固定（安定させるため）
            verticalVelocity = -2f;
        }

        // 重力の加算
        verticalVelocity += gravity * Time.deltaTime;

        // 水平移動に垂直方向の速度を合算
        move.y = verticalVelocity;

        // キャラクターを動かす
        controller.Move(move * Time.deltaTime);

        // 回転処理（移動している時だけ）
        if (new Vector3(move.x, 0, move.z).magnitude > 0.1f)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(new Vector3(move.x, 0, move.z)),
                rotationSpeed * Time.deltaTime);
        }
    }
}