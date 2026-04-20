using UnityEngine;
using UnityEngine.InputSystem;
using Cinemachine;

public class CameraController : MonoBehaviour
{
    [Header("カメラ設定")]
    // VirtualCamera ではなく FreeLook を使うように変更
    public CinemachineFreeLook freeLookCamera;
    public Transform playerTransform;
    public Transform ballTransform;

    private bool isBallTargetMode = false;

    public void OnCameraReset(InputValue value)
    {
        if (!value.isPressed || freeLookCamera == null) return;

        isBallTargetMode = !isBallTargetMode;

        if (isBallTargetMode && ballTransform != null)
        {
            LookAtBall();
        }
        else
        {
            ResetToForward();
        }
    }

    private void ResetToForward()
    {
        // プレイヤーの正面向きにリセット
        freeLookCamera.m_XAxis.Value = playerTransform.eulerAngles.y;
        freeLookCamera.m_YAxis.Value = 0.5f; // 真ん中の高さ
        Debug.Log("カメラ：正面リセット");
    }

    private void LookAtBall()
    {
        Vector3 direction = ballTransform.position - playerTransform.position;
        float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;

        freeLookCamera.m_XAxis.Value = targetAngle;
        freeLookCamera.m_YAxis.Value = 0.5f;
        Debug.Log("カメラ：ボール注視");
    }
}