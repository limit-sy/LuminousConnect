using System;
using UnityEngine;

[Serializable]
public class Talk
{
    public string speakerName; // 話者の名前
    [TextArea(3, 10)]
    public string message;     // セリフ
}

[CreateAssetMenu(fileName = "TalkData", menuName = "ScriptableObjects/TalkData")]
public class TalkData : ScriptableObject
{
    public Talk[] talks;
}