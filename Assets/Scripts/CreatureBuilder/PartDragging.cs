using CreatureParts;
using Graphics;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CreatureBuilder
{
    public class PartDragging : MonoBehaviour
    { 
        #region Public Variables
        [Header("Public Variables")]
        
        public GameObject Prefab => partData?.prefab;
        public CreaturePartData partData;
        public Camera targetCamera;
        public RectTransform creatureBuilderWindow;
        public Transform endPoint;
        public bool isClamped;
        public float dragDistance = 0f;
        public GameObject axisVisual;
        
        #endregion
        
        #region Private Variables
        [Header("Private Variables")]
        
        private GameObject falseWormBody;
        private CreatureBuilder creatureBuilder;
        
        private Vector3 lastValidPosition;
        private Vector3 lastMouseWorldPos;
        private Vector2 lastValidViewport;
        private Vector3 dragOffset;
        
        private bool isSelected;
        private bool isDragging;
        private bool doubleSelected;
        
        private Rigidbody rb;
        
        #endregion
    
        #region Built-In Methods
        void Start()
        {
            if (targetCamera == null)
            {
                enabled = false;       
                return;
            }
            
            if (SceneManager.GetActiveScene().name != "CreatureBuilderScene") return;
            
            creatureBuilder = GameObject.Find("CreatureBuilder").GetComponent<CreatureBuilder>();
            falseWormBody = GameObject.Find("falseWormBody");

            rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }

            if (!isClamped)
                StartDragging();
        }
    
        void Update()
        {
            if (isDragging)
            {
                Drag();
            }

            if (Input.GetMouseButtonUp(0))
            {
                StopDragging();
                GetComponent<AttachablePart>().CalculateConnection();
            }

            if (Input.GetKeyDown(KeyCode.Delete))
            {
                Delete3DPart();
            }
        }

        void OnDisable()
        {
            isSelected = false;
            isDragging = false;
            isClamped = false;
    
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }
        }
        
        #endregion

        #region Public Methods

        public void Clamp()
        {
            isClamped = true;
        }

        public void StartDragging()
        {
            SelectPart();
            isDragging = true;
            
            if (dragDistance == 0f)
                dragDistance = Vector3.Distance(targetCamera.transform.position, transform.position);
    
            lastValidPosition = transform.position;
            Vector3 viewportPos = targetCamera.WorldToViewportPoint(transform.position);
            lastValidViewport = new Vector2(viewportPos.x, viewportPos.y);
        }

        public void SelectPart()
        {
            if (isSelected) doubleSelected = true;
            isSelected = true;
            
            if (axisVisual != null)
                axisVisual.SetActive(true);
            
            if (!doubleSelected) GetComponent<HighlightOutline>().HighlightPart();
        }
    
        public void StopDragging()
        {
            isDragging = false;
        }

        public void DeselectPart()
        {
            isSelected = false;
            isDragging = false;
            doubleSelected = false;
    
            HighlightOutline draggingUI = GetComponent<HighlightOutline>();
            if (draggingUI != null)
            {
                draggingUI.RemoveHighlight();
            }
            else
            {
                Debug.LogError($"PartDraggingUI component not found on {gameObject.name}");
            }
    
            if (axisVisual != null)
            {
                axisVisual.SetActive(false);
            }
        }
        
        public void Delete3DPart()
        {
            if (isSelected)
            {
                creatureBuilder.SwitchFrom3DPartToCard(Prefab, gameObject);
                Destroy(gameObject);
            }
        }
        
        #endregion

        #region Private Methods
        private void Drag()
        {
            Vector2 viewportPos = GetViewportFromMouse();
            Ray ray = targetCamera.ViewportPointToRay(new Vector3(viewportPos.x, viewportPos.y, 0));
            
            float currentDragDistance = Vector3.Dot(transform.position - targetCamera.transform.position, targetCamera.transform.forward);
            Vector3 currentMouseWorldPos = ray.GetPoint(currentDragDistance);
            Vector3 targetPosition = currentMouseWorldPos + dragOffset;
            
            if (isClamped)
            {
                DragAlongSurface(currentMouseWorldPos - lastMouseWorldPos);
            }
            else
            {
                DragNotClamped(targetPosition, currentDragDistance);
            }
            
            lastMouseWorldPos = currentMouseWorldPos;
            RotateTowardWormBody();
        }

        private void DragAlongSurface(Vector3 mouseDelta)
        {
            if (falseWormBody == null || endPoint == null || !falseWormBody.TryGetComponent(out Collider wormCollider))
            {
                isClamped = false;
                return;
            }
            
            Vector3 currentClosest = wormCollider.ClosestPoint(endPoint.position);
            Vector3 surfaceNormal = (endPoint.position - currentClosest).normalized;
            
            if (surfaceNormal.magnitude < 0.001f)
                surfaceNormal = (endPoint.position - falseWormBody.transform.position).normalized;
            
            Vector3 tangentDelta = (mouseDelta - Vector3.Dot(mouseDelta, surfaceNormal) * surfaceNormal) * 0.7f;
            if (tangentDelta.magnitude < 0.0001f) return;
            
            Vector3 offset = endPoint.position - transform.position;
            Vector3 newEndPoint = transform.position + tangentDelta + offset;
            Vector3 newClosest = wormCollider.ClosestPoint(newEndPoint);
            Vector3 newNormal = (newEndPoint - newClosest).normalized;
            
            if (newNormal.magnitude < 0.001f)
                newNormal = (newEndPoint - falseWormBody.transform.position).normalized;
            
            transform.position = newClosest + newNormal * 0.02f - offset;
            
            if (Vector3.Distance(endPoint.position, wormCollider.ClosestPoint(endPoint.position)) > GameParameters.DistanceToClampPart)
                isClamped = false;
        }

        private void DragNotClamped(Vector3 targetPosition, float currentDragDistance)
        {
            float distanceFromCamera = Vector3.Distance(transform.position, targetCamera.transform.position);
            float smoothing = Mathf.Clamp01(distanceFromCamera / 50f) * 0.3f;
            
            targetPosition = Vector3.Lerp(targetPosition, transform.position, smoothing);
                
            if (CanMoveTo(targetPosition))
            {
                transform.position = targetPosition;
                lastValidPosition = targetPosition;
                lastValidViewport = GetViewportFromMouse();
            }
            else
            {
                Ray ray = targetCamera.ViewportPointToRay(new Vector3(lastValidViewport.x, lastValidViewport.y, 0));
                transform.position = ray.GetPoint(currentDragDistance) + dragOffset;
            }
                
            TryToClampToWormBody();
        }

        private void RotateTowardWormBody()
        {
            if (falseWormBody == null || endPoint == null || !falseWormBody.TryGetComponent(out Collider wormCollider))
                return;
            
            Vector3 wormToEndpoint = (endPoint.position - falseWormBody.transform.position).normalized;
            if (wormToEndpoint.magnitude < 0.001f) return;
            
            Vector3 offsetEndpoint = endPoint.position + wormToEndpoint * 0.15f;
            Vector3 inwardDirection = -wormToEndpoint;
            
            if (Physics.Raycast(offsetEndpoint, -wormToEndpoint, out RaycastHit hit, 0.65f) && hit.collider == wormCollider)
                inwardDirection = -hit.normal;
            
            Vector3 centerToEndpoint = (endPoint.position - transform.position).normalized;
            Quaternion targetRotation = Quaternion.FromToRotation(centerToEndpoint, inwardDirection) * transform.rotation;
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 0.15f);
        }
        
        private Vector2 GetViewportFromMouse()
        {
            Vector3[] corners = new Vector3[4];
            creatureBuilderWindow.GetWorldCorners(corners);
            Vector2 mousePos = Input.mousePosition;
            
            return new Vector2(
                Mathf.Clamp01(Mathf.InverseLerp(corners[0].x, corners[2].x, mousePos.x)),
                Mathf.Clamp01(Mathf.InverseLerp(corners[0].y, corners[2].y, mousePos.y))
            );
        }

        private void TryToClampToWormBody()
        {
            if (falseWormBody == null || endPoint == null || !falseWormBody.TryGetComponent(out Collider wormCollider))
                return;

            Vector3 closestPoint = wormCollider.ClosestPoint(endPoint.position);
            if (Vector3.Distance(endPoint.position, closestPoint) <= GameParameters.DistanceToClampPart)
            {
                transform.position = closestPoint - (endPoint.position - transform.position);
                isClamped = true;
            }
        }

        private bool CanMoveTo(Vector3 targetPosition)
        {
            foreach (Collider col in GetComponentsInChildren<Collider>())
            {
                Vector3 newColliderCenter = targetPosition + (col.bounds.center - transform.position);
                
                foreach (Collider overlap in Physics.OverlapBox(newColliderCenter, col.bounds.extents, transform.rotation))
                {
                    if (!overlap.transform.IsChildOf(transform) && overlap.transform != transform)
                        return false;
                }
            }
            return true;
        }
        
        #endregion
    }
}