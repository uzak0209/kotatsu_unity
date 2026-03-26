using UnityEngine;

public class PlayerRespawn : MonoBehaviour
{
    private Vector3 lastCheckpointPos;
    private Rigidbody2D rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        // 最初のリスポーン地点を開始位置に設定
        lastCheckpointPos = transform.position;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 死ぬオブジェクトに触れた場合
        if (other.CompareTag("Deadly"))
        {
            Respawn();
        }
        
        // チェックポイントに触れた場合（別途タグ "Checkpoint" を設定）
        if (other.CompareTag("Checkpoint"))
        {
            lastCheckpointPos = other.transform.position;
        }
    }

    public void Respawn()
    {
        transform.position = lastCheckpointPos;
        rb.linearVelocity = Vector2.zero; // 勢いをリセット
    }
}
