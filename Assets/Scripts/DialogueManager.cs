using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.InputSystem;

/// <summary>
/// ScriptableObject形式の台本データを再生する会話制御クラス
/// 入力ガード、文字送りスキップ、クリア後の特殊リザルト演出を含む
/// </summary>
public class DialogueManager : MonoBehaviour
{
    [Header("UI参照")]
    public GameObject dialoguePanel;     // 会話ウィンドウ全体
    public TextMeshProUGUI nameText;    // 名前表示用テキスト
    public TextMeshProUGUI messageText; // 本文表示用テキスト
    public GameObject choicePanel;      // 選択肢ウィンドウ（はい・いいえ）
    public RectTransform cursorRect;    // 選択肢を指す矢印（指アイコン）
    public GameObject blackBackground;  // 演出用の真っ黒な背景パネル
    public GameObject titleReturnButton;

    [Header("台本設定")]
    public float textSpeed = 0.05f;      // 文字の流れる速さ
    public TalkData openingTalkData;    // 最初の導入会話
    public TalkData reactionYes;        // 「はい」を選んだ時の反応
    public TalkData reactionNo1;        // 「いいえ」1回目の反応
    public TalkData reactionNo2;        // 「いいえ」2回目の反応
    public TalkData reactionNo3;        // 「いいえ」3回目の反応（ゲームオーバー直前）
    public TalkData explanationTalkData;

    [Header("カーソル位置設定（AnchoredPosition Y）")]
    public float cursorY_Yes = 36f;     // 「はい」の横に矢印が来る時の高さ
    public float cursorY_No = -28f;     // 「いいえ」の横に矢印が来る時の高さ

    [Header("UI入力参照")]
    public PlayerInput playerInput;
    private InputAction navigateAction;
    private InputAction submitAction;

    // 内部状態管理用
    private int currentIndex = 0;       // 現在何行目のセリフか
    private bool isDialogueActive = false; // 会話中フラグ
    private bool isTyping = false;      // 文字が流れている最中か
    private string currentFullMessage = ""; // 現在表示中の全文（スキップ用）
    private int refuseCount = 0;        // 「いいえ」を選んだ回数

    private bool isSelecting = false;   // 選択肢を選んでいる最中か
    private bool isYesSelected = true;  // 現在「はい」を選択中か（falseなら「いいえ」）
    private Coroutine mainSequenceRoutine; // 会話の流れを管理するコルーチン
    private Coroutine typingRoutine; // 文字入力コルーチンを個別に管理するための変数

    [Header("SE設定")]
    public AudioSource audioSource;     // 音を鳴らすためのコンポーネント
    public AudioClip popSound;          // 文字が流れる時の「ポポポ」音
    public AudioClip selectSE;          // カーソルを動かした時の音
    public AudioClip decisionSE;        // 決定ボタンを押した時の音
    [Range(0.1f, 2.0f)]
    public float pitchRange = 0.1f;     // ポポポ音にゆらぎを出す幅

    private bool isPostClear = false; // クリア後の会話かどうかのフラグ
    private TalkData currentTalkData; // 現在再生中の会話データ
    private bool canInput = false;  // 入力突き抜け防止用フラグ

    private enum DialogueState { Opening, Selecting, Explaining, Ended }
    private DialogueState currentState = DialogueState.Opening;

    // プレイヤーの移動を止めるために外部から参照するフラグ
    public static bool IsTalking { get; private set; }

    void Awake()
    {
        var uiActions = playerInput.actions.FindActionMap("UI");
        navigateAction = uiActions.FindAction("Navigate");
        submitAction = uiActions.FindAction("Submit");
    }

    void Start()
    {
        if (choicePanel != null) choicePanel.SetActive(false);

        if (openingTalkData != null) StartDialogue(openingTalkData, false);
    }

    void Update()
    {
        if (!isDialogueActive || currentState == DialogueState.Ended) return;

        switch (currentState)
        {
            case DialogueState.Opening:
                HandleDialogueInput();
                break;
            case DialogueState.Selecting:
                HandleChoiceInput();
                break;
            case DialogueState.Explaining:
                HandleExplainingInput();
                break;
        }
    }

