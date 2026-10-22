using System;
using System.Collections.Generic;
using PurrNet;
using PurrNet.Transports;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using System.Linq;
using Audio;
using Player;

namespace GameLoop.GameLobby
{
    public class GameLobby : MonoBehaviour
    {
        #region Public Variables
        
        public Image playerList;

        public GameObject colorSelectionPanel;
        public GameObject startGameButton;

        public GameObject playerUsernameTextPrefab;
        
        public PlayerRegister playerRegister;

        public ColorSelection colorSelection;
        
        public Camera mainCamera;
        public LayerMask groundLayer;
        [HideInInspector] public Vector3 mouseWorldPosition;
        public GameObject cursorSphere;
        public Vector3 groundAnchorPoint = Vector3.zero;

        public GameObject FakeoutTitleScreen;
        public GameObject music;
        public event Action OnGameStart;
        
        #endregion

        #region Built-In Methods
        
        void Start()
        {
            Debug.Log("Game Lobby start method called");
            playerRegister = Network.instance.gameObject.GetOrAddComponent<PlayerRegister>();
            PlayerRegister.OnPlayerRegisterChanged.AddListener(OnPlayerRegisterChanged);
            PlayerRegister.OnPlayerRegistered.AddListener(OnPlayerRegistered);

            if (Player.LocalPlayer.Instance != null)
            {
                RegisterLocalPlayer();
                LoadingScreenManager.LoadingScreenForSelf(false);
            }
            else
            {
                // Wait for the local player to finish spawning and registering
                Player.LocalPlayer.OnLocalPlayerReady += OnLocalPlayerReady;
            }
        }

        void Update()
        {
            if (Input.GetKey(KeyCode.N))
            {
                FakeoutTitleScreen.SetActive(true);
                music.SetActive(false);
            }
            
            // Clamp mouse position to screen bounds before raycasting
            Vector2 clampedMousePos = new Vector2(
                Mathf.Clamp(Input.mousePosition.x, 0, Screen.width),
                Mathf.Clamp(Input.mousePosition.y, 0, Screen.height)
            );

            Ray ray = mainCamera.ScreenPointToRay(clampedMousePos);

            if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, groundLayer))
            {
                mouseWorldPosition = hit.point;
            }
            else
            {
                Plane groundPlane = new Plane(Vector3.up, groundAnchorPoint);
                if (groundPlane.Raycast(ray, out float enter))
                {
                    Vector3 pointOnPlane = ray.GetPoint(enter);

                    if (Physics.Raycast(pointOnPlane + Vector3.up * 100f, Vector3.down, out RaycastHit groundHit, Mathf.Infinity, groundLayer))
                        mouseWorldPosition = groundHit.point;
                    else
                        mouseWorldPosition = pointOnPlane;
                }
            }

            if (cursorSphere != null)
                cursorSphere.transform.position = mouseWorldPosition;
        }

        private void FixedUpdate()
        {
            if (LocalPlayer.Instance != null) LocalPlayer.Instance.SetLobbyMoveTarget(mouseWorldPosition);
        }

        private void OnLocalPlayerReady()
        {
            Player.LocalPlayer.OnLocalPlayerReady -= OnLocalPlayerReady;
            RegisterLocalPlayer();
            LoadingScreenManager.LoadingScreenForSelf(false);
            
        }

        private void RegisterLocalPlayer()
        {
            PlayerRegister.PlayerData name;
            if (PlayerRegister.Players.ContainsKey(Network.instance.manager.localPlayer)) name = PlayerRegister.Players[Network.instance.manager.localPlayer];
            else name = new PlayerRegister.PlayerData();
            name.name = Player.LocalPlayer.Instance.PlayerName;
            name.playerID = Network.instance.manager.localPlayer;
            if (Network.instance != null)
            {
                Network.instance.manager.SendToServer<PlayerRegister.PlayerData>(name);
            }
        }

        private void OnDestroy()
        {
            PlayerRegister.OnPlayerRegisterChanged.RemoveListener(OnPlayerRegisterChanged);
            Player.LocalPlayer.OnLocalPlayerReady -= OnLocalPlayerReady; // safety cleanup
            PlayerRegister.OnPlayerRegistered.RemoveListener(OnPlayerRegistered);
        }
        
        private void OnDisable()
        {
            if (LocalPlayer.Instance != null) LocalPlayer.Instance.ClearLobbyMoveTarget();
        }

        #endregion
        
        #region Multiplayer Events
        
        public void OnPlayerRegistered()
        {
            RefreshColorSelection();
            //colorSelection.SetInitialColor();
        }

        public void OnPlayerRegisterChanged(PlayerID playerID, bool connected)
        {
            Debug.Log("Player Register changed");
            UpdatePlayerList(playerID, connected);

            if (connected && playerID == Network.instance.manager.localPlayer)
                colorSelection.SetInitialColor();

            RefreshColorSelection();
            ToggleStartGameButton();
        }
        
        private void RefreshColorSelection()
        {
            Dictionary<int, PlayerID> taken = new Dictionary<int, PlayerID>();
            foreach (PlayerRegister.PlayerData player in PlayerRegister.Players.Values)
            {
                if (player.colorIndex >= 0 && player.colorIndex < colorSelection.availableColors.Count)
                {
                    taken.Add(player.colorIndex, player.playerID);
                }
            }
            
            colorSelection.RefreshTakenColors(taken);
        }
        
        #endregion

        #region Public Methods

        public void OpenColorSelectionPanel()
        {
            colorSelectionPanel.SetActive(true);
            colorSelection.UpdateColorButtons();
        }

        public void CloseColorSelectionPanel()
        {
            colorSelectionPanel.SetActive(false);
        }

        public void StartGame()
        {
            LoadingScreenManager.LoadingScreenForSelf(true);
            GameLoop.Instance.StartGame();
            OnGameStart?.Invoke();
        }

        public void UpdatePlayerList(PlayerID playerID, bool connected)
        {
            Debug.Log("player list updating");
            foreach (Transform child in playerList.transform)
            {
                 Destroy(child.gameObject);
            }

            foreach (KeyValuePair<PlayerID,PlayerRegister.PlayerData> player  in PlayerRegister.Players)
            {
                if(player.Value.isDisconected)continue;
                Debug.Log("Player register name: " + player.Value.name);

                foreach (Player.Player playerObject in FindObjectsByType<Player.Player>(FindObjectsSortMode.None))
                {
                    playerObject.SetPlayernameFromLobby(player.Value.name, player.Key);
                }
                
                GameObject textObject = Instantiate(playerUsernameTextPrefab, playerList.transform);
                TextMeshProUGUI tmpText = textObject.GetComponent<TextMeshProUGUI>();
                tmpText.text = player.Value.name;
            }
        }
        
        #endregion
        
        private void ToggleStartGameButton()
        {
            bool isHost = Network.instance.manager.isServer;
            startGameButton.SetActive(isHost);
        }
        
    }
}
