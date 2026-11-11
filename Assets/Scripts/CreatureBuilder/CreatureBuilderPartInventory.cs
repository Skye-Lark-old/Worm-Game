using System;
using System.Collections;
using System.Collections.Generic;
using Player;
using UnityEngine;

namespace CreatureBuilder
{
    public class CreatureBuilderPartInventory : MonoBehaviour
    {
        private InventorySlot[] slots;

        void Awake()
        {
            slots = GetComponentsInChildren<InventorySlot>();
            
            foreach (var slot in slots)
            {
                slot.Initialize(this);
            }
            
            StartCoroutine(AddStartingCardsToInventory());
        }

        private void OnDisable()
        {
            ReturnAllCardsToPlayerInventory();
        }

        #region public methods
        
        public bool AddCardToInventory(GameObject cardPrefab)
        {
            InventorySlot emptySlot = GetEmptySlot();
            
            if (emptySlot != null)
            {
                GameObject cardInstance = Instantiate(cardPrefab, emptySlot.transform);
                
                RectTransform cardRect = cardInstance.GetComponent<RectTransform>();
                if (cardRect != null)
                {
                    cardRect.anchoredPosition = Vector2.zero;
                    cardRect.localScale = Vector3.one;
                }
                
                InventoryItem item = cardInstance.GetComponent<InventoryItem>();
                if (item != null)
                {
                    item.prefab = cardPrefab;
                    item.Initialize(emptySlot);
                    emptySlot.SetItem(item);
                    return true;
                }
                else
                {
                    Debug.LogWarning("Card prefab doesn't have InventoryItem component");
                    Destroy(cardInstance);
                    return false;
                }
            }
            else
            {
                Debug.LogWarning("No empty inventory slot available");
                return false;
            }
        }
        
        #endregion
        
        #region private methods
        
        private IEnumerator AddStartingCardsToInventory()
        {
            yield return new WaitForSeconds(0.1f);
            
            List<GameObject> partCards = LocalPlayer.Instance.wormPartsInInventory;
            foreach (var part in partCards)
            {
                AddCardToInventory(part);
            }
            LocalPlayer.Instance.wormPartsInInventory.Clear();
        }

        private InventorySlot GetEmptySlot()
        {
            foreach (var slot in slots)
            {
                if (slot.currentItem == null)
                {
                    return slot;
                }
            }

            return null;
        }
        
        private void ReturnAllCardsToPlayerInventory()
        {
            Debug.Log("returning all cards");
            
            foreach (var slot in slots)
            {
                if (slot.currentItem != null)
                {
                    GameObject cardInstance = slot.currentItem.gameObject;
                    var cardPrefab = slot.currentItem.prefab;

                    if (!slot.currentItem.GetComponent<InventoryItem>().isInfiniteSlot)
                    {
                        LocalPlayer.Instance.wormPartsInInventory.Add(cardPrefab);
                    }
                    
                    Destroy(cardInstance);
                }
            }
        }
        
    }
    
    #endregion
}