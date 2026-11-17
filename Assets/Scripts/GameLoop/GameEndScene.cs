using TMPro;
using UnityEngine;
using System.Linq;
using multiplayer;
using Player;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace GameLoop
{
    public class GameEndScene : MonoBehaviour
    {
        public GameObject gameFirstText;
        public GameObject gameSecondText;
        public GameObject gameThirdText;
        
        public GameObject secondPlacePodium;
        public GameObject thirdPlacePodium;
        
        public GameObject firstPlaceWorm;
        public GameObject secondPlaceWorm;
        public GameObject thirdPlaceWorm;
    
        void Start()
        {
            ShowWinner();
        }

        private void ShowWinner()
        {
            var players = PlayerRegister.Players.Values.OrderByDescending(p => p.score).ToList();
            
            if (players.Count >= 1)
            {
                gameFirstText.GetComponent<TextMeshProUGUI>().text = players[0].name;
                foreach (Player.Player player in FindObjectsByType<Player.Player>(FindObjectsSortMode.None)) //TODO: find a better way of doing this
                {
                    if (player.owner == players[0].playerID)
                    {
                        Color color = player.gameObject.GetComponent<WormRenderer>().wormMaterial.color;
                        firstPlaceWorm.GetComponent<Image>().color = color;
                    }
                }
            }

            if (players.Count >= 2)
            {
                gameSecondText.GetComponent<TextMeshProUGUI>().text = players[1].name;

                secondPlacePodium.SetActive(true);
                
                foreach (Player.Player player in FindObjectsByType<Player.Player>(FindObjectsSortMode.None)) //TODO: find a better way of doing this
                {
                    if (player.owner == players[1].playerID)
                    {
                        Color color = player.gameObject.GetComponent<WormRenderer>().wormMaterial.color;
                        secondPlaceWorm.GetComponent<Image>().color = color;
                    }
                }
            }
            else
            {
                gameSecondText.GetComponent<TextMeshProUGUI>().text = "";
                secondPlacePodium.SetActive(false);
            }
            
            if (players.Count >= 3)
            {
                gameThirdText.GetComponent<TextMeshProUGUI>().text = players[2].name;

                thirdPlacePodium.SetActive(true);
                
                foreach (Player.Player player in FindObjectsByType<Player.Player>(FindObjectsSortMode.None)) //TODO: find a better way of doing this
                {
                    if (player.owner == players[2].playerID)
                    {
                        Color color = player.gameObject.GetComponent<WormRenderer>().wormMaterial.color;
                        thirdPlaceWorm.GetComponent<Image>().color = color;
                    }
                }
            }
            else
            {
                gameThirdText.GetComponent<TextMeshProUGUI>().text = "";
                thirdPlacePodium.SetActive(false);
            }
        }
    }
}
