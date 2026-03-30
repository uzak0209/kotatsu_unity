using UnityEngine;

[CreateAssetMenu(fileName = "NewMovementSettings", menuName = "Settings/MovementSettings")]
public class MovementSettings : ScriptableObject
{
    public float moveSpeed = 8f;    // 基本速度
    public float jumpForce = 16f;   // ジャンプ力(固定値)
    public float lowJumpMultiplier = 2.5f; // ジャンプ中にボタンを離したときの重力倍率
    public float friction = 8f;    // 摩擦
    public float gravityScale = 8f; // 重力の倍率
}
// ここの値を動的に変更していく