using UnityEngine;

public class LogManager : MonoBehaviour
{
    [SerializeField, Header("生成するログのプレハブ")] 
    private LogItem logItemPrefab;
    
    [SerializeField, Header("ログを並べる親オブジェクト(Canvas等)")] 
    private Transform logParent;

    [SerializeField, Header("ログが消えるまでの時間(秒)")] 
    private float displayTime = 3.0f;
    [SerializeField]
    private Sprite[] beforeSprites;
    [SerializeField]
    private string[] afterStrings;

    // 外部からログを表示したい時に呼ぶメソッド
    public void ShowLog(int beforeIndex, int afterIndex)
    {
        // プレハブを生成
        LogItem newLog = Instantiate(logItemPrefab, logParent);
        
        // 画像と消滅時間をセットして初期化
        newLog.Setup(beforeSprites[beforeIndex], afterStrings[afterIndex], displayTime);
    }
}