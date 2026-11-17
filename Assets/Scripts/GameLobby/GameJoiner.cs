using multiplayer;
using TMPro;
using UnityEngine;

namespace GameLobby
{
    public class GameJoiner : MonoBehaviour
    {
        public TMP_InputField addressbox;
        public void Join()
        {
            Network.targetAddress=addressbox.text;
            LoadingScreenManager.LoadingScreenForSelf(true);
            GameObject Client = new GameObject();
            Client.AddComponent<Network>().StartClient();
            DontDestroyOnLoad(Client);
        }
    
        public void Host()
        {
            Network.targetAddress=addressbox.text;
            LoadingScreenManager.LoadingScreenForSelf(true);
            GameObject Host = new GameObject();
            Host.AddComponent<Network>().StartServer();
            DontDestroyOnLoad(Host);
        }
    }
}
