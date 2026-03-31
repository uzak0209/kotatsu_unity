using UnityEngine;
using UnityEngine.InputSystem;
using Kotatsu.Network;


public class UII : MonoBehaviour//, UIControler
{
    private int currentIndex = 0;
    void Update() {
        if (Keyboard.current.upArrowKey.wasPressedThisFrame || Keyboard.current.wKey.wasPressedThisFrame) {
            currentIndex = (currentIndex - 1 + selectObject.Length) % selectObject.Length;
        } else if (Keyboard.current.downArrowKey.wasPressedThisFrame || Keyboard.current.sKey.wasPressedThisFrame) {
            currentIndex = (currentIndex + 1) % selectObject.Length;
        }
        Debug.Log($"Current selected index: {currentIndex}");
        SetSelected(currentIndex);
    }

    [SerializeField] private GameObject[] selectObject;
    
    public void SetSelected(int index) {
        for (int i = 0; i < selectObject.Length; i++) {
            selectObject[i].SetActive(i == index);
        }
    }
}
