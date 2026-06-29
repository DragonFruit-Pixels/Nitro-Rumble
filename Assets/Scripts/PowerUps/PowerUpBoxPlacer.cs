using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[DisallowMultipleComponent]
public class PowerUpBoxPlacer : MonoBehaviour
{
    [Header("Prefab")]
    [SerializeField] private GameObject _boxPrefab;

    [Header("Layout")]
    [SerializeField] private int _rows = 1;
    [SerializeField] private int _cols = 3;
    [SerializeField] private float _spacingX = 5f;
    [SerializeField] private float _spacingZ = 5f;

    [ContextMenu("Place Boxes")]
    public void PlaceBoxes()
    {
        if (_boxPrefab == null)
        {
            Debug.LogWarning("[PowerUpBoxPlacer] _boxPrefab is not assigned.", this);
            return;
        }

        ClearBoxes();

        int id = GetNextAvailableId();
        float offsetX = (_cols - 1) * _spacingX * 0.5f;
        float offsetZ = (_rows - 1) * _spacingZ * 0.5f;

        for (int row = 0; row < _rows; row++)
        {
            for (int col = 0; col < _cols; col++)
            {
                Vector3 localPos = new Vector3(
                    col * _spacingX - offsetX,
                    0f,
                    row * _spacingZ - offsetZ
                );

#if UNITY_EDITOR
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(_boxPrefab, transform);
                instance.transform.localPosition = localPos;
                instance.name = $"PowerUpBox_{id}";

                var so = new SerializedObject(instance.GetComponent<PowerUpBox>());
                so.FindProperty("_boxId").intValue = id;
                so.ApplyModifiedProperties();
#else
                GameObject instance = Instantiate(_boxPrefab, transform);
                instance.transform.localPosition = localPos;
#endif
                id++;
            }
        }

#if UNITY_EDITOR
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
    }

    private int GetNextAvailableId()
    {
#if UNITY_EDITOR
        int max = -1;
        PowerUpBox[] all = FindObjectsOfType<PowerUpBox>(true);
        foreach (PowerUpBox box in all)
        {
            if (box.transform.IsChildOf(transform))
                continue;

            var so = new SerializedObject(box);
            int bid = so.FindProperty("_boxId").intValue;
            if (bid > max)
                max = bid;
        }
        return max + 1;
#else
        return 0;
#endif
    }

    [ContextMenu("Clear Boxes")]
    public void ClearBoxes()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
#if UNITY_EDITOR
            DestroyImmediate(transform.GetChild(i).gameObject);
#else
            Destroy(transform.GetChild(i).gameObject);
#endif
        }
    }
}