    // 説明パート専用の入力受付
    void HandleExplainingInput()
    {
        if (submitAction.triggered && isTyping)
        {
            FinishTypingEarly(); // スキップ：説明中も全文表示が可能に
        }
    }

    // 文字表示を即座に終わらせる共通メソッド
    void FinishTypingEarly()
    {
        if (typingRoutine != null) StopCoroutine(typingRoutine);
        messageText.text = currentFullMessage;
        isTyping = false;
    }

    /// <summary>
    /// 会話を開始する。外部からも呼び出し可能。
    /// </summary>
    public void StartDialogue(TalkData data, bool isPostClearMode = false)
    {
        // 前の会話の残骸をリセット
        if (mainSequenceRoutine != null) StopCoroutine(mainSequenceRoutine);
        if (typingRoutine != null) StopCoroutine(typingRoutine);

        // 渡されたデータを現在の会話データとして保持
        currentTalkData = data;
        isPostClear = isPostClearMode;
        currentIndex = 0; // 0行目から開始

        // 状態を「会話中」にセットする
        currentState = DialogueState.Opening;

        isDialogueActive = true;
        IsTalking = true;
        dialoguePanel.SetActive(true);
        if (blackBackground != null) blackBackground.SetActive(true);

        // 入力アクションを有効化
        if (submitAction != null) submitAction.Enable();

        // 開始直後の決定ボタン誤爆を防ぐため、僅かなディレイを挟む
        canInput = false;
        StartCoroutine(EnableInputDelay()); // 0.1秒後に入力を許可

        // 最初の行を表示
        DisplayNextSentence();
    }

    IEnumerator EnableInputDelay()
    {
        yield return new WaitForSeconds(0.1f);
        canInput = true;
    }

    /// <summary>
    /// 選択肢の入力処理
    /// </summary>
    void HandleChoiceInput()
    {
        // 選択肢の上下操作
        Vector2 navigation = navigateAction.ReadValue<Vector2>();
        if (navigation.y > 0.5f && !isYesSelected) { isYesSelected = true; PlaySE(selectSE); }
        else if (navigation.y < -0.5f && isYesSelected) { isYesSelected = false; PlaySE(selectSE); }

        cursorRect.anchoredPosition = new Vector2(cursorRect.anchoredPosition.x, isYesSelected ? cursorY_Yes : cursorY_No);

        if (submitAction.triggered)
        {
            PlaySE(decisionSE);
            choicePanel.SetActive(false);

            if (isYesSelected)
            {
                // 「はい」なら「説明状態」へ移行
                currentState = DialogueState.Explaining;
                StartCoroutine(StartGameRoutine());
            }
            else
            {
                // 「いいえ」の時は状態を維持（※内部でPlayReactionが終わればまたSelectingに戻る）
                HandleNoSelection();
            }
        }
    }

    /// <summary>
    /// 「はい」を選んだあとの物語とゲームを繋ぐ一連の流れ
    /// </summary>
    IEnumerator StartGameRoutine()
    {
        if (explanationTalkData != null)
        {
            // 第二引数 true で、終わったら EndDialogue へ
            yield return StartCoroutine(PlayReaction(explanationTalkData, true));
        }
        else
        {
            EndDialogue();
        }
    }

    /// <summary>
    /// 「いいえ」を選んだ時の回数別分岐。
    /// </summary>
    void HandleNoSelection()
    {
        refuseCount++;
        if (refuseCount == 1) StartCoroutine(PlayReaction(reactionNo1, false));
        else if (refuseCount == 2) StartCoroutine(PlayReaction(reactionNo2, false));
        else StartCoroutine(PlayReaction(reactionNo3, false, true)); // 3回「いいえ」でゲームオーバー
    }

