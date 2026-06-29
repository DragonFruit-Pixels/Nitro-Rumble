using System.Collections;
using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadingSceneController : MonoBehaviour
{
    [SerializeField] private float _minimumLoadingSeconds = 0.75f;
    [SerializeField] private string _offlineFallbackScene = "Lobby";

    private IEnumerator Start()
    {
        yield return new WaitForSeconds(_minimumLoadingSeconds);

        if (!PhotonNetwork.InRoom)
        {
            SceneManager.LoadScene(_offlineFallbackScene);
            yield break;
        }

        if (!PhotonNetwork.IsMasterClient)
            yield break;

        string nextScene = ChampionshipManager.Instance != null
            ? ChampionshipManager.Instance.GetNextSceneName()
            : null;

        if (string.IsNullOrEmpty(nextScene) && PhotonNetwork.CurrentRoom != null)
        {
            nextScene = PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(Keys.NEXT_SCENE_KEY, out object value)
                ? value as string
                : null;
        }

        if (string.IsNullOrEmpty(nextScene))
            nextScene = _offlineFallbackScene;

        PhotonNetwork.LoadLevel(nextScene);
    }
}
