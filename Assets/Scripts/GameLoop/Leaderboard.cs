using System.Collections.Generic;
using TMPro;
using UnityEngine;
using System.Linq;
using multiplayer;

namespace GameLoop
{
    public class Leaderboard : MonoBehaviour
    {
        public GameObject leaderboardBackground;
        public GameObject textPrefab;

        private List<Vector2> spawnPositions = new List<Vector2>();

        void Start()
        {
            PopulateLeaderboard();
        }

        private void PopulateLeaderboard()
        {
            var players = PlayerRegister.Players.Values.ToList();

            List<LeaderboardEntryUI> entries = new List<LeaderboardEntryUI>();

            spawnPositions.Clear();

            float spacing = 150f;
            float startY = -250f;
            int index = 0;

            foreach (var player in players)
            {
                GameObject textObject = Instantiate(textPrefab, leaderboardBackground.transform);

                RectTransform rt = textObject.GetComponent<RectTransform>();
                
                rt.anchorMin = new Vector2(0.5f, 1f);
                rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 0.5f);

                Vector2 pos = new Vector2(0, startY - index * spacing);
                rt.anchoredPosition = pos;

                spawnPositions.Add(pos);

                LeaderboardEntryUI entryUI = textObject.GetComponent<LeaderboardEntryUI>();
                entryUI.SetData(player);
                entries.Add(entryUI);

                index++;
            }

            AssignSortedPositions(entries, players);
        }

        private void AssignSortedPositions(List<LeaderboardEntryUI> entries, List<PlayerRegister.PlayerData> players)
        {
            var sortedPlayers = players
                .OrderByDescending(p => p.score)
                .ToList();

            for (int i = 0; i < sortedPlayers.Count; i++)
            {
                var sortedPlayer = sortedPlayers[i];

                var entry = entries.Find(e => e.playerData.name == sortedPlayer.name);

                if (entry != null)
                {
                    entry.SetTargetPosition(spawnPositions[i]);
                }
            }
        }
    }
}