    /// <summary>
    /// 選択後の反応を再生（「いいえ」の自動選択肢復帰対応版）
    /// </summary>
    IEnumerator PlayReaction(TalkData data, bool isEnd, bool isGameOver = false)
    {
        // 決定ボタンを離すまで待機
        yield return new WaitUntil(() => !submitAction.IsPressed());

        for (int i = 0; i < data.talks.Length; i++)
        {
            var talk = data.talks[i];
            currentState = DialogueState.Explaining;
            nameText.text = talk.speakerName;
            currentFullMessage = talk.message;

            // 文字表示を開始
            typingRoutine = StartCoroutine(TypeText(currentFullMessage));
            // 文字表示が終わるのを待つ（スキップ対応）
            while (isTyping) yield return null;

            // 最後の行かどうか、および「いいえ」ルートかどうかの判定
            bool isLastLine = (i == data.talks.Length - 1);

            // 「いいえ」ルート（isEnd=false）かつ 最後の行 かつ ゲームオーバーでない場合
            if (isLastLine && !isEnd && !isGameOver)
            {
                // ボタン入力を待たずに、0.8秒待機して次の処理（選択肢表示）へ
                yield return new WaitForSeconds(0.8f);
            }
            else
            {
                // それ以外（途中の行や、「はい」の最後の行）は、プレイヤーのボタン入力を待つ
                yield return null;
                yield return new WaitUntil(() => submitAction.triggered);
                yield return null;
            }
        }

        // 全てのセリフ表示後の分岐
        if (isGameOver)
        {
            if (GameManager.gameManager != null) GameManager.gameManager.GoToGameOver();
        }
        else if (isEnd)
        {
            // 「はい」のルートなら、会話を終了
            EndDialogue();
        }
        else
        {
            // 「いいえ」のルートなら、再び選択肢を表示
            currentState = DialogueState.Selecting;
            ShowChoices();
        }
    }

    /// <summary>
    /// 通常のセリフ送り処理
    /// </summary>
    void HandleDialogueInput()
    {
        // ガードが入っている間は入力を受け付けない
        if (!canInput) return;

        if (submitAction.triggered)
        {
            if (isTyping)
            {
                FinishTypingEarly(); // スキップ：文字表示だけを止める
            }
            else
            {
                // 台本の末尾に到達している場合はコルーチン側の処理を優先する
                if (currentIndex >= currentTalkData.talks.Length) return;

                DisplayNextSentence();
            }
        }
    }

    /// <summary>
    /// 次のセリフを表示。
    /// </summary>
    void DisplayNextSentence()
    {
        // 進行中のメインの流れがあれば止める（二重再生防止）
        if (mainSequenceRoutine != null) StopCoroutine(mainSequenceRoutine);

        // すでに全セリフ出し終わっているなら、即座に選択肢へ
        if (currentIndex >= currentTalkData.talks.Length)
        {
            // クリア後なら選択肢は絶対に出さない
            if (isPostClear) return;

            currentState = DialogueState.Selecting;
            ShowChoices();
            return;
        }

        // 次のセリフの流れを開始
        mainSequenceRoutine = StartCoroutine(OpeningDialogueSequence());
    }

    IEnumerator OpeningDialogueSequence()
    {
        // 念のためここでも状態をセット（あるいは維持）
        currentState = DialogueState.Opening;

        nameText.text = currentTalkData.talks[currentIndex].speakerName;
        currentFullMessage = currentTalkData.talks[currentIndex].message;
        currentIndex++;

        // 文字表示を開始
        typingRoutine = StartCoroutine(TypeText(currentFullMessage));

        // 文字表示が終わるのを待つ（※スキップされてもここは通過）
        while (isTyping) yield return null;

        // 最後のセリフだった場合、自動で選択肢へ
        if (currentIndex >= currentTalkData.talks.Length)
        {
            // 最後のセリフを読み終わった後の「溜め」
            yield return new WaitForSeconds(0.8f);

            if (isPostClear)
            {
                // クリア後の最終演出：ボタン表示と終了入力を待機
                yield return new WaitForSeconds(0.8f);

                titleReturnButton.SetActive(true);

                // 入力を待つ
                yield return new WaitUntil(() => submitAction.triggered);

                PlaySE(decisionSE); // 最後に決定音を鳴らす
                EndDialogue();
                yield break;
            }
            else
            {
                yield return new WaitForSeconds(0.8f);
                // 導入時は選択肢へ
                currentState = DialogueState.Selecting;
                ShowChoices();
            }
        }
    }

