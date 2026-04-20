using UnityEngine;

public class UIController : MonoBehaviour
{
    private CanvasGroup canvasGroup;

    void Awake()
    {
        // 自身についているCanvas Groupを取得
        canvasGroup = GetComponent<CanvasGroup>();
    }

    void Update()
    {
        // 会話中ならアルファ（不透明度）を0にし、そうでなければ1にする
        if (DialogueManager.IsTalking)
        {
            canvasGroup.alpha = 0f;
        }
        else
        {
            canvasGroup.alpha = 1f;
        }
    }
}