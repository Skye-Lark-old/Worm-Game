using System;
using System.Collections.Generic;
using CreatureBuilder;
using GameLoop.multiplayer;
using Player;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

namespace GameLoop
{
    public class PartSelection : MonoBehaviour
    {
        #region Public Variables
        [Header("Public Variables")] 
        
        public Image card1Slot;
        public Image card2Slot;
        public TextMeshProUGUI card1Name;
        public TextMeshProUGUI card2Name;
        
        public event Action OnCardSelected;
        
        #endregion

        #region Private Variables
        [Header("Private Variables")] 
        
        private List<GameObject> partCards;
        private GameObject card1;
        private GameObject card2;
    
        private GameObject currentCard;
        private GameObject discardedCard;
        
        public PartSelectionManager.SelectableCardsPacket selectableCards;

        public static PartSelection Instance;
        
        #endregion
    
        #region Built-In Methods
        
        void Start()
        {
            Instance = this;
            partCards = GameLoop.partCardsStatic;
            gameObject.GetOrAddComponent<PartSelectionManager>();
            
            GameLoop.gameLoopTimer.TimeExpired.AddListener(EndCardSelection);
        }

        private void Update()
        {
        }

        void OnDestroy()
        {
            if(card1!=null)EndCardSelection();
        }
        
        #endregion
        
        #region Public Methods

        public void SetCardOptions(PurrNet.PlayerID player, PartSelectionManager.SelectableCardsPacket packet, bool asServer)
        {
            selectableCards = packet;
            card1 = partCards[packet.Card1Index];
            card2 = partCards[packet.Card2Index];
            
            card1Slot.sprite = card1.GetComponent<PartCard>().sprite;
            card2Slot.sprite = card2.GetComponent<PartCard>().sprite;
            card1Name.text = card1.GetComponent<PartCard>().cardName;
            card2Name.text = card2.GetComponent<PartCard>().cardName;
            
            print("card1Name: " + card1Name.name + " card2Name: " + card2Name.name);
            LoadingScreenManager.LoadingScreenForSelf(false);
        }

        public void EndCardSelection()
        {
            LoadingScreenManager.LoadingScreenForSelf(true);
            //if no card was selected, auto select card 1
            if (currentCard == null)
            {
                currentCard = card1;
            }
            if(card1==null) return;
            LocalPlayer.Instance.wormPartsInInventory.Add(currentCard);
            
            //TODO: discard card and get discarded card from opponent
            PartSelectionManager.ReturnCard(selectableCards, currentCard==card1);
            print("end");
            ResetPartSelection();
        }

        public void SelectCard1()
        {
            card1Slot.transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);
            card2Slot.transform.localScale = new Vector3(1f, 1f, 1f);
            currentCard = card1;
            discardedCard = card2;
            OnCardSelected?.Invoke();
        }

        public void SelectCard2()
        {
            card2Slot.transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);
            card1Slot.transform.localScale = new Vector3(1f, 1f, 1f);
            currentCard = card2;
            discardedCard = card1;
            OnCardSelected?.Invoke();
        }
        
        #endregion
        
        #region Private Methods

        private void ResetPartSelection()
        {
            card1 = null;
            card2 = null;
            currentCard = null;
            discardedCard = null;
            card1Slot.sprite = null;
            card2Slot.sprite = null;
            card1Name.text = "";
            card2Name.text = "";
            card1Slot.transform.localScale = new Vector3(1f, 1f, 1f);
            card2Slot.transform.localScale = new Vector3(1f, 1f, 1f);
        }
        
        #endregion
    }
}