    IEnumerator DialogueSequenceRoutine()
    {
        // 文字表示を開始し、その参照を保存
        typingRoutine = StartCoroutine(TypeText(currentFullMessage));

        // 文字表示（TypeText）が終わるまで待機
        yield return typingRoutine;

        // もしこれが最後のセリフなら、自動で選択肢へ移行
        if (currentIndex >= openingTalkData.talks.Length)
        {
            yield return new WaitForSeconds(0.8f); // 読了の「間」
            currentState = DialogueState.Selecting;
            ShowChoices();
        }
    }

    // 導入会話専用のコルーチン
    IEnumerator OpeningDialogueRoutine()
    {
        // 文字を1文字ずつ表示
        yield return StartCoroutine(TypeText(currentFullMessage));

        // もしこれが導入パートの最後のセリフだったら、自動で選択肢へ
        if (currentIndex >= openingTalkData.talks.Length)
        {
            yield return new WaitForSeconds(0.8f); // 読了の「間」
            currentState = DialogueState.Selecting;
            ShowChoices();
        }
    }

    // セリフを表示し、最後なら自動で選択肢を出すコルーチン
    IEnumerator DisplaySentenceAndThenCheckChoices()
    {
        yield return StartCoroutine(TypeText(currentFullMessage));

        // もしこれが導入パートの最後のセリフだったら
        if (currentIndex >= openingTalkData.talks.Length)
        {
            yield return new WaitForSeconds(0.8f); // 読了のための少しの「間」
            currentState = DialogueState.Selecting;
            ShowChoices();
        }
    }

    /// <summary>
    /// 1文字ずつ文字を表示するメインコルーチン。
    /// </summary>
    IEnumerator TypeText(string text)
    {
        isTyping = true;
        messageText.text = "";
        foreach (char letter in text.ToCharArray())
        {
            messageText.text += letter;

            // SE再生（スペース以外の文字の時だけ鳴らす）
            if (audioSource != null && popSound != null && letter != ' ' && letter != '　')
            {
                audioSource.pitch = 1.0f + Random.Range(-pitchRange, pitchRange);
                audioSource.PlayOneShot(popSound);
            }

            // 句読点、「！」、「？」では少し溜めを作る演出
            if (letter == '。' || letter == '、' || letter == '？' || letter == '！')
            {
                yield return new WaitForSeconds(textSpeed * 3);
            }
            else
            {
                yield return new WaitForSeconds(textSpeed);
            }
        }
        isTyping = false;
    }

    /// <summary>
    /// 名前も同時に更新してタイピングを開始する便利メソッド。
    /// </summary>
    IEnumerator TypeTextCustom(string speaker, string msg)
    {
        nameText.text = speaker;
        yield return StartCoroutine(TypeText(msg));
    }

    /// <summary>
    /// SE再生用の共通メソッド。
    /// </summary>
    void PlaySE(AudioClip clip)
    {
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
    }

    /// <summary>
    /// 選択肢ウィンドウを表示。
    /// </summary>
    void ShowChoices()
    {
        // 1フレーム待ってから選択肢を出すことで、決定ボタンの「突き抜け」を完全に防ぐ
        StartCoroutine(ShowChoicesRoutine());
    }

    IEnumerator ShowChoicesRoutine()
    {
        // 直前の決定ボタン入力が選択肢に干渉しないようにディレイを入れる
        yield return new WaitForSeconds(0.1f);

        isSelecting = true;
        isYesSelected = true;
        if (choicePanel != null)
        {
            choicePanel.SetActive(true);
            cursorRect.anchoredPosition = new Vector2(cursorRect.anchoredPosition.x, cursorY_Yes);
        }
        else
        {
            EndDialogue();
        }
    }

    /// <summary>
    /// 会話を終了し、ゲームの世界を表示する。
    /// </summary>
    void EndDialogue()
    {
        currentState = DialogueState.Ended; // 状態を終了にする
        IsTalking = false;
        isDialogueActive = false;
        dialoguePanel.SetActive(false);
        if (blackBackground != null) blackBackground.SetActive(false);

        // クリア後の会話が終わったなら、タイトルに戻る
        if (isPostClear)
        {
            if (GameManager.gameManager != null) GameManager.gameManager.BackToTitle();
        }
    }
}