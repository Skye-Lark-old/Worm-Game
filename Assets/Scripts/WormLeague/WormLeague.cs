using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;
using GameLoop;
using Player;
using PurrNet;
using Unity.VisualScripting;
using UnityEngine.InputSystem;

namespace WormLeague
{
    public class WormLeague : NetworkBehaviour
    {
        #region Public Variables
        
        public WormLeagueUI wormLeagueUI;
        
        public Ball ball;

        public int teamRedScore = 0;
        public int teamBlueScore = 0;

        public List<GameObject> spawnPoints;
        public List<GameObject> redSpawnPoints;
        public List<GameObject> blueSpawnPoints;
        
        #endregion
        
        #region Private Variables
        
        private List<PlayerID> teamBlue = new();
        private List<PlayerID> teamRed = new();
        
        #endregion
        
        #region Built-In Methods
    
        void Start()
        {
            //StartCoroutine(Ddebug());
            AssignPlayerTeams();
            AssignPlayerSpawnPoints();

            if (LoadingScreenManager.instance != null)
            {
                LoadingScreenManager.LoadingScreenForSelf(false);
            }
        }

        private void Update()
        {
            if(Input.GetKey(KeyCode.B))ball.transform.position = new Vector3(0f, 1f, 3f);
        }

        public void OnDestroy()
        {
            GameOver();
        }

        #endregion
        
        #region Public Methods

        public void OnGoalScored(string team)
        {
            if (!isServer) return;
            if (ball == null || ball.LastTouchingPlayer == null) return;

            var manager = NetworkManager.main;
            if (manager == null)
            {
                Debug.LogWarning("Goal: NetworkManager.main is null");
                return;
            }

            var scorer = ball.LastTouchingPlayer;

            if (!PlayerRegister.Players.TryGetValue(scorer.playerID, out PlayerRegister.PlayerData scoringPlayer))
            {
                Debug.LogWarning($"Goal: scorer {scorer.playerID} not in PlayerRegister. Keys: {string.Join(", ", PlayerRegister.Players.Keys)}");
                return;
            }

            if (team == "blue" || team == "red")
            {
                WormLeagueUI.GoalScoredPacket packet;
                packet.playerName = scoringPlayer.name;
                packet.goalName = team == "blue" ? "red" : "blue";
                manager.SendToAll(packet);
            }

            ball.Reset();

            if (string.IsNullOrEmpty(scoringPlayer.name))
                scoringPlayer.name = $"Player {scorer.playerID}";

            scoringPlayer.playerID = scorer.playerID;
            scoringPlayer.score += 1;

            PlayerRegister.Players[scorer.playerID] = scoringPlayer;
            manager.SendToAll(scoringPlayer);
        }
        public void GameOver()
        {
            if (teamRedScore > teamBlueScore)
            {
                foreach (PlayerID player in teamRed)
                {
                    PlayerRegister.PlayerData playerData = PlayerRegister.Players[player];
                    playerData.score += 10;
                    PlayerRegister.Players[playerData.playerID] = playerData;
                    Network.instance.manager.SendToAll(playerData);
                    
                    
                }
            }
            else if (teamRedScore < teamBlueScore)
            {
                foreach (PlayerID player in teamBlue)
                {
                    PlayerRegister.PlayerData playerData = PlayerRegister.Players[player];
                    playerData.score += 10;
                    PlayerRegister.Players[playerData.playerID] = playerData;
                    Network.instance.manager.SendToAll(playerData);
                    
                }
            }
            else
            {
                foreach (PlayerID player in teamBlue)
                {
                    if(!PlayerRegister.Players.TryGetValue(player, out var playerData))continue;
                    
                    playerData.score += 5;
                    PlayerRegister.Players[playerData.playerID] = playerData;
                    Network.instance.manager.SendToAll(playerData);
                    
                }
                foreach (PlayerID player in teamRed)
                {
                    if(!PlayerRegister.Players.TryGetValue(player, out var playerData))continue;
                    
                    playerData.score += 5;
                    PlayerRegister.Players[playerData.playerID] = playerData;
                    Network.instance.manager.SendToAll(playerData);
                    
                }
            }
            PlayerRegister.ClearPlayerTeams();
        }
        
        #endregion
        
        #region Private Methods

        private static int pointer;

        private static int[] riggedList = new[]
        {
            1, 2, 1, 2,
            1, 1, 2, 2,
            2, 2, 1, 1
        };
        private void RigPlayerTeams()
        {
            
            foreach (PlayerID player in  PlayerRegister.Players.Keys)
            {
                PlayerRegister.PlayerData playerData = PlayerRegister.Players[player];
                playerData.team = (PlayerRegister.Team)riggedList[pointer++];
                if(playerData.team == PlayerRegister.Team.Red)teamRed.Add(playerData.playerID);
                if(playerData.team == PlayerRegister.Team.Blue)teamBlue.Add(playerData.playerID);
                
                if (pointer >= riggedList.Length) pointer = 0;
                Network.instance.manager.SendToServer<PlayerRegister.PlayerData>(playerData);
            }
        }
    
        private void AssignPlayerTeams()
        {
            if(!isServer||!isHost)return;
            RigPlayerTeams();
            return;
            
            List<Player.Player> playerObjects = new List<Player.Player>(FindObjectsByType<Player.Player>(FindObjectsSortMode.None));

            //currently this always assigns the first random player to team red, so worm is always red with 1 player
            List<PlayerID> players = new List<PlayerID>(PlayerRegister.Players.Keys.AsReadOnlyList());
            while (players.Count > 0)
            {
                int random =  Random.Range(0, players.Count);
                teamRed.Add(players[random]);

                {
                    PlayerRegister.PlayerData playerData = PlayerRegister.Players[players[random]];
                    playerData.team = PlayerRegister.Team.Red;
                    //PlayerRegister.Players[players[random]] = playerData;
                    Network.instance.manager.SendToServer<PlayerRegister.PlayerData>(playerData);
                    
                    
                    print(playerData.name +" is on "+playerData.team);
                }

                players.RemoveAt(random);
                if (players.Count > 0)
                {
                    random =  Random.Range(0, players.Count);
                    teamBlue.Add(players[random]);
                    
                    {
                        PlayerRegister.PlayerData playerData = PlayerRegister.Players[players[random]];
                        playerData.team = PlayerRegister.Team.Blue;
                        //PlayerRegister.Players[players[random]] = playerData;
                        Network.instance.manager.SendToServer<PlayerRegister.PlayerData>(playerData);
                        print(playerData.name +" is on "+playerData.team);
                    }
                    
                    players.RemoveAt(random);
                }
            }
            
        }

        private IEnumerator Ddebug()
        {
            while (true)
            {
                yield return new WaitForSeconds(1);
                PlayerRegister.PlayerData playerData = new PlayerRegister.PlayerData();
                playerData.name = "DEBUG PLS WORK";
                Network.instance.manager.SendToServer<PlayerRegister.PlayerData>(playerData);
                
            }
        }
        
        private void AssignPlayerSpawnPoints()
        {
            //TODO: improve logic because the double for loop feels gross
            if (!isServer) return;
            
            List<Player.Player> playerObjects = new List<Player.Player>(FindObjectsByType<Player.Player>(FindObjectsSortMode.None));
            Debug.Log("Assigning spawnpoint to player as server");

            List<PlayerID> teamRedCopy = new List<PlayerID>(teamRed);
            Debug.Log($"redcopy length: {teamRedCopy.Count}");
            foreach(GameObject redSpawnPoint in redSpawnPoints)
            {
                if (teamRedCopy.Count == 0) break;
    
                int random = Random.Range(0, teamRedCopy.Count);
                PlayerID randomPlayer = teamRedCopy[random];
                teamRedCopy.RemoveAt(random);
                
                Debug.Log($"assigning {redSpawnPoint} to {randomPlayer}");
                foreach (Player.Player player in playerObjects)
                {
                    if (player.owner == randomPlayer)
                    {
                        player.GetComponent<PlayerSpawning>().SetSpawnPoint(redSpawnPoint);
                    }
                }
            }
            
            List<PlayerID> teamBlueCopy = new List<PlayerID>(teamBlue);
            foreach(GameObject blueSpawnPoint in blueSpawnPoints)
            {
                if (teamBlueCopy.Count == 0) break;
    
                int random = Random.Range(0, teamBlueCopy.Count);
                PlayerID randomPlayer = teamBlueCopy[random];
                teamBlueCopy.RemoveAt(random);
                
                foreach (Player.Player player in playerObjects)
                {
                    if (player.owner == randomPlayer)
                    {
                        player.GetComponent<PlayerSpawning>().SetSpawnPoint(blueSpawnPoint);
                    }
                }
            }
        }
        
        #endregion
    }
}
