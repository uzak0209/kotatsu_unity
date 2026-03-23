using UnityEngine;

public class GoalPost : MonoBehaviour
{
    private bool isGoalReached = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        // まだ誰もゴールしておらず、触れたのがプレイヤー（自機）の場合
        if (!isGoalReached && other.CompareTag("Player"))
        {
            isGoalReached = true;
            
            // 最新のUnity推奨メソッドに修正
            var flowManager = Object.FindFirstObjectByType<GameFlowManager>();
            
            if (flowManager != null)
            {
                flowManager.OnPlayerGoal("YOU");
            }
            else
            {
                Debug.LogWarning("GameFlowManagerが見つかりません");
            }
        }
    }
}