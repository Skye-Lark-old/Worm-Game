using UnityEngine;

namespace Audio
{
    public class GameLobbyAudio : MonoBehaviour
    {
        public AudioSource audioSource;
        
        void Start()
        {
            GetComponent<GameLobby.GameLobby>().OnGameStart += OnGameStart;
        }

        void OnDestroy()
        {
            GetComponent<GameLobby.GameLobby>().OnGameStart -= OnGameStart;
        }

        private void OnGameStart()
        {
            audioSource.Play();
        }
    }
}
