using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class StageManager : MonoBehaviour
{
    [System.Serializable]
    public struct CharacterStageSet
    {
        public string characterName;
        public GameObject startArea;
        public GameObject goalArea;
        public GameObject[] stageChunks; // 各キャラごとのステージパーツ（n個）
    }

    [Header("Visual Settings")]
    [SerializeField] private CharacterStageSet[] characterStages; // 4体分
    [SerializeField] private int selectedCharacterIndex = 0;

    [Header("Generation Settings")]
    [SerializeField] private int chunkCount = 5;
    [SerializeField] private float chunkWidth = 19.2f;

    void Awake()
    {
        // プレイヤーの選択を自動取得（存在すれば）
        PlayerController player = Object.FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            selectedCharacterIndex = player.selectedCharacterIndex;
        }

        GenerateStage();
    }

    void GenerateStage()
    {
        if (characterStages.Length <= selectedCharacterIndex) return;

        CharacterStageSet currentSet = characterStages[selectedCharacterIndex];
        float currentX = 0;

        // 1. 指定色のスタート地点
        Instantiate(currentSet.startArea, new Vector3(currentX, 0, 0), Quaternion.identity);
        currentX += chunkWidth;

        // 2. 重複なしでランダムに選ぶ
        // リスト化してシャッフルする
        List<GameObject> availableChunks = new List<GameObject>(currentSet.stageChunks);
        Shuffle(availableChunks);

        // 用意されたパーツ数以上のchunkCountが指定された場合のための安全策
        int actualGenCount = Mathf.Min(chunkCount, availableChunks.Count);

        for (int i = 0; i < actualGenCount; i++)
        {
            GameObject selected = availableChunks[i];
            Instantiate(selected, new Vector3(currentX, 0, 0), Quaternion.identity);
            currentX += chunkWidth * 2; // パーツ間のスペースを空ける
        }

        // 3. 指定色のゴール地点
        Instantiate(currentSet.goalArea, new Vector3(currentX, 0, 0), Quaternion.identity);
    }

    // リストをランダムに並び替える（フィッシャー・イェーツのシャッフル）
    private void Shuffle(List<GameObject> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            GameObject temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }
}