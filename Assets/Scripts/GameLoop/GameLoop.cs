using System;
using System.Collections;
using System.Collections.Generic;
using multiplayer;
using Player;
using PurrNet;
using PurrNet.Modules;
using PurrNet.Packing;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace GameLoop
{
    public class GameLoop : MonoBehaviour
    {
        #region Public variables

        [Header("Public Variables")] public static GameLoop Instance;
        public bool IsGameLoopRunning { get; private set; }
        public List<Player.Player> players;
        public Dictionary<Player.Player, Player.Player> networkPlayersDictionary;
        public List<GameObject> partCards = new List<GameObject>();
        public static List<GameObject> partCardsStatic;

        public static GameLoopTimeSyncer gameLoopTimer;

        #endregion

        #region Modifiable Loop Settings

        [Header("modifiable game loop settings")]

        private int numberOfRounds;
        private int numberOfPartsPerRound;
        public static int timePerPartSelection;
        private int timePerCreatureBuilding;
        private int timePerMinigame;
        private int timeForLeaderboard;
        private bool skipCreatureBuilding1StRound = false;

        private bool EndOfRound = false;

        #endregion

        #region Private Variables

        private Coroutine gameLoop;
        private WormGameSceneSwitcher sceneSwitcher;
        private bool sceneReady = false;
        
        private Dictionary<PlayerID, (Scene unityScene, SceneID sceneID)> playerScenes = new Dictionary<PlayerID, (Scene, SceneID)>();

        #endregion

        #region Built-In Methods

        private void Awake()
        {
            partCardsStatic = partCards;
            gameLoopTimer = new GameObject("Timer").AddComponent<GameLoopTimeSyncer>();

            // Guard against Network.instance not being ready yet
            if (Network.instance == null || Network.instance.manager == null)
            {
                Debug.LogWarning("GameLoop awoke before Network was ready — deferring setup.");
                StartCoroutine(DeferredAwake());
                return;
            }

            InitializeGameLoop();
        }

        private IEnumerator DeferredAwake()
        {
            yield return new WaitUntil(() => Network.instance != null && Network.instance.manager != null);
            InitializeGameLoop();
        }

        private void InitializeGameLoop()
        {
            if (!Network.instance.manager.isServer && !Network.instance.manager.isHost)
            {
                Destroy(gameObject);
                return;
            }

            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(this);
            }
        }

        private void Start()
        {
            sceneSwitcher = gameObject.GetComponent<WormGameSceneSwitcher>();
            sceneSwitcher.OnSceneLoaded += HandleSceneLoaded;
            SetDefaultGameLoopSettings();
        }

        private void OnDestroy()
        {
            // Reserved for future cleanup
        }

        #endregion

        #region Public Methods

        public void Reset()
        {
            if (gameLoop == null) return;

            StopCoroutine(gameLoop);
            StopAllCoroutines();

            gameLoopTimer.TimeLeftInScene = 0;
            IsGameLoopRunning = false;
            sceneReady = false;

            foreach (Player.Player player in players)
            {
                player.ResetPlayer();
            }
        }

        public void StartGame()
        {
            IsGameLoopRunning = true;
            gameLoop = StartCoroutine(RunGameLoop());
        }

        #endregion

        #region Private Methods

        private void HandleSceneLoaded()
        {
            sceneReady = true;
        }

        private void SetDefaultGameLoopSettings()
        {
            numberOfRounds = GameParameters.DefaultNumberOfRounds;
            numberOfPartsPerRound = GameParameters.DefaultNumberOfPartsPerRound;
            timePerPartSelection = GameParameters.DefaultTimePerPartSelection;
            timePerCreatureBuilding = GameParameters.DefaultTimePerCreatureBuilding;
            timePerMinigame = GameParameters.DefaultTimePerMinigame;
            timeForLeaderboard = GameParameters.TimeForLeaderboard;
        }

        #endregion

        #region Game Loop Logic

        private IEnumerator RunGameLoop()
        {
            for (int i = 0; i < numberOfRounds; i++)
            {
                if (!skipCreatureBuilding1StRound || (i > 0))
                {
                    yield return StartCoroutine(RunPartSelectionAndCreatureBuilding());
                    yield return new WaitUntil(() => EndOfRound);
                    EndOfRound = false;
                }
            }

            Network.instance.manager.sceneModule.LoadSceneAsync("GameEndScene");
        }

        private IEnumerator RunPartSelectionAndCreatureBuilding()
        {
            sceneReady = false;
            yield return Network.instance.manager.sceneModule.LoadSceneAsync("PartSelectionScene");
        }

        public IEnumerator StartCreatureBuilding()
        {
            
            gameLoopTimer.TimeLeftInScene = 0;
            Network.instance.manager.sceneModule.LoadSceneAsync("CreatureBuilderScene");
            
            yield return StartCoroutine(gameLoopTimer.Timer(timePerCreatureBuilding));
            
            LoadingScreenManager.LoadingScreenForAll(true);
            CreatureBuilder.CreatureBuilder creatureBuilder = GameObject.Find("CreatureBuilder").GetComponent<CreatureBuilder.CreatureBuilder>();
            
            yield return StartCoroutine(StartMinigame());
            
        }

        private IEnumerator StartMinigame()
        {
            Debug.Log("Loading game scene");
            Network.instance.manager.sceneModule.LoadSceneAsync(GameSceneList.GetRandomGameScene());

            yield return StartCoroutine(gameLoopTimer.Timer(timePerMinigame));

            Network.instance.manager.sceneModule.LoadSceneAsync("LeaderboardScene");
            yield return StartCoroutine(gameLoopTimer.Timer(timeForLeaderboard));
            EndOfRound = true;
        }

        #endregion

        public void StartCreatureBuildingCoroutine()
        {
            StartCoroutine(StartCreatureBuilding());
        }
    }

    public class GameLoopTimeSyncer : PurrMonoBehaviour
    {
        public float TimeLeftInScene;
        private float ServerTime;
        public UnityEvent TimeExpired = new UnityEvent();

        void Start()
        {
            DontDestroyOnLoad(this);
            StartCoroutine(SendTimePacket());
        }

        public struct TimePacket : IPackedAuto
        {
            public float time;
        }

        public IEnumerator SendTimePacket()
        {
            while (true)
            {
                yield return new WaitForSeconds(0.1f);
                if (Network.instance != null)
                {
                    Network.instance.manager.SendToAll<TimePacket>(new TimePacket { time = ServerTime });
                    yield return new WaitUntil(() => ServerTime > 0);
                }
            }
        }

        public IEnumerator Timer(float time)
        {
            if (Network.instance.manager.isServer || Network.instance.manager.isHost)
            {
                ServerTime = time;
                while (ServerTime > 0)
                {
                    ServerTime -= Time.deltaTime;
                    yield return null;
                }

                ServerTime = -1;
            }
        }

        public void Skip()
        {
            ServerTime = 1;
        }

        public override void Subscribe(NetworkManager manager, bool asServer)
        {
            manager.Subscribe<TimePacket>(SyncClock, asServer);
        }

        public override void Unsubscribe(NetworkManager manager, bool asServer)
        {
            manager.Unsubscribe<TimePacket>(SyncClock, asServer);
        }

        void SyncClock(PlayerID playerID, TimePacket timePacket, bool asServer)
        {
            TimeLeftInScene = timePacket.time;
            if(timePacket.time < 0)TimeExpired.Invoke();
        }
    }
}