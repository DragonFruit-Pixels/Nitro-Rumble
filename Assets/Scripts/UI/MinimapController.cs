using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(Camera))]
[DisallowMultipleComponent]
public class MinimapController : MonoBehaviour
{
    [Header("Track Detection")]
    [SerializeField] private string _trackTag = "Track";
    [SerializeField] private LayerMask _trackLayer = ~0;

    [Header("Fit")]
    [SerializeField] private float _paddingFactor = 1.15f;
    [SerializeField] private float _cameraHeight  = 150f;

    [Header("Manual Override (0 = auto)")]
    [SerializeField] private float   _manualSize   = 0f;
    [SerializeField] private Vector3 _manualCenter = Vector3.zero;

    private Camera _cam;

    private void Awake()
    {
        _cam = GetComponent<Camera>();
        ExcludeMinimapLayerFromAllCameras();
    }

    private void Start()
    {
        FitToTrack();
    }

    private void ExcludeMinimapLayerFromAllCameras()
    {
        int layer = LayerMask.NameToLayer("MinimapIcon");
        if (layer < 0) return;

        foreach (Camera cam in Camera.allCameras)
        {
            if (cam != _cam)
                cam.cullingMask &= ~(1 << layer);
        }
    }

    [ContextMenu("Fit to Track")]
    public void FitToTrack()
    {
        if (_cam == null)
            _cam = GetComponent<Camera>();

        Bounds bounds = CalculateTrackBounds();

        Vector3 center = _manualCenter != Vector3.zero ? _manualCenter : bounds.center;
        transform.position  = new Vector3(center.x, bounds.max.y + _cameraHeight, center.z);
        transform.rotation  = Quaternion.Euler(90f, 0f, 0f);

        _cam.orthographic     = true;
        _cam.orthographicSize = _manualSize > 0f
            ? _manualSize
            : Mathf.Max(bounds.extents.x, bounds.extents.z) * _paddingFactor;
    }

    private Bounds CalculateTrackBounds()
    {
        Renderer[] renderers = FindRenderersForBounds();

        if (renderers.Length == 0)
            return new Bounds(Vector3.zero, Vector3.one * 100f);

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            b.Encapsulate(renderers[i].bounds);

        return b;
    }

    private Renderer[] FindRenderersForBounds()
    {
        if (!string.IsNullOrEmpty(_trackTag) && _trackTag != "Untagged")
        {
            GameObject[] tagged = GameObject.FindGameObjectsWithTag(_trackTag);
            if (tagged.Length > 0)
            {
                System.Collections.Generic.List<Renderer> list =
                    new System.Collections.Generic.List<Renderer>();
                foreach (GameObject go in tagged)
                    list.AddRange(go.GetComponentsInChildren<Renderer>(true));
                if (list.Count > 0)
                    return list.ToArray();
            }
        }

        return Object.FindObjectsOfType<Terrain>().Length > 0
            ? GetTerrainRenderers()
            : Object.FindObjectsOfType<Renderer>();
    }

    private Renderer[] GetTerrainRenderers()
    {
        Terrain[] terrains = Object.FindObjectsOfType<Terrain>();
        System.Collections.Generic.List<Renderer> list =
            new System.Collections.Generic.List<Renderer>();
        foreach (Terrain t in terrains)
        {
            Renderer r = t.GetComponent<Renderer>();
            if (r != null) list.Add(r);
        }
        return list.ToArray();
    }
}
