using UnityEngine;
using System.Collections.Generic;

public class TrajectoryPredictor : MonoBehaviour
{
    [Header("設定")]
    public LineRenderer lineRenderer;
    public int maxPoints = 30;         // 線の滑らかさ（点の数）
    public float timeStep = 0.1f;     // 点と点の間隔（秒）

    /// <summary>
    /// 重力を考慮した物理計算を行い、予測線を描画する
    /// </summary>
    public void RenderArc(Vector3 startPosition, Vector3 velocity)
    {
        lineRenderer.enabled = true;
        lineRenderer.positionCount = maxPoints;
        Vector3[] points = new Vector3[maxPoints];

        for (int i = 0; i < maxPoints; i++)
        {
            float time = i * timeStep;
            // 物理公式(x = v0t + 1/2at^2)に基づき各座標を算出
            Vector3 point = startPosition + velocity * time + 0.5f * Physics.gravity * time * time;
            points[i] = point;

            // 地面(y=0)との接触時に描画を終了する
            if (i > 0 && points[i].y < 0)
            {
                lineRenderer.positionCount = i + 1;
                break;
            }
        }
        lineRenderer.SetPositions(points);
    }

    public void HideArc()
    {
        lineRenderer.enabled = false;
    }
}