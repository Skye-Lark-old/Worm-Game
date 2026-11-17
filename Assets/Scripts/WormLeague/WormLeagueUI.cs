using System;
using DG.Tweening;
using multiplayer;
using PurrNet;
using PurrNet.Packing;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WormLeague
{
    public class WormLeagueUI : PurrMonoBehaviour
    {
        public WormLeague wormLeague;
        
        public TextMeshProUGUI titleText;
        public TextMeshProUGUI teamText;
        public TextMeshProUGUI redScore;
        public TextMeshProUGUI blueScore;

        private string team;
        
        private void Start()
        {
            DisplayTitleScreen();

            if (PlayerRegister.Instance != null)
            {
                PlayerRegister.OnPlayerRegisterChanged.AddListener(UpdateUI);
            }
            UpdateUI(new PlayerID(), false);
        }

        private void UpdateUI(PlayerID player, bool _)
        {
            if (Network.instance == null) return;
            if (Network.instance.manager == null) return;
            if (Network.instance.manager.localPlayer == null) return;
            
            PlayerID thisPlayer = Network.instance.manager.localPlayer;
            
            if (!PlayerRegister.Players.ContainsKey(thisPlayer)) return;
            if(PlayerRegister.Players[thisPlayer].team == 0 || !String.IsNullOrEmpty(team)) return;
            
            if(PlayerRegister.Players[thisPlayer].team==PlayerRegister.Team.Red)SetTeam("red");
            else if(PlayerRegister.Players[thisPlayer].team==PlayerRegister.Team.Blue)SetTeam("blue");
            DisplayTitleScreen();
            
            
        }

        public void SetTeam(string inputTeam)
        {
            team = inputTeam;
        }

        public void GoalScored(string goalTeam, string playerName)
        {
            if (goalTeam == "blue")
            {
                titleText.color = Color.blue;
                wormLeague.teamBlueScore++;
                blueScore.text = "" + wormLeague.teamBlueScore;
            }
            else if (goalTeam == "red")
            {
                titleText.color = Color.red;
                wormLeague.teamRedScore++;
                redScore.text = "" + wormLeague.teamRedScore;
            }
            
            titleText.text = playerName + " Scored!";
            titleText.alpha = 1f;
            titleText.DOFade(0f, GameParameters.ScoreFadeTime).SetDelay(GameParameters.ScoreShowTime);
        }
        
        private void DisplayTeam()
        {
            if (team == "blue")
            {
                titleText.text = "Team Blue";
                titleText.color = Color.blue;
                teamText.text = "You Are Blue";
                teamText.color = Color.blue;
            }
            else if (team == "red")
            {
                titleText.text = "Team Red";
                titleText.color = Color.red;
                teamText.text = "You Are Red";
                teamText.color = Color.red;
            }
            else
            {
                return;
            }

            titleText.alpha = 1f;
            titleText.DOFade(0f, GameParameters.TeamFadeTime).SetDelay(GameParameters.TeamShowTime);
        }

        private void DisplayTitleScreen()
        {
            titleText.text = "Worm League";
            
            titleText.alpha = 1f;
            
            titleText.DOFade(0f, GameParameters.TitleFadeTime).SetDelay(GameParameters.TitleShowTime).OnComplete(DisplayTeam);
        }

        public override void Subscribe(NetworkManager manager, bool asServer)
        {
            manager.Subscribe<GoalScoredPacket>(OnGoalScoredPacket, asServer);

        }

        public override void Unsubscribe(NetworkManager manager, bool asServer)
        {
            manager.Unsubscribe<GoalScoredPacket>(OnGoalScoredPacket, asServer);
        }

        private void OnGoalScoredPacket(PlayerID player, GoalScoredPacket data, bool asServer)
        {
            GoalScored(data.goalName,data.playerName);
        }
        
        public struct GoalScoredPacket : IPackedAuto
        {
            public string playerName;
            public string goalName;
        }
    }
}
