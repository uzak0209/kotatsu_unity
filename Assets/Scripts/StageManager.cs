using UnityEngine;

public class StageManager : MonoBehaviour
{
    [SerializeField] private GameObject startArea;
    [SerializeField] private GameObject goalArea;
    [SerializeField] private GameObject[] stageChunks;
    [SerializeField] private int chunkCount = 5;
    [SerializeField] private float chunkWidth = 19.2f; // 各ステージの横幅

    void Awake()
    {
        GenerateStage();
    }

    void GenerateStage()
    {
        float currentX = 0;

        // 1. スタート地点
        Instantiate(startArea, new Vector3(currentX, 0, 0), Quaternion.identity);
        currentX += chunkWidth;

        // 2. ランダムに5つ配置
        for (int i = 0; i < chunkCount; i++)
        {
            GameObject selected = stageChunks[Random.Range(0, stageChunks.Length)];
            Instantiate(selected, new Vector3(currentX, 0, 0), Quaternion.identity);
            currentX += chunkWidth * 2;
        }

        // 3. ゴール地点
        Instantiate(goalArea, new Vector3(currentX, 0, 0), Quaternion.identity);
    }
}