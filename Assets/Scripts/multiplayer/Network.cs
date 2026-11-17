using System.Collections;
using GameLoop;
using PurrNet;
using PurrNet.Packing;
using PurrNet.Transports;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace multiplayer
{
    public class Network : MonoBehaviour
    {
        #region public variables

        public UDPTransport udpTransport;
        public PurrTransport purrTransport;
        public NetworkManager manager;

        public static Network instance;
        public static SimplePing pinger;

        public static string targetAddress;

        #endregion

        #region private variables

        private bool Init = false;

        private GameObject networkObject;
        private GameObject predictionObject;

        #endregion

        #region built in methods

        private void OnDestroy()
        {
            {
                PlayerRegister.ResetRegister();
                manager?.StopClient();
                if (manager?.isServer == true) manager.StopServer();
                Destroy(manager?.gameObject);
            }
        }

        #endregion

        #region public methods

        public void StartServer()
        {
            LoadingScreenManager.LoadingScreenForSelf(true);
            StartCommon();
            manager.StartHost();
        }

        public void StartClient()
        {
            StartCommon();
            manager.StartClient();
        }

        public bool AllClientsReady()
        {
            if (!SceneManager.GetActiveScene().isLoaded) return false;
            manager.sceneModule.TryGetSceneID(SceneManager.GetActiveScene(), out SceneID scene);
            foreach (PlayerID player in manager.players)
            {
                if (!manager.scenePlayersModule.IsPlayerLoadedInScene(player, scene)) return false;
            }

            print("All Client's Synced");
            return true;
        }

        #endregion

        #region private methods

        private void StartCommon()
        {
            if (!Init)
            {
                instance = this;
                Init = true;
                networkObject = Instantiate(Resources.Load<GameObject>("Multiplayer/Network-Config"));
                predictionObject = Instantiate(Resources.Load<GameObject>("Multiplayer/Prediction-Config"));

                pinger = gameObject.GetOrAddComponent<SimplePing>();

                manager = networkObject.GetComponent<NetworkManager>();

                if (manager.transport as UDPTransport != null)
                {
                    udpTransport = manager.transport as UDPTransport;

                    if (targetAddress == "" || targetAddress == null) targetAddress = "127.0.0.1";
                    instance.udpTransport.address = targetAddress;
                    udpTransport.serverPort = 5000;
                }
                else if (manager.transport as PurrTransport != null)
                {
                    purrTransport = manager.transport as PurrTransport;

                    if (targetAddress == "" || targetAddress == null) targetAddress = "BattleWorms20";
                    instance.purrTransport.roomName = targetAddress;
                }
                else
                {
                    Debug.LogError("No transport found");
                    return;
                }

                DontDestroyOnLoad(networkObject);
                DontDestroyOnLoad(predictionObject);
                DontDestroyOnLoad(gameObject);

                manager.onClientConnectionState += OnNetworkChangeScene;
                manager.onServerConnectionState += OnNetworkChangeScene;

                PlayerRegister.InitRegister();

                gameObject.AddComponent<WormGameSceneSwitcher>();
            }

            print("Connecting to: " + Network.targetAddress);

        }

        private void OnNetworkChangeScene(ConnectionState state)
        {
            if (state == ConnectionState.Connected)
            {
                WormGameSceneSwitcher switcher = gameObject.GetOrAddComponent<WormGameSceneSwitcher>();
                StartCoroutine(switcher.LoadGameLobbyScene(0));
            }

            if (state == ConnectionState.Disconnected)
            {
                gameObject.GetOrAddComponent<WormGameSceneSwitcher>().LoadMainMenuScene();
                Destroy(gameObject);
            }
        }

        #endregion
    }
}