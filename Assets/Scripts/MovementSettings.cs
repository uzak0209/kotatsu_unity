using UnityEngine;

[CreateAssetMenu(fileName = "NewMovementSettings", menuName = "Settings/MovementSettings")]
public class MovementSettings : ScriptableObject
{
    public float moveSpeed = 10f;    // 基本速度
    public float jumpForce = 12f;   // ジャンプ力
    public float friction = 10f;    // 摩擦（減速の強さ）
    public float gravityScale = 3f; // 重力の倍率
}
// ここの値を動的に変更していく