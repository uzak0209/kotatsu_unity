using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target; 
    [SerializeField] private float smoothSpeed = 1.0f; 
    [SerializeField] private Vector3 offset = new Vector3(0, 0, -10);

    void LateUpdate()
    {
        if (target == null) return;

        // 目標とするX座標
        float targetX = target.position.x + offset.x;

        // 現在のカメラのXから目標のXへ滑らかに補間
        float smoothedX = Mathf.Lerp(transform.position.x, targetX, smoothSpeed);

        // カメラの位置を更新
        transform.position = new Vector3(smoothedX, transform.position.y, offset.z);
    }
}