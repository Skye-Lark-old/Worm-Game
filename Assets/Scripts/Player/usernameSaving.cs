using System.Collections;
using multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

namespace Player
{
    public class UsernameSaving : MonoBehaviour
    {
        public static string username;
        public TMP_InputField usernameInputField;
        
        private static UsernameSaving Instance { get; set; }
        
        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }

            LocalPlayer.OnLocalPlayerReady += OnLocalPlayerReady;
        }
        
        public void SaveUsername()
        {
            if (usernameInputField != null)
            {
                username = usernameInputField.text;
            }
        }

        private void OnLocalPlayerReady()
        {
            if (SceneManager.GetActiveScene().name == "GameLobbyScene")
            {
                OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
                return;
            }
            
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "GameLobbyScene")
            {
                Debug.Log("switched to game lobby in username saving, username: " + username);
                LocalPlayer.Instance.PlayerName = username;
                LocalPlayer.Instance.playerID = Network.instance.manager.localPlayer;

                StartCoroutine(WaitToDestroy());
            }
        }

        private IEnumerator WaitToDestroy()
        {
            yield return new WaitForSeconds(0.5f);
            Destroy(gameObject);
        }
        
        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            LocalPlayer.OnLocalPlayerReady -= OnLocalPlayerReady;
        }
    }
}