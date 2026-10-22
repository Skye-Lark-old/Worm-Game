using System;
using System.Collections;
using System.Collections.Generic;
using GameLoop;
using Player;
using UnityEngine;
using PurrNet;
using PurrNet.Modules;
using PurrNet.Packing;
using PurrNet.Transports;
using TMPro;
using Unity.VisualScripting;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

public class Network : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public UDPTransport udpTransport;
    public PurrTransport purrTransport;
    public NetworkManager manager;
    
    public static Network instance;
    public static SimplePing pinger;

    public static string targetAddress;
    
    bool Init = false;

    private GameObject networkObject;
    private GameObject predictionObject;

    private void OnDestroy()
    {
        {
            PlayerRegister.ResetRegister();
            manager?.StopClient();
            if(manager?.isServer == true) manager.StopServer();
            Destroy(manager?.gameObject);
        }
    }

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

    void StartCommon()
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
                
                if(targetAddress==""||targetAddress==null)targetAddress = "127.0.0.1";
                instance.udpTransport.address = targetAddress;
                udpTransport.serverPort = 5000;
            }
            else if (manager.transport as PurrTransport != null)
            {
                purrTransport = manager.transport as PurrTransport;
                
                if(targetAddress==""||targetAddress==null)targetAddress = "BattleWorms20";
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
        print("Connecting to: "+Network.targetAddress);
        
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
    
    public bool AllClientsReady()
    {
        if (!SceneManager.GetActiveScene().isLoaded) return false;
        manager.sceneModule.TryGetSceneID(SceneManager.GetActiveScene(), out SceneID scene);
        foreach (PlayerID player in manager.players)
        {
            if(!manager.scenePlayersModule.IsPlayerLoadedInScene(player,scene))return false;
        }
        print("All Client's Synced");
        return true;
    }
}

public class SimplePing : PurrMonoBehaviour
{
    private int Responces = 0;

    struct ping : IPackedAuto
    {
        public bool isResponse;
    }
    public IEnumerator Ping()
    {
        Responces = 0;
        Network.instance.manager.SendToAll<ping>(new ping(), Channel.ReliableOrdered);
        print("Ping");
        yield return new WaitUntil(() => Responces == Network.instance.manager.playerCount);
    }

    private void Pong(PlayerID id, ping p, bool asserver)
    {
        if (!p.isResponse)
        {
            p.isResponse = true;
            manager.SendToServer<ping>(p, Channel.ReliableOrdered);
            print("Pong");
        }
        else
        {
            if (Network.instance.manager.isServer || Network.instance.manager.isHost)
            {
                Responces++;
            }
            else
            {
                Debug.LogWarning("A Client has received a ping response. This should not happen!");
            }
        }
    }

    public override void Subscribe(NetworkManager manager, bool asServer)
    {
        manager.Subscribe<ping>(Pong, asServer);
    }

    public override void Unsubscribe(NetworkManager manager, bool asServer)
    {
        manager.Unsubscribe<ping>(Pong, asServer);
    }
}
