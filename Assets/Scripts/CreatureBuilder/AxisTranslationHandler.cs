using UnityEngine;

namespace CreatureBuilder
{
    public class AxisTranslationHandler : MonoBehaviour
    {
        public Vector3 localAxis = Vector3.right;   //Set per cube/arrow/whatever (x or z)
        public float moveSpeed = 0.01f;
        public Transform targetPart;

        private bool isDragging;
        private Vector3 lastMousePosition;
        private PartDragging partDragging;

        void Awake()
        {
            partDragging = GetComponentInParent<PartDragging>();

            if (partDragging != null)
                targetPart = partDragging.transform;
        }

        void Update()
        {
            if (!isDragging || targetPart == null || partDragging == null || partDragging.targetCamera == null)
                return;

            Vector3 mouseDelta = Input.mousePosition - lastMousePosition;
            lastMousePosition = Input.mousePosition;

            Vector3 worldAxis = targetPart.TransformDirection(localAxis);
            float movementAmount = Vector3.Dot(mouseDelta, partDragging.targetCamera.transform.right) * moveSpeed;
            Vector3 movement = worldAxis * movementAmount;

            MoveAlwaysClamped(movement);
        }

        private void MoveAlwaysClamped(Vector3 movement)
        {
            GameObject falseWormBody = GameObject.Find("falseWormBody");
            if (falseWormBody == null || partDragging.endPoint == null ||
                !falseWormBody.TryGetComponent(out Collider wormCollider))
            {
                targetPart.position += movement; // fallback
                return;
            }
        
            Vector3 currentClosest = wormCollider.ClosestPoint(partDragging.endPoint.position);
            Vector3 surfaceNormal = (partDragging.endPoint.position - currentClosest).normalized;
            if (surfaceNormal.magnitude < 0.001f)
                surfaceNormal = (partDragging.endPoint.position - falseWormBody.transform.position).normalized;

            Vector3 tangentDelta = movement - Vector3.Dot(movement, surfaceNormal) * surfaceNormal;
            if (tangentDelta.magnitude < 0.00001f) return;

            Vector3 offset = partDragging.endPoint.position - targetPart.position;
            Vector3 newEndPoint = targetPart.position + tangentDelta + offset;
            Vector3 newClosest = wormCollider.ClosestPoint(newEndPoint);
            Vector3 newNormal = (newEndPoint - newClosest).normalized;
            if (newNormal.magnitude < 0.001f)
                newNormal = (newEndPoint - falseWormBody.transform.position).normalized;

            targetPart.position = newClosest + newNormal * 0.02f - offset;
        
            Quaternion targetRotation = Quaternion.FromToRotation(
                (partDragging.endPoint.position - targetPart.position).normalized,
                -newNormal
            ) * targetPart.rotation;

            targetPart.rotation = Quaternion.Slerp(targetPart.rotation, targetRotation, 0.15f);
        }
        public void StartTranslation()
        {
            isDragging = true;
            lastMousePosition = Input.mousePosition;

            if (targetPart != null)
                targetPart.GetComponent<PartDragging>().enabled = false;
        }

        public void StopTranslation()
        {
            isDragging = false;

            if (targetPart != null)
                targetPart.GetComponent<PartDragging>().enabled = true;
        }
    }
}