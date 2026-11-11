using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CreatureBuilder
{
    public class InventoryItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        #region Public Variables
        [Header("Public Variables")]
        
        public GameObject prefab;
        public InventorySlot originalSlot;
        public bool isInfiniteSlot;
        
        #endregion
        
        #region Private Variables
        [Header("Public Variables")]
        
        private GameObject creatureBuilderWindow;
        private CreatureBuilder creatureBuilder;
        
        private InventorySlot currentSlot;
        private Transform originalParent;
        private Canvas canvas;
        private CanvasGroup canvasGroup;
        private RectTransform rectTransform;
        private Vector3 startPosition;
        
        #endregion

        #region Built-In Methods
        
        void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            canvas = GetComponentInParent<Canvas>();
            
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
            
            creatureBuilderWindow = GameObject.Find("Creature Builder Window");
            creatureBuilder = GameObject.Find("CreatureBuilder").GetComponent<CreatureBuilder>();
        }

        #endregion

        #region Public Methods
        public void Initialize(InventorySlot slot)
        {
            currentSlot = slot;
            originalSlot = slot;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            startPosition = rectTransform.position;
            originalParent = transform.parent;
            originalSlot = currentSlot;
            
            transform.SetParent(canvas.transform);
            
            canvasGroup.alpha = GameParameters.CardTransparencyWhileDragging;
            canvasGroup.blocksRaycasts = false;
        }

        public void OnDrag(PointerEventData eventData)
        {
            rectTransform.position = eventData.position;
            
            CheckIfOverCreatureBuilderWindow(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
            
            bool droppedOnSlot = false;
            if (eventData.pointerEnter != null)
            {
                InventorySlot slot = eventData.pointerEnter.GetComponent<InventorySlot>();
                if (slot != null)
                {
                    droppedOnSlot = true;
                }
            }
            
            if (!droppedOnSlot)
            {
                transform.SetParent(originalSlot.transform);
                rectTransform.anchoredPosition = Vector2.zero;
            }
        }
        
        public void SetNewSlot(InventorySlot newSlot)
        {
            if (currentSlot != null)
            {
                currentSlot.ClearItem();
            }
            
            transform.SetParent(newSlot.transform);
            rectTransform.anchoredPosition = Vector2.zero;
            
            currentSlot = newSlot;
            newSlot.SetItem(this);
        }
        
        
        #endregion
        
        #region Private Methods
        
        private void CheckIfOverCreatureBuilderWindow(PointerEventData eventData)
        {
            foreach (var raycastResult in eventData.hovered)
            {
                if (raycastResult == creatureBuilderWindow || raycastResult.transform.IsChildOf(creatureBuilderWindow.transform))
                {
                    OnEnterCreatureBuilder();
                }
            }
        }

        private void OnEnterCreatureBuilder()
        {
            if (isInfiniteSlot == true)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.blocksRaycasts = true;
                transform.SetParent(originalSlot.transform);
                rectTransform.anchoredPosition = Vector2.zero;
            }

            creatureBuilder.SwitchFromCardTo3DPart(prefab, this);
            gameObject.SetActive(false);
            if (isInfiniteSlot == false)
            {
                DestroySelf();
            }
        }

        public void DestroySelf()
        {
            Destroy(gameObject);
        }
        
        #endregion
    }
}