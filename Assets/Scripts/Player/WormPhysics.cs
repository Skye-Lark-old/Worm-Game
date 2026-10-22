using System;
using System.Collections;
using System.Collections.Generic;
using CreatureParts;
using PurrNet;
using PurrNet.Prediction;
using Unity.VisualScripting;
using UnityEngine;

namespace Player
{
    public class WormPhysics : MonoBehaviour
    {
        #region Private Variables
        [Header("Private Variables")]
        
        private List<SphereCollider> segmentColliders = new List<SphereCollider>();
        private Player player;
        
        #endregion
        
        #region Built-In Methods

        public void Awake()
        {
            player = GetComponent<Player>();
        }
        
        #endregion

        #region Public Methods
        
        public void AddCollidersToSegments()
        {
            SphereCollider headCollider = player.wormHead.GetComponent<SphereCollider>();
            if (headCollider == null)
                headCollider = player.wormHead.gameObject.AddComponent<SphereCollider>();
        
            headCollider.radius = GameParameters.WormBodyWidth * 4;
        
            for (int i = 0; i < player.wormBodySegments.Count; i++)
            {
                SphereCollider segmentCollider = player.wormBodySegments[i].GetComponent<SphereCollider>();
                segmentCollider.radius = GameParameters.WormBodyWidth * 4;
            
                if (!segmentColliders.Contains(segmentCollider))
                    segmentColliders.Add(segmentCollider);
            }
        
            IgnoreWormSelfCollision();
        }

        public void ToggleWormKinematics(bool isKinematic)
        {
            Debug.Log("toggle worm kinematics called");
            
            if (isKinematic)
            {
                MakeWormKinematic();
            }
            else
            {
                MakeWormUnkinematic();
            }
        }
    
        public void MakeWormKinematic()
        {
            Debug.Log($"MakeWormKinematic on {player.PlayerName} | segments: {player.wormBodySegments.Count}", this);
            SetSegmentPhysics(player.wormHead, isKinematic: true, useGravity: false);
            foreach (Transform segment in player.wormBodySegments)
            {
                SetSegmentPhysics(segment, isKinematic: true, useGravity: false);
            }

            foreach (Transform part in player.wormBodySegments)
            {
                //TODO: update to attachedParts?
                SetSegmentPhysics(part, isKinematic: true, useGravity: false);
            }
        }

        public void MakeWormUnkinematic()
        {
            Debug.Log($"MakeWormUnkinematic on {player.PlayerName} | segments: {player.wormBodySegments.Count}", this);
            PredictedRigidbody headRb = player.wormHead.GetComponent<PredictedRigidbody>();
            if (headRb != null)
            {
                headRb.useGravity = true;
                headRb.isKinematic = false;
                headRb.linearVelocity = Vector3.zero;
                headRb.angularVelocity = Vector3.zero;
            }
            
            foreach (Transform segment in player.wormBodySegments)
            {
                PredictedRigidbody segmentRb = segment.GetComponent<PredictedRigidbody>();
                segmentRb.useGravity = true;
                segmentRb.isKinematic = false;
                segmentRb.linearVelocity = Vector3.zero;
                segmentRb.angularVelocity = Vector3.zero;
                
                segment.GetComponent<CreatureBodySegment>().ResetJointPhysics();
            }
        }

        public void SetSegmentPhysics(Transform segment, bool isKinematic, bool useGravity)
        {
            PredictedRigidbody rb = segment.GetComponent<PredictedRigidbody>();
            if (rb == null) return;
            
            rb.isKinematic = isKinematic;
            rb.useGravity = useGravity;

            if (isKinematic == false)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        public void PositionWormSegments(Vector3 headPosition)
        {
            player.wormHead.position = headPosition;
            Vector3 currentPosition = headPosition;
            Vector3 backDirection = -player.wormHead.forward;

            for (int i = 0; i < player.wormBodySegments.Count; i++)
            {
                currentPosition += backDirection * GameParameters.SegmentMaxPartDistance;
                player.wormBodySegments[i].position = currentPosition;
                player.wormBodySegments[i].rotation = player.wormHead.rotation;
            }
        }
        
        public void IgnoreWormSelfCollision()
        {
            List<Collider> allWormColliders = new List<Collider>();
        
            Collider headCollider = player.wormHead.GetComponent<Collider>();
            if (headCollider != null)
                allWormColliders.Add(headCollider);
        
            foreach (var segment in player.wormBodySegments)
            {
                Collider segmentCollider = segment.GetComponent<Collider>();
                if (segmentCollider != null)
                    allWormColliders.Add(segmentCollider);
            }
        
            int ignoreDistance = GameParameters.NumSegmentCollisionsIgnored;
        
            for (int i = 0; i < allWormColliders.Count; i++)
            {
                for (int j = i + 1; j < allWormColliders.Count; j++)
                {
                    if (Mathf.Abs(i - j) <= ignoreDistance)
                    {
                        Physics.IgnoreCollision(allWormColliders[i], allWormColliders[j], true);
                    }
                }
            }
        }

        public void ToggleWormCollisions(bool canCollide)
        {
            player.wormHead.GetComponent<Collider>().enabled = canCollide;

            foreach (Transform bodySegment in player.wormBodySegments)
            {
                bodySegment.GetComponent<Collider>().enabled = canCollide;
            }

            foreach (GameObject attachedPart in player.attachedWormParts)
            {
                foreach (Collider col in attachedPart.GetComponentsInChildren<Collider>(false))
                {
                    col.enabled = canCollide;
                }
            }
        }
        
        public void ResetWormOrientation()
        {
            player.wormVisualHead.rotation = Quaternion.identity;
            player.wormHead.rotation = Quaternion.identity;
            foreach (Transform segment in player.wormBodySegments)
            {
                segment.rotation = Quaternion.identity;
            }
        }
        
        public void IgnorePartCollisionWithWorm(GameObject part, Transform nearestWormSegment)
        {
            Debug.Log("ignoring part collision with worm");
            int numSegments = GameParameters.NumSegmentCollisionsIgnored;
            
            Collider[] partColliders = part.GetComponentsInChildren<Collider>();
            
            IgnoreCollisionsInDirection(partColliders, nearestWormSegment, true, numSegments);
            IgnoreCollisionsInDirection(partColliders, nearestWormSegment, false, numSegments);
            
            foreach (var attachedWormPart in LocalPlayer.Instance.attachedWormParts)
            {
                Physics.IgnoreCollision(part.GetComponent<Collider>(), attachedWormPart.GetComponent<Collider>(), true);
            }
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
        
        #endregion
    }
}