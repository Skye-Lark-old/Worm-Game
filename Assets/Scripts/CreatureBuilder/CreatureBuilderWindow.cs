using NUnit.Framework.Constraints;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CreatureBuilder
{
    public class CreatureBuilderWindow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IInputAxisController
    {
        #region public variables
        [Header("Public Variables")]
        
        public GameObject cinemachineCamera;
        public GameObject targetCameraObject;
        public GameObject selectedPart = null;
        public GameObject deletePartButton;
        
        #endregion
        
        #region private variables
        [Header("Private Variables")]
        
        private Camera targetCamera;
        private bool isMouseOver = false;
        private bool wasCameraEnabled = false;
        private bool isDragging = false;
        private bool isDraggingPart = false;
        private IInputAxisController inputProvider;
        private CinemachineCamera cmCam;
        private float scrollActiveTimer = 0f;
        [SerializeField] private float zoomSpeed = 10f;
        [SerializeField] private float minFOV = 20f;
        [SerializeField] private float maxFOV = 60f;
        [SerializeField] private float scrollCameraHoldTime = 0.15f;
        
        #endregion

        #region Built-In Methods
        
        void Start()
        {
            if (cinemachineCamera != null)
            {
                wasCameraEnabled = cinemachineCamera.activeSelf;
                inputProvider = cinemachineCamera.GetComponent<IInputAxisController>();
                
                if (!isMouseOver)
                {
                    cinemachineCamera.SetActive(false);
                }
                
            }
            targetCamera = targetCameraObject.GetComponent<Camera>();
            cmCam = cinemachineCamera.GetComponent<CinemachineCamera>();
        }
        
        void Update()
        {
            if (selectedPart)
                deletePartButton.SetActive(true);
            else
                deletePartButton.SetActive(false);
            
            if (isMouseOver && !isDraggingPart)
            {
                float scroll = Input.mouseScrollDelta.y;

                if (Mathf.Abs(scroll) > 0.01f)
                {
                    cmCam.Lens.FieldOfView -= scroll * zoomSpeed;
                    cmCam.Lens.FieldOfView = Mathf.Clamp(
                        cmCam.Lens.FieldOfView,
                        minFOV,
                        maxFOV
                    );
                    cinemachineCamera.SetActive(true);

                    scrollActiveTimer = scrollCameraHoldTime;
                }
            }
            
            if (!isDragging)
            {
                if (scrollActiveTimer > 0f)
                {
                    scrollActiveTimer -= Time.deltaTime;
                }
                else
                {
                    cinemachineCamera.SetActive(false);
                }
            }
        }
        
        void OnDisable()
        {
            if (cinemachineCamera != null)
            {
                cinemachineCamera.SetActive(wasCameraEnabled);
            }
            isDragging = false;
        }
        
        public void OnPointerEnter(PointerEventData eventData)
        {
            isMouseOver = true;
        }
        
        public void OnPointerExit(PointerEventData eventData)
        {
            isMouseOver = false;
            
            if (!isDraggingPart)
            {
                //isDragging = false;
            }
        }
    
        public void OnPointerDown(PointerEventData eventData)
        {
            if (isMouseOver)
            {
                if (IsOverCreaturePart(out GameObject hitPart))
                {
                    if (hitPart.CompareTag("RotationHandle"))
                    {
                        AxisRotationHandler handler = hitPart.GetComponent<AxisRotationHandler>();
                        if (selectedPart != handler.hostPart)
                        {
                            if (selectedPart)
                                GetPartDragging(selectedPart)?.DeselectPart();

                            selectedPart = handler.hostPart;
                            GetPartDragging(selectedPart)?.SelectPart();
                        }
                        handler?.StartRotation();
                        return;
                    }

                    if (hitPart.CompareTag("TranslationHandle"))
                    {
                        AxisTranslationHandler handler = hitPart.GetComponent<AxisTranslationHandler>();
                        if (selectedPart != handler.targetPart.gameObject)
                        {
                            if (selectedPart)
                                GetPartDragging(selectedPart)?.DeselectPart();

                            selectedPart = handler.targetPart.gameObject;
                            GetPartDragging(selectedPart)?.SelectPart();
                        }
                        handler?.StartTranslation();
                        return;
                    }

                    if (hitPart.CompareTag("Axis"))
                    {
                        PartDragging parentPart = hitPart.GetComponentInParent<PartDragging>();
                        if (parentPart != null)
                            hitPart = parentPart.gameObject;
                    }

                    if (hitPart != selectedPart)
                    {
                        if (selectedPart)
                            GetPartDragging(selectedPart)?.DeselectPart();

                        selectedPart = null;
                    }

                    // Resolve to the GameObject that actually owns PartDragging
                    PartDragging dragging = GetPartDragging(hitPart);
                    if (dragging == null)
                    {
                        Debug.LogWarning($"No PartDragging found on {hitPart.name} or any parent.");
                        return;
                    }

                    isDraggingPart = true;
                    selectedPart = dragging.gameObject; 
                    dragging.StartDragging();
                }
                else
                {
                    if (HasValidSelection())
                        GetPartDragging(selectedPart)?.DeselectPart();

                    selectedPart = null;
                    isDragging = true;
                    cinemachineCamera.SetActive(true);
                }
            }
        }
        
        public void OnPointerUp(PointerEventData eventData)
        {
            if (selectedPart != null)
            {
                AxisRotationHandler[] handlers = selectedPart.GetComponentsInChildren<AxisRotationHandler>();
                foreach (var h in handlers)
                    h.StopRotation();
                
                AxisTranslationHandler[] translationHandlers =
                    selectedPart.GetComponentsInChildren<AxisTranslationHandler>();

                foreach (var t in translationHandlers)
                    t.StopTranslation();
            }
            
            isDraggingPart = false;
            isDragging = false;
            cinemachineCamera.SetActive(false);
        }
        
        public void DeleteSelectedPart()
        {
            if (!selectedPart) return;
            selectedPart.GetComponent<PartDragging>().Delete3DPart();
            selectedPart = null;
        }

        public bool ControllersAreValid()
        {
            return true;
        }
        
        public void SynchronizeControllers()
        {
            
        }
        
        #endregion
    
        #region Private Methods
        
        private bool IsOverCreaturePart(out GameObject hitObject)
        {
            hitObject = null;

            if (targetCamera == null || gameObject.GetComponent<RectTransform>() == null)
                return false;

            Vector3[] corners = new Vector3[4];
            gameObject.GetComponent<RectTransform>().GetWorldCorners(corners);

            Vector2 mousePos = Input.mousePosition;

            float viewportX = Mathf.InverseLerp(corners[0].x, corners[2].x, mousePos.x);
            float viewportY = Mathf.InverseLerp(corners[0].y, corners[2].y, mousePos.y);

            viewportX = Mathf.Clamp01(viewportX);
            viewportY = Mathf.Clamp01(viewportY);

            Ray ray = targetCamera.ViewportPointToRay(new Vector3(viewportX, viewportY, 0));
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                GameObject hitGO = hit.collider.gameObject;

                if (hitGO.CompareTag("RotationHandle") ||
                    hitGO.CompareTag("TranslationHandle"))
                {
                    hitObject = hitGO;
                    return true;
                }
                
                Transform current = hit.collider.transform;
                
                int safetyCounter = 0; // prevent infinite loops
                while (current != null && safetyCounter < 10)
                {
                    if (current.CompareTag("CreaturePart"))
                    {
                        hitObject = current.gameObject;
                        return true;
                    }
                    current = current.parent;
                    safetyCounter++;
                }
            }

            return false;
        }
        
        private PartDragging GetPartDragging(GameObject go)
        {
            Transform current = go.transform;
            int safety = 0;
            while (current != null && safety < 10)
            {
                PartDragging pd = current.GetComponent<PartDragging>();
                if (pd != null) return pd;
                current = current.parent;
                safety++;
            }
            return null;
        }
        
        private bool HasValidSelection()
        {
            return selectedPart != null && selectedPart;
        }
        
        #endregion
    }
}