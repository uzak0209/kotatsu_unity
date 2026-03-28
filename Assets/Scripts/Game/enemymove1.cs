using UnityEngine;
using System.Collections;

public class enemymove1 : MonoBehaviour
{
    public float speed = 1f; // 敵の移動速度
    public float movetime = 1f; // 移動時間
    void Start()
    {
        StartCoroutine( MoveRoutine() );
    }

    IEnumerator MoveRoutine()
    {
        while (true)
        {
            // 右に移動
            float elapsed = 0f;
            while (elapsed < movetime)
            {
                transform.Translate(Vector2.right * speed * Time.deltaTime);
                elapsed += Time.deltaTime;
                yield return null;
            }

            // 左に移動
            elapsed = 0f;
            while (elapsed < movetime)
            {
                transform.Translate(Vector2.left * speed * Time.deltaTime);
                elapsed += Time.deltaTime;
                yield return null;
            }
        }
    }
}
