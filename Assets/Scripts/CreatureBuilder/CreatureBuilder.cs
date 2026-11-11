using System;
using System.Collections.Generic;
using CreatureParts;
using Player;
using PurrNet;
using Unity.Cinemachine;
using UnityEngine;

namespace CreatureBuilder
{
    public class CreatureBuilder : MonoBehaviour
    {
        #region public variables
        [Header("Public Variables")]
        
        public List<GameObject> parts = new List<GameObject>();
        public List<GameObject> legs = new List<GameObject>();
        
        public Camera targetCamera;
        public CinemachineCamera cinemachineCamera;
        public RectTransform creatureBuilderWindow;
        public CreatureBuilderWindow creatureBuilderScript;
        
        public event Action OnPartTo3D;
        #endregion
        
        #region private variables
        [Header("Private Variables")]
        
        private Player.Player player;

        private InventoryItem hiddenCard;
        
        [SerializeField] private float spawnDistance = 5f;
        
        [SerializeField] private List<PartPair> partPairs = new List<PartPair>();
        private readonly Dictionary<string, GameObject> prefabMapping = new Dictionary<string, GameObject>();
        #endregion

        #region Built-In Methods
        
        private void Awake()
        {
            if (LocalPlayer.Instance == null)
                LocalPlayer.OnLocalPlayerReady += OnLocalPlayerReady;
            else
            {
                InitializePrefabMapping();
                player = LocalPlayer.Instance;
                cinemachineCamera.Follow = player.transform;
            }
            LoadingScreenManager.LoadingScreenForSelf(false);
        }

        private void OnLocalPlayerReady()
        {
            InitializePrefabMapping();
            player = LocalPlayer.Instance;
            cinemachineCamera.Follow = player.transform;
            
        }
        
        void Update()
        {
            if (Input.GetMouseButtonUp(0) && hiddenCard != null)
            {
                hiddenCard.gameObject.SetActive(true);
                hiddenCard = null;
            }
        }

        private void OnDisable()
        {
            LocalPlayer.Instance.GetComponent<PlayerPartAttachment>().AttachCreatureParts(parts, partPairs);
        }

        #endregion

        #region public methods
        
        public void SwitchFromCardTo3DPart(GameObject cardPrefab, InventoryItem card)
        {
            if (card.isInfiniteSlot)
            {
                hiddenCard = card;
            }
            else
            {
                card.DestroySelf();
            }
            string cardName = cardPrefab.name.Replace("(Clone)", "").Trim();
            Debug.Log($"Looking up: '{cardName}' | Available keys: {string.Join(", ", prefabMapping.Keys)}");
            
            if (prefabMapping.TryGetValue(cardName, out GameObject prefab3D))
            {
                Vector3 spawnPosition = CalculateWorldSpawnPosition();
                if (creatureBuilderScript.selectedPart)
                {
                    creatureBuilderScript.selectedPart
                        .GetComponent<PartDragging>()
                        .DeselectPart();
                }
                GameObject spawnedPart = SpawnPartInWorld(prefab3D, spawnPosition);
                creatureBuilderScript.selectedPart = spawnedPart;
                creatureBuilderScript.selectedPart.GetComponent<PartDragging>().SelectPart();
            }
            else
            {
                Debug.LogWarning($"No 3D prefab mapping found for card: {cardName}");
            }
            
            OnPartTo3D?.Invoke();
        }
        
        public void SwitchFrom3DPartToCard(GameObject partPrefab, GameObject caller)
        {
            string partName = partPrefab.name.Replace("(Clone)", "").Trim();
            
            foreach (var pair in partPairs)
            {
                if (pair.part3DPrefab != null && pair.part3DPrefab.name == partName)
                {
                    SpawnCardInInventory(pair.cardPrefab);
                    
                    parts.Remove(caller);
                    Destroy(caller);
                    return;
                }
            }
        }
        
        public void ResetPartDragging(PartDragging partDragging)
        {
            partDragging.targetCamera = targetCamera;
            partDragging.creatureBuilderWindow = creatureBuilderWindow;
            partDragging.dragDistance = spawnDistance;
            partDragging.axisVisual.SetActive(false);
        }
        
        #endregion
        
        #region private methods

        private void InitializePrefabMapping()
        {
            prefabMapping.Clear();
            foreach (var pair in partPairs)
            {
                if (pair.cardPrefab != null && pair.part3DPrefab != null)
                {
                    string cardName = pair.cardPrefab.name;
                    prefabMapping[cardName] = pair.part3DPrefab;
                }
            }
        }
        
        private void SpawnCardInInventory(GameObject cardPrefab)
        {
            CreatureBuilderPartInventory inventory = FindFirstObjectByType<CreatureBuilderPartInventory>();
    
            if (inventory != null)
            {
                bool success = inventory.AddCardToInventory(cardPrefab);
                if (!success)
                {
                    Debug.LogWarning("Failed to add card to inventory");
                }
            }
            else
            {
                Debug.LogWarning("Inventory not found");
            }
        }

        private Vector3 CalculateWorldSpawnPosition()
        {
            Vector3[] corners = new Vector3[4];
            creatureBuilderWindow.GetWorldCorners(corners);
    
            // corners[0] = bottom-left, corners[1] = top-left, corners[2] = top-right, corners[3] = bottom-right
            Vector2 mousePos = Input.mousePosition;
            
            float viewportX = Mathf.InverseLerp(corners[0].x, corners[2].x, mousePos.x);
            float viewportY = Mathf.InverseLerp(corners[0].y, corners[2].y, mousePos.y);
            
            viewportX = Mathf.Clamp01(viewportX);
            viewportY = Mathf.Clamp01(viewportY);
            
            Ray ray = targetCamera.ViewportPointToRay(new Vector3(viewportX, viewportY, 0));
            return ray.GetPoint(spawnDistance);
        }
        
        private GameObject SpawnPartInWorld(GameObject prefab, Vector3 position)
        {
            GameObject newPart = UnityProxy.InstantiateDirectly(prefab, position, Quaternion.identity);
            if (newPart.GetComponent<MeshCollider>() != null)
            {
                newPart.GetComponent<MeshCollider>().convex = false;
            }
            DontDestroyOnLoad(newPart);
            
            foreach (Rigidbody rb in newPart.GetComponentsInChildren<Rigidbody>())
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }
            
            newPart.name = prefab.name;
    
            PartDragging partDragging = newPart.GetComponent<PartDragging>();
            if (partDragging != null)
            {
                ResetPartDragging(partDragging);
                parts.Add(partDragging.gameObject);
            }
            
            LegPart legPart = newPart.GetComponent<LegPart>();
            if (legPart != null)
            {
                legPart.enabled = false;
            }
            return newPart;
        }
        
        #endregion
        
        public void Skip()
        {
            GameLoop.GameLoop.gameLoopTimer.Skip();
        }
    }
    
   
}