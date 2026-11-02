using System;
using System.Collections;
using Player;
using UnityEngine;

namespace CreatureParts
{
    public class AttachablePart : CreaturePart
    {
        public Vector3 defaultConnectedAnchor;
        public Vector3 attachmentPosition;
        public Quaternion attachmentRotation;
        public Rigidbody attachedSegmentRigidbody;
        public Vector3 localPositionOnAttach;
        public Quaternion localRotationOnAttach;
        
        private Transform attachedEndPoint;
        private Vector3 savedAnchor;
        private Vector3 savedConnectedAnchor;

        public void CalculateConnection()
        {
            FindNearestWormSegment();
            attachmentPosition = transform.position;
            attachmentRotation = transform.rotation; 
        }
        
        public IEnumerator ResetJoint()
        {
            Debug.Log($"ResetJoint called on {gameObject.name} | attachedEndPoint: {attachedEndPoint} | attachedSegmentRigidbody: {attachedSegmentRigidbody}");

            float elapsed = 0f;
            while (((attachedSegmentRigidbody.isKinematic == true) || (gameObject.GetComponent<Rigidbody>().isKinematic == true)) && elapsed < 3f)
            {
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }
            
            Rigidbody partRb = gameObject.GetComponent<Rigidbody>();
            partRb.linearVelocity = Vector3.zero;
            partRb.angularVelocity = Vector3.zero;
            
            attachedSegmentRigidbody.linearVelocity = Vector3.zero;
            attachedSegmentRigidbody.angularVelocity = Vector3.zero;
            
            HingeJoint existing = GetComponent<HingeJoint>();
            Debug.Log($"Existing hinge: {existing}");
            if (existing != null)
                Destroy(existing);
            
            transform.position = attachedSegmentRigidbody.transform.TransformPoint(localPositionOnAttach);
            transform.rotation = attachedSegmentRigidbody.transform.rotation * localRotationOnAttach;

            HingeJoint hinge = gameObject.AddComponent<HingeJoint>();
            hinge.connectedBody = attachedSegmentRigidbody;
            hinge.autoConfigureConnectedAnchor = false;
            
            hinge.anchor = savedAnchor;
            hinge.connectedAnchor = savedConnectedAnchor;

            JointLimits limits = hinge.limits;
            limits.min = -10f;
            limits.max = 10f;
            hinge.limits = limits;
            hinge.useLimits = true;
            hinge.enablePreprocessing = true;
            hinge.enableCollision = false;

            IgnorePartCollisionWithWorm(gameObject, attachedSegmentRigidbody.transform);
        }
        
        public void ConfigureRigidBody(Rigidbody partRigidbody, float mass)
        {
            partRigidbody.mass = mass;
            
            partRigidbody.linearDamping = attachedSegmentRigidbody.linearDamping;
            partRigidbody.angularDamping = attachedSegmentRigidbody.angularDamping;
            partRigidbody.interpolation = attachedSegmentRigidbody.interpolation;
            partRigidbody.collisionDetectionMode = attachedSegmentRigidbody.collisionDetectionMode;
            
            partRigidbody.linearDamping = 1f;
            partRigidbody.angularDamping = 1f;
        }
        
        public void ConfigureHingeJoint(Transform endPoint)
        {
            attachedEndPoint = endPoint;
    
            HingeJoint hinge = gameObject.AddComponent<HingeJoint>();
            hinge.connectedBody = attachedSegmentRigidbody;
            hinge.anchor = gameObject.transform.InverseTransformPoint(endPoint.position);
    
            JointLimits limits = hinge.limits;
            limits.min = -10f;
            limits.max = 10f;
            hinge.limits = limits;
            hinge.useLimits = true;
            hinge.enablePreprocessing = true;
            hinge.enableCollision = false;
    
            defaultConnectedAnchor = hinge.connectedAnchor;
            
            savedAnchor = hinge.anchor;
            savedConnectedAnchor = hinge.connectedAnchor;
        }

        public void IgnorePartCollisionWithWorm(GameObject part, Transform nearestWormSegment)
        {
            int numSegments = GameParameters.NumSegmentCollisionsIgnored;
            
            Collider[] partColliders = part.GetComponentsInChildren<Collider>();
            
            IgnoreCollisionsInDirection(partColliders, nearestWormSegment, true, numSegments);
            IgnoreCollisionsInDirection(partColliders, nearestWormSegment, false, numSegments);
            
            foreach (var attachedWormPart in ParentPlayer.attachedWormParts)
            {
                if (attachedWormPart == null || attachedWormPart == part) continue;
                var a = part.GetComponent<Collider>();
                var b = attachedWormPart.GetComponent<Collider>();
                if (a != null && b != null) Physics.IgnoreCollision(a, b, true);
            }
        }
        
        public void SetLocalOffsets(Vector3 localPos, Quaternion localRot)
        {
            localPositionOnAttach = localPos;
            localRotationOnAttach = localRot;
        }
        
        private void FindNearestWormSegment() 
        {
            Debug.Log("finding nearest worm segment");
            Transform nearestPart = null;
            float shortestDistance = Mathf.Infinity;
    
            foreach (Transform wormSegment in LocalPlayer.Instance.GetComponent<Player.Player>().wormBodySegments)
            {
                float distance = Vector3.Distance(transform.position, wormSegment.position);
                
                if (distance < shortestDistance)
                {
                    shortestDistance = distance;
                    nearestPart = wormSegment;
                }
            }
            
            attachedSegmentRigidbody = nearestPart.GetComponent<Rigidbody>();
            Debug.Log("set attachedSegmentRigidBody to " + attachedSegmentRigidbody);
        }
        
        private void IgnoreCollisionsInDirection(Collider[] partColliders, Transform startSegment, bool forward, int numSegments)
        {
            Transform current = startSegment;

            for (int i = 0; i < numSegments && current != null; i++)
            { 
                Collider[] segmentColliders = current.GetComponentsInChildren<Collider>();
                
                foreach (var pCol in partColliders)
                {
                    foreach (var sCol in segmentColliders)
                    {
                        Physics.IgnoreCollision(pCol, sCol, true);
                    }
                }
                
                if (forward)
                {
                    current = current.childCount > 0 ? current.GetChild(0) : null;
                }
                else
                {
                    current = current.parent;
                }
            }
        }
    }
}
