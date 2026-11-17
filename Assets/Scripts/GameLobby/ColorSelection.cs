using System.Collections.Generic;
using Player;
using PurrNet;
using UnityEngine;
using UnityEngine.UI;

namespace GameLobby
{
    public class ColorSelection : MonoBehaviour
    {
        #region public variables
        
        public GameObject colorSelectionPanel;
        public GameObject buttonPrefab;
        public Image selectColorButtonColor;
        public Material wormMaterial;
        public List<ColorPair> availableColors = new List<ColorPair>();
        
        #endregion
        
        #region private variables

        private List<Button> colorButtons = new List<Button>();
        private Dictionary<int,PlayerID> takenIndices = new Dictionary<int,PlayerID>();
        
        #endregion
        
        #region built in methods

        void Start() => CreateColorButtons();
        
        #endregion
        
        #region public methods

        public void RefreshTakenColors(Dictionary<int,PlayerID> taken)
        {
            takenIndices = taken;
            UpdateColorButtons();
        }

        public void SetInitialColor()
        {
            
            // Try to claim the color matching the worm's current material
            for (int i = 0; i < availableColors.Count; i++)
            {
                if(takenIndices.ContainsKey(i)&&takenIndices[i]==LocalPlayer.Instance.playerID)return;
                
                if (availableColors[i].bodyMaterial == wormMaterial && !takenIndices.ContainsKey(i))
                {
                    SetColor(i);
                    return;
                }
            }

            // Fall back to first available
            for (int i = 0; i < availableColors.Count; i++)
            {
                if (!takenIndices.ContainsKey(i))
                {
                    SetColor(i);
                    return;
                }
            }

            Debug.LogWarning("SetInitialColor: no available colors.");
        }

        public void SetColor(int colorIndex)
        {
            if (takenIndices.ContainsKey(colorIndex))
            {
                Debug.LogWarning("Color already taken.");
                return;
            }
            
            selectColorButtonColor.color = availableColors[colorIndex].headMaterial.color;

            PlayerRegister.PlayerData player = PlayerRegister.Players[Network.instance.manager.localPlayer];
            player.colorIndex = colorIndex;
            Network.instance.manager.SendToServer(player);
            GetComponent<GameLobby>().CloseColorSelectionPanel();
        }
        
        public void UpdateColorButtons()
        {
            for (int i = 0; i < colorButtons.Count; i++)
            {
                ColorPair pair = availableColors[i];
                bool available = !takenIndices.ContainsKey(i);
                float alpha = available ? 1f : 0.3f;

                colorButtons[i].interactable = available;
                
                if (pair.bodyMaterial == null || pair.headMaterial == null)
                {
                    Debug.LogWarning($"Material at index {i} is null or destroyed!");
                    continue;
                }

                Color body = pair.bodyMaterial.color;
                body.a = alpha;
                colorButtons[i].image.color = body;

                if (colorButtons[i].transform.childCount > 0)
                {
                    Image headImg = colorButtons[i].transform.GetChild(0).GetComponent<Image>();
                    if (headImg != null)
                    {
                        Color head = pair.headMaterial.color;
                        head.a = alpha;
                        headImg.color = head;
                    }
                }
            }
        }
        
        #endregion
        
        #region private methods

        private void CreateColorButtons()
        {
            foreach (Transform child in colorSelectionPanel.transform)
                Destroy(child.gameObject);
            colorButtons.Clear();

            for (int i = 0; i < availableColors.Count; i++)
            {
                int index = i;
                GameObject obj = Instantiate(buttonPrefab, colorSelectionPanel.transform);
                Button button = obj.GetComponent<Button>();
                button.onClick.AddListener(() => SetColor(index));
                colorButtons.Add(button);
            }

            UpdateColorButtons();
        }
        
        #endregion
    }
}