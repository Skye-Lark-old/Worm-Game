using System;
using System.Collections.Generic;
using GameLobby;
using PurrNet;
using PurrNet.Packing;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;
using Random = UnityEngine.Random;

namespace multiplayer
{
    public class PlayerRegister : PurrMonoBehaviour
    {
        
        #region public variables
        
        public static readonly Dictionary<PlayerID, PlayerData> Players = new();
        public static readonly UnityEvent<PlayerID, bool> OnPlayerRegisterChanged = new();
        public static readonly UnityEvent OnPlayerRegistered = new();
        public static NetworkManager Manager { get; private set; }

        public static PlayerRegister Instance;
        
        #endregion
        
        public enum Team : byte
        {
            None = 0,
            Red = 1,
            Blue = 2
        }
    
        public struct PlayerData : IPackedAuto
        {
            public string name;
            public PlayerID playerID;
            public int colorIndex;
            public ushort score;
            public Team team;
            public bool isDisconected;
        }
        
        #region built in methods

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);   
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        
        public override void Subscribe(NetworkManager manager, bool asServer)
        {
            Manager = manager;
            manager.Subscribe<PlayerData>(OnPlayerDataRequest, asServer);
        }
        
        public override void Unsubscribe(NetworkManager manager, bool asServer)
        {
            manager.Unsubscribe<PlayerData>(OnPlayerDataRequest, asServer);
        }
    
        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                Players.Clear();       
            }
        }
        
        #endregion
        
        #region public methods
        
        public static void RemoveClient(PlayerID playerID, bool _)
        {
            Debug.Log("Delisting client: "+playerID.id.value);
            PlayerData playerData = Players[playerID];
            playerData.isDisconected = true;
            Players[playerID] = playerData;
        
            OnPlayerRegisterChanged.Invoke(playerID,false);
        }
    
        public static void RegisterClient(PlayerID playerID, bool firstJoin, bool _)
        {
            Debug.Log("Registering client: "+playerID.id.value);
            if(!Players.ContainsKey(playerID)) Players[playerID] = new PlayerData { colorIndex = -1, playerID = playerID };
            PlayerData playerData = Players[playerID];
            playerData.isDisconected = false;
            Players[playerID] = playerData;
    
            OnPlayerRegistered?.Invoke();
        }
        
        public static void ResetRegister()
        {
            if (Network.instance?.manager == null)return;
        
            Network.instance.manager.onPlayerJoined -= RegisterClient;
            Network.instance.manager.onPlayerLeft -= RemoveClient;
            Players.Clear();
        }

        public static void InitRegister()
        {
            if (Network.instance.manager == null)
            {
                Debug.LogError("Network manager is null! Can not initialize player register.");
                return;
            }
            Network.instance.manager.onPlayerJoined += RegisterClient;
            Network.instance.manager.onPlayerLeft += RemoveClient;
        }
    
        public static void ClearPlayerTeams()
        {
            List<PlayerID> keys = new List<PlayerID>(Players.Keys);
            foreach (PlayerID key in keys)
            {
                PlayerData playerData = Players[key];
                playerData.team = Team.None;
                Players[key] = playerData;
            }

            if (Network.instance?.manager != null)
            {
                foreach (PlayerData playerData in Players.Values)
                {
                    Network.instance.manager.SendToAll(playerData);
                }
            }
        }
        
        public static Player.Player FindPlayerByID(PlayerID id)
        {
            foreach (Player.Player p in FindObjectsByType<Player.Player>(FindObjectsSortMode.None))
            {
                if (p.owner == id)
                    return p;
            }
            return null;
        }
        
        #endregion
        
        #region private methods

        private string FixUserName(string newName)
        {
            List<string> takenNames = new List<string>();
            foreach (PlayerData player in Players.Values)
            {
                takenNames.Add(player.name);
            }
        
            newName ??= "Player";
            newName = System.Text.RegularExpressions.Regex.Replace(newName, "[^A-Za-z0-9_-]", "");
            if (newName.Length < 2 || newName.Length > 16) newName = "Player";
            while (takenNames.Contains(newName))
            {
                newName += (int)(Random.value*10);
                if (newName.Length < 2 || newName.Length > 16) newName = "Player";
            }
            return newName;
        }
        
        private void OnPlayerDataRequest(PlayerID playerID, PlayerData player, bool asServer)
        {
            if (String.IsNullOrEmpty(player.name))
            {
                Debug.LogWarning("Player packet recived with no name!");
            }
            if (player.score != 0)
            {
                Debug.LogWarning("Score found! "+player.score+" "+player.name);
            }
            if (asServer)
            {
                bool isUpdate = Players.ContainsKey(player.playerID) && !string.IsNullOrEmpty(Players[player.playerID].name);

                if (!isUpdate)
                {
                    RejoinLogic(player.playerID, player.name);
                    player.name = FixUserName(player.name);
                }
                
                RegisterPlayerData(player, player.playerID);
                
                foreach (var existingPlayer in Players)
                {
                    Network.instance.manager.SendToAll(existingPlayer.Value);
                }
            }
            else
            {
                RegisterPlayerData(player, player.playerID);
            }
        }

        private void RegisterPlayerData(PlayerData player, PlayerID playerID)
        {
            Players[player.playerID] = player;
            ColorUpdate(player);
            OnPlayerRegisterChanged.Invoke(playerID, true);
        }
        
        private static void RejoinLogic(PlayerID playerID, string newName)
        {
            List<KeyValuePair<PlayerID, PlayerData>> playerValues = Players.ToListPooled();
            for(int i = 0; i < playerValues.Count; i++)
            {
            
                if(!playerValues[i].Value.isDisconected) continue;
                if (playerValues[i].Value.name == newName)
                {
                    Players[playerID] = playerValues[i].Value;
                    Players.Remove(playerValues[i].Value.playerID);
                }
            }
        }
        
        private void ColorUpdate(PlayerData data)
        {
            
            Player.Player targetPlayer = FindPlayerByID(data.playerID);
            if (targetPlayer == null)
            {
                Debug.LogWarning($"OnColorUpdate: could not find Player object for {data.playerID}");
                return;
            }

            ColorSelection colorSelection = FindFirstObjectByType<ColorSelection>();
            if (colorSelection == null || data.colorIndex < 0 || data.colorIndex >= colorSelection.availableColors.Count)
                return;

            targetPlayer.SetColor(colorSelection.availableColors[data.colorIndex].bodyMaterial, colorSelection.availableColors[data.colorIndex].headMaterial, colorSelection.availableColors[data.colorIndex].deadMaterial);
            
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
    }
}
