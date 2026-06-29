using Photon.Pun;
using UnityEngine;

[DisallowMultipleComponent]
public class MinimapIcon : MonoBehaviour
{
    [SerializeField] private float _size = 6f;
    [SerializeField] private Color _localColor = Color.white;
    [SerializeField] private Color[] _remoteColors = new Color[]
    {
        new Color(1.00f, 0.22f, 0.22f),
        new Color(0.20f, 0.55f, 1.00f),
        new Color(0.20f, 0.90f, 0.25f),
        new Color(1.00f, 0.55f, 0.10f),
        new Color(0.75f, 0.20f, 1.00f),
        new Color(0.10f, 0.90f, 0.90f),
        new Color(1.00f, 0.30f, 0.75f),
    };

    private void Awake()
    {
        transform.localScale = Vector3.one * _size;

        PhotonView pv = GetComponentInParent<PhotonView>();
        bool isLocal  = PhotonViewAuthority.HasLocalInputAuthority(pv);

        Color color;
        if (isLocal)
        {
            color = _localColor;
        }
        else
        {
            int actorNumber = pv != null && pv.Owner != null ? pv.OwnerActorNr : 0;
            color = _remoteColors[actorNumber % _remoteColors.Length];
        }

        Renderer r = GetComponent<Renderer>();
        if (r != null)
            r.material.color = color;
    }
}
