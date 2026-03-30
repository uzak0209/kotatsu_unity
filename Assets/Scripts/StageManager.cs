using System;
using System.Collections.Generic;
using UnityEngine;
using Kotatsu.Network;

public class StageManager : MonoBehaviour
{
    [Serializable]
    public struct CharacterStageSet
    {
        public string characterName;
        public GameObject startArea;
        public GameObject goalArea;
        public GameObject[] stageChunks;
    }

    public static StageManager Instance { get; private set; }

    [Header("Visual Settings")]
    [SerializeField] private CharacterStageSet[] characterStages;
    [SerializeField] private int selectedCharacterIndex;

    [Header("Generation Settings")]
    [SerializeField] private int chunkCount = 5;
    [SerializeField] private float chunkWidth = 19.2f;

    private NetworkManager networkManager;
    private int[] generatedStageOrder = Array.Empty<int>();
    private int generatedStageCount;

    public int[] GeneratedStageOrder
    {
        get
        {
            int[] copy = new int[generatedStageOrder.Length];
            Array.Copy(generatedStageOrder, copy, copy.Length);
            return copy;
        }
    }

    private void Awake()
    {
        Instance = this;
        networkManager = FindAnyObjectByType<NetworkManager>();
        ResolveLayoutFromNetwork();
        GenerateStage();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void ResolveLayoutFromNetwork()
    {
        if (networkManager != null &&
            networkManager.TryGetPlayerMatchState(networkManager.CurrentPlayerId, out MatchPlayerState playerState))
        {
            selectedCharacterIndex = Mathf.Clamp(playerState.color_index, 0, Mathf.Max(0, characterStages.Length - 1));
            generatedStageOrder = SanitizeStageOrder(playerState.stage_order);
            if (generatedStageOrder.Length > 0)
            {
                chunkCount = generatedStageOrder.Length;
            }
            return;
        }

        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            selectedCharacterIndex = player.selectedCharacterIndex;
        }
    }

    private void GenerateStage()
    {
        if (characterStages == null || characterStages.Length <= selectedCharacterIndex)
        {
            return;
        }

        CharacterStageSet currentSet = characterStages[selectedCharacterIndex];
        float currentX = 0f;

        Instantiate(currentSet.startArea, new Vector3(currentX, 0f, 0f), Quaternion.identity);
        currentX += chunkWidth;

        List<int> stageIndices = BuildStageIndices(currentSet);
        generatedStageOrder = stageIndices.ToArray();
        generatedStageCount = generatedStageOrder.Length;

        for (int i = 0; i < generatedStageOrder.Length; i++)
        {
            int stageIndex = generatedStageOrder[i];
            GameObject selected = currentSet.stageChunks[stageIndex];
            Instantiate(selected, new Vector3(currentX, 0f, 0f), Quaternion.identity);
            currentX += chunkWidth * 2f;
        }

        Instantiate(currentSet.goalArea, new Vector3(currentX, 0f, 0f), Quaternion.identity);
    }

    private List<int> BuildStageIndices(CharacterStageSet currentSet)
    {
        List<int> indices = new List<int>();

        if (generatedStageOrder != null && generatedStageOrder.Length > 0)
        {
            for (int i = 0; i < generatedStageOrder.Length; i++)
            {
                int candidate = generatedStageOrder[i];
                if (candidate < 0 || candidate >= currentSet.stageChunks.Length || indices.Contains(candidate))
                {
                    continue;
                }

                indices.Add(candidate);
            }
        }

        if (indices.Count > 0)
        {
            return indices;
        }

        List<int> available = new List<int>();
        for (int i = 0; i < currentSet.stageChunks.Length; i++)
        {
            available.Add(i);
        }

        Shuffle(available);
        int actualGenCount = Mathf.Min(chunkCount, available.Count);
        if (actualGenCount < available.Count)
        {
            available.RemoveRange(actualGenCount, available.Count - actualGenCount);
        }

        return available;
    }

    private int[] SanitizeStageOrder(int[] stageOrder)
    {
        if (stageOrder == null || stageOrder.Length == 0)
        {
            return Array.Empty<int>();
        }

        List<int> sanitized = new List<int>();
        for (int i = 0; i < stageOrder.Length; i++)
        {
            if (!sanitized.Contains(stageOrder[i]))
            {
                sanitized.Add(stageOrder[i]);
            }
        }
        return sanitized.ToArray();
    }

    public int GetCurrentStageIndexForPosition(float worldX)
    {
        if (generatedStageCount <= 0)
        {
            return 0;
        }

        if (worldX < chunkWidth)
        {
            return 0;
        }

        float generatedEndX = chunkWidth + generatedStageCount * chunkWidth * 2f;
        if (worldX >= generatedEndX)
        {
            return generatedStageCount + 1;
        }

        int stageIndex = Mathf.FloorToInt((worldX - chunkWidth) / (chunkWidth * 2f));
        return Mathf.Clamp(stageIndex + 1, 1, generatedStageCount);
    }

    private void Shuffle(List<int> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            int temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }
}
