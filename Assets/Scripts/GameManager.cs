using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement; // 【追加】シーン管理に必要
using UnityEngine.InputSystem; // 追加


public class GameManager : MonoBehaviour
{
    public static GameManager gameManager;

    [Header("ゲームルール")]
    public int parStrokes = 3;
    public int maxStrokes = 5;
    private int currentStrokes = 0;

    [Header("UI参照")]
    public TextMeshProUGUI strokeText;
    public GameObject resultUI; // リザルトパネルを紐付ける用
    public TextMeshProUGUI finalScoreText; // リザルト用のテキスト枠

    [Header("Input System")]
    public InputActionReference submitAction; // 安定性の高いReference形式を採用

    [Header("Dialogue Reference")]
    public DialogueManager dialogueManager;
    public TalkData postClearTalkData;

    void Awake()
    {
        // シングルトンパターンの実装
        if (gameManager == null)
        {
            gameManager = this;
            // DontDestroyOnLoad(gameObject); // シーンを跨いで打数などを保持したい場合はコメント解除
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        UpdateUI();
        // 開始時はリザルトを隠しておく（念のため）
        if (resultUI != null) resultUI.SetActive(false);
    }

    void OnEnable()
    {
        if (submitAction != null) submitAction.action.Enable();
    }

    void OnDisable()
    {
        if (submitAction != null) submitAction.action.Disable();
    }

    // DialogueManagerから呼ばれる「ゲームオーバーシーンへ」の処理
    public void GoToGameOver()
    {
        SceneManager.LoadScene("GameOver");
    }

    // GameOverシーンのリトライボタンから呼ばれる処理
    public void RetryFromFirstStage()
    {
        // シーン遷移時に入力が無効化されるのを防ぐ
        if (submitAction != null) submitAction.action.Enable();

        // 打数などをリセット
        currentStrokes = 0;
        SceneManager.LoadScene("1stStage");
    }

    void Update()
    {
        // タイトル画面でのスタート入力待ち
        if (SceneManager.GetActiveScene().name == "TitleScene")
        {
            if (submitAction != null && submitAction.action.triggered)
            {
                StartGame();
            }
        }
    }

    public void StartGame()
    {
        SceneManager.LoadScene("1stStage");
    }

    public void AddStroke()
    {
        currentStrokes++;
        UpdateUI();

        // 打数に応じてボールの発光演出を更新
        PlayerController player = Object.FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            player.UpdateBallGlow(currentStrokes, maxStrokes);
        }
    }

    public void UpdateUI()
    {
        if (strokeText != null)
        {
            strokeText.text = $"SHOTS: {currentStrokes}\nPAR: {parStrokes}";
            strokeText.color = (currentStrokes > parStrokes) ? Color.red : Color.white;
        }
    }

    // GoalArea.cs から呼ばれるメソッド
    public void OnGoal()
    {
        // リザルト画面のテキストに、最終的な打数を流し込む
        if (finalScoreText != null)
        {
            finalScoreText.text = $"Total Shots: {currentStrokes}";
        }

        resultUI.SetActive(true);

        // ゲームパッド操作を考慮し、表示時にボタンへフォーカスを強制する
        UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(resultUI.GetComponentInChildren<UnityEngine.UI.Button>().gameObject);

    }

    // リザルト画面のボタン用（タイトルへ戻る）
    public void BackToTitle()
    {
        SceneManager.LoadScene("TitleScene");
    }

    // リザルトUIの「次へ」ボタンから呼ぶメソッド
    public void OnNextButtonClicked()
    {
        if (resultUI != null) resultUI.SetActive(false);

        if (dialogueManager != null && postClearTalkData != null)
        {
            // クリア後専用モードで会話を開始
            dialogueManager.StartDialogue(postClearTalkData, true);
        }
        else
        {
            // 万が一データがない場合はタイトルへ
            BackToTitle();
        }
    }
}