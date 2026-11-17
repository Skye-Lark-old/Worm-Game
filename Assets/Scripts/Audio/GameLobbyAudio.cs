using UnityEngine;

namespace Audio
{
    public class GameLobbyAudio : MonoBehaviour
    {
        public AudioSource audioSource;
        
        void Start()
        {
            GetComponent<GameLobby>().OnGameStart += OnGameStart;
        }

        void OnDestroy()
        {
            GetComponent<GameLobby>().OnGameStart -= OnGameStart;
        }

        private void OnGameStart()
        {
            audioSource.Play();
        }
    }
}
