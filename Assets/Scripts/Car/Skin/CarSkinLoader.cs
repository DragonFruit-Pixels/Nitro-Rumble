using ExitGames.Client.Photon;
using Photon.Pun;
using UnityEngine;

public class CarSkinLoader : MonoBehaviourPun
{
    private const string SKIN_PROP = "SkinID";

    [Header("Car Skin Filter")]
    [SerializeField] private MeshFilter carMeshFilter;

    [Header("Car Skins")]
    [SerializeField] private CarSkinCatalogueSO carSkins;

    private int _currentSkinID = 0;

    private void Start()
    {
        LoadSkin();
    }

    private void OnEnable()
    {
        // LiveOps: si la skin actual queda deshabilitada mientras este objeto existe
        // (poll del dashboard, resume desde background, etc.), cambiar a otra disponible.
        if (LiveOpsConfig.Instance != null)
            LiveOpsConfig.Instance.OnConfigApplied += OnLiveOpsConfigApplied;
    }

    private void OnDisable()
    {
        if (LiveOpsConfig.Instance != null)
            LiveOpsConfig.Instance.OnConfigApplied -= OnLiveOpsConfigApplied;
    }

    private void OnLiveOpsConfigApplied()
    {
        EnsureCurrentSkinAvailable();
    }

    private void LoadSkin()
    {
        if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            if (PhotonViewAuthority.HasLocalInputAuthority(photonView))
            {
                if (LocalSaveManager.Instance)
                    _currentSkinID = LocalSaveManager.Instance.Profile.selectedSkin;

                // Sync to Photon player properties so late-joining clients can read it
                PhotonNetwork.LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable
                {
                    { SKIN_PROP, _currentSkinID }
                });

                photonView.RPC(nameof(RPC_LoadSkin), RpcTarget.AllBuffered, _currentSkinID);

                // La skin guardada pudo haber quedado deshabilitada entre sesiones (LiveOps).
                EnsureCurrentSkinAvailable();
            }
            // Non-owner: wait for the buffered RPC.
        }
        else
        {
            if (LocalSaveManager.Instance)
                _currentSkinID = LocalSaveManager.Instance.Profile.selectedSkin;

            RPC_LoadSkin(_currentSkinID);

            EnsureCurrentSkinAvailable();
        }
    }

    public void ChangeCurrentSkin(CarSkinSO newSkin)
    {
        if (!carSkins.HasSkin(newSkin.skinID)) return;

        _currentSkinID = newSkin.skinID;
        LocalSaveManager.Instance?.SaveSkin(_currentSkinID);

        if (PhotonNetwork.IsConnected)
            PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { SKIN_PROP, _currentSkinID } });

        if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
            photonView.RPC(nameof(RPC_LoadSkin), RpcTarget.AllBuffered, _currentSkinID);
        else
            RPC_LoadSkin(_currentSkinID);
    }

    /// <summary>
    /// Si la skin actual quedó deshabilitada por LiveOps, cambia a la primera skin
    /// disponible en el catálogo (que actúa como default). Solo corre para el dueño
    /// del objeto (o en modo offline): nunca debe disparar para autos de otros jugadores.
    /// </summary>
    private void EnsureCurrentSkinAvailable()
    {
        if (LiveOpsConfig.Instance == null || carSkins == null) return;
        if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom && !PhotonViewAuthority.HasLocalInputAuthority(photonView)) return;

        if (LiveOpsConfig.Instance.IsSkinAvailable(_currentSkinID)) return;

        foreach (CarSkinSO skin in carSkins.Skins)
        {
            if (skin == null) continue;
            if (!LiveOpsConfig.Instance.IsSkinAvailable(skin.skinID)) continue;

            Logger.Log($"[LiveOps] Skin {_currentSkinID} deshabilitada; cambiando a {skin.skinID} ({skin.SkinName}).");
            ChangeCurrentSkin(skin); // guarda en LSM, sincroniza props y RPC
            return;
        }

        // Ningún skin del catálogo está disponible (caso extremo de config mal hecha).
        // Fail-open: se deja la skin actual puesta para no romper el auto visualmente.
        Logger.LogWarning("[LiveOps] Ninguna skin disponible en el catálogo; se mantiene la actual.");
    }

    [PunRPC]
    private void RPC_LoadSkin(int skinID)
    {
        _currentSkinID = skinID;

        if (!carSkins.HasSkin(skinID)) return;

        CarSkinSO carSkin = carSkins.GetSkin(skinID);
        ChangeMesh(carSkin.SkinMesh);
    }

    private void ChangeMesh(Mesh newMesh)
    {
        if (carMeshFilter == null) return;
        carMeshFilter.mesh = newMesh;
    }
}
