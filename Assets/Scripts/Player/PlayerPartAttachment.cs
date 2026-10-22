using System.Collections;
using System.Collections.Generic;
using CreatureBuilder;
using CreatureParts;
using PurrNet;
using PurrNet.Prediction;
using UnityEngine;

namespace Player
{
    public class PlayerPartAttachment : NetworkBehaviour
    {
        public void AttachCreatureParts(List<GameObject> parts, List<PartPair> partPairs)
        {
            foreach (GameObject part in parts)
            {
                Debug.Log("attaching creature part " + part.name);
                PartDragging partDragging = part.GetComponent<PartDragging>();
        
                if (partDragging != null && partDragging.isClamped)
                {
                    AddPartToWorm(part);
                    Destroy(part);
                }
                else
                {
                    print("part not clamped, returning to player inventory");
                    ReturnPartToPlayerInventory(part, partPairs);
                }
            }
    
            parts.Clear();
        }
        
        [ServerRpc]
        public void AddAttachedPartServerSide(GameObject prefab, Vector3 position, Quaternion rotation, GameObject attachedSegment, float partMass, GameObject player, Vector3 localPos, Quaternion localRot)
        {
            GameObject networkedPart = Instantiate(prefab, position, rotation, player.transform);
            networkedPart.GetComponent<AttachablePart>().attachedSegmentRigidbody = attachedSegment.GetComponent<Rigidbody>();
            networkedPart.GetComponent<AttachablePart>().attachmentPosition = position;
            networkedPart.GetComponent<AttachablePart>().attachmentRotation = rotation;
            Debug.LogWarning("Could not properly give ownership because of switch to purrdiction");
            //networkedPart.GetComponent<AttachablePart>().GiveOwnership(player.GetComponent<NetworkTransform>().owner);
    
            AddAttachedPartForClients(networkedPart, player, partMass, attachedSegment, localPos, localRot);
            SyncLegOrderRpc(player);
        }

        [ObserversRpc(runLocally: true)]
        public void AddAttachedPartForClients(GameObject part, GameObject partPlayer, float partMass, GameObject attachedSegment, Vector3 localPos, Quaternion localRot)
        {
            Debug.Log($"AddAttachedPartForClients: part instanceID={part.GetInstanceID()} name={part.name}");
    
            var ap = part.GetComponent<AttachablePart>();
            ap.attachedSegmentRigidbody = attachedSegment.GetComponent<Rigidbody>();
            ap.attachmentPosition = part.transform.position;
            ap.attachmentRotation = part.transform.rotation;
            ap.SetLocalOffsets(localPos, localRot);
            
            part.transform.position = attachedSegment.GetComponent<Rigidbody>().transform.TransformPoint(localPos);
            part.transform.rotation = attachedSegment.GetComponent<Rigidbody>().transform.rotation * localRot;

            Player targetPlayer = partPlayer.GetComponent<Player>();
            targetPlayer.attachedWormParts.Add(part);

            part.GetComponent<PartDragging>().DeselectPart();
            part.GetComponent<PartDragging>().enabled = false;

            var rb = part.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
            }

            LegPart legPart = part.GetComponent<LegPart>();
            if (legPart != null)
            {
                targetPlayer.MaxVelocity += GameParameters.LegMaxVelocityIncrease;
                legPart.enabled = true;
            }

            Rigidbody partRigidbody = part.GetComponent<Rigidbody>();
            if (partRigidbody == null)
                partRigidbody = part.AddComponent<Rigidbody>();
    
            ap.ConfigureRigidBody(partRigidbody, partMass);

            Transform endPoint = part.GetComponent<PartDragging>().endPoint;
            if (endPoint == null)
                return;

            if (isOwner && owner == localPlayer) ap.ConfigureHingeJoint(endPoint);
            
            partPlayer.GetComponent<WormPhysics>().IgnorePartCollisionWithWorm(part, ap.attachedSegmentRigidbody.transform);
        }
        
        [ObserversRpc]
        public void SyncLegOrderRpc(GameObject specificPlayer)
        {
            List<LegPart> legParts = new List<LegPart>();
            foreach (GameObject part in specificPlayer.GetComponent<Player>().attachedWormParts)
            {
                LegPart leg = part.GetComponent<LegPart>();
                if (leg != null)
                    legParts.Add(leg);
            }

            float totalTime = GameParameters.LegMoveTime;
            for (int i = 0; i < legParts.Count; i++)
            {
                legParts[i].timeOffset = i * (totalTime / legParts.Count);
            }
        }

        [ServerRpc]
        public void DestroyPart(GameObject part)
        {
            Destroy(part);
        }
        
        public IEnumerator ReactivateAttachedParts()
        {
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
    
            foreach (GameObject attachedPart in GetComponent<Player>().attachedWormParts)
            {
                Debug.Log($"ReactivateAttachedParts: part instanceID={attachedPart.GetInstanceID()} name={attachedPart.name}");
                attachedPart.SetActive(true);
                if (isOwner && owner == localPlayer) yield return StartCoroutine(attachedPart.GetComponent<AttachablePart>().ResetJoint());
            }
    
            GetComponent<WormPhysics>().IgnoreWormSelfCollision();
        }

        [ServerRpc]
        public void ClearAttachedParts()
        {
            ClearAttachedPartsObserverSide();
        }
        
        public void AddAlreadyAttachedParts()
        {
            if (!isOwner) return;
            
            Debug.Log("adding already attached parts, LocalPlayer owner: " + LocalPlayer.Instance.owner);

            foreach (GameObject part in LocalPlayer.Instance.attachedWormParts)
            {
                Debug.Log("adding already attached part " + part.name);
                AddAlreadyAttachedPart(part);
            }
            
            ClearAttachedParts();
        }
        
        #region Private Methods
        
        [ObserversRpc]
        private void ClearAttachedPartsObserverSide()
        {
            GetComponent<Player>().attachedWormParts.Clear();
        }
        
        private void AddPartToWorm(GameObject creaturePart)
        {
            AttachablePart attachablePart = creaturePart.GetComponent<AttachablePart>();
            Rigidbody attachedSegment = attachablePart.attachedSegmentRigidbody;
            Vector3 position = attachablePart.attachmentPosition;
            Quaternion rotation = attachablePart.attachmentRotation;
            GameObject prefab = creaturePart.GetComponent<PartDragging>().Prefab;
            float partMass = creaturePart.GetComponent<PartDragging>().partData.mass;
            
            Vector3 localPos = attachedSegment.transform.InverseTransformPoint(position);
            Quaternion localRot = Quaternion.Inverse(attachedSegment.transform.rotation) * rotation;

            AddAttachedPartServerSide(prefab, position, rotation, attachedSegment.gameObject, partMass, gameObject, localPos, localRot);
        }
        
        private void ReturnPartToPlayerInventory(GameObject part, List<PartPair> partPairs)
        {
            string partName = part.name.Replace("(Clone)", "").Trim();
            
            foreach (var pair in partPairs)
            {
                if (pair.part3DPrefab != null && pair.part3DPrefab.name == partName)
                {
                    GetComponent<Player>().wormPartsInInventory.Add(pair.cardPrefab);
                    Destroy(part);
                    return;
                }
            }
            
            Destroy(part);
        }

        private void AddAlreadyAttachedPart(GameObject part)
        {
            var netRb = part.GetComponent<PredictedRigidbody>();
            if (netRb != null) netRb.enabled = false;
            
            PartDragging partDraggingComponent = part.GetComponent<PartDragging>();
            GameObject prefab = partDraggingComponent.Prefab;

            if (prefab == null)
            {
                Debug.LogWarning($"Prefab reference is null for {part.name}");
                return;
            }
    
            // Check for LegPart component directly
            LegPart legPart = part.GetComponent<LegPart>();
            if (legPart != null)
            {
                GetComponent<Player>().MaxVelocity -= GameParameters.LegMaxVelocityIncrease;
                legPart.enabled = false;
            }
    
            CreateDuplicatePart(part, prefab);
        }

        private void CreateDuplicatePart(GameObject part, GameObject prefab)
        {
            Vector3 position = part.GetComponent<AttachablePart>().attachmentPosition;
            Quaternion rotation = part.GetComponent<AttachablePart>().attachmentRotation;

            GameObject newPart = UnityProxy.InstantiateDirectly(prefab, position, rotation);
            newPart.GetComponent<PredictedRigidbody>().enabled = false;
            newPart.GetComponent<Rigidbody>().isKinematic = true;
            newPart.GetComponent<Rigidbody>().useGravity = false;
            
            AttachablePart newAttachablePart = newPart.GetComponent<AttachablePart>();
            AttachablePart oldAttachablePart = part.GetComponent<AttachablePart>();
            newAttachablePart.attachedSegmentRigidbody = oldAttachablePart.attachedSegmentRigidbody;
            newAttachablePart.attachmentPosition = oldAttachablePart.attachmentPosition;
            newAttachablePart.attachmentRotation = oldAttachablePart.attachmentRotation;
            
            DontDestroyOnLoad(newPart);
            newPart.name = prefab.name;
            newPart.transform.localScale = part.transform.localScale;
                
            PartDragging partDragging = newPart.GetComponent<PartDragging>();

            CreatureBuilder.CreatureBuilder creatureBuilder = FindFirstObjectByType<CreatureBuilder.CreatureBuilder>();
            
            if (partDragging != null)
            {
                partDragging.enabled = true;
                creatureBuilder.ResetPartDragging(partDragging);
                partDragging.Clamp();
            }
            
            LegPart legPart = newPart.GetComponent<LegPart>();
            if (legPart != null)
            {
                legPart.enabled = false;
            }

            creatureBuilder.parts.Add(newPart);
            DestroyPart(part);
        }
        
        #endregion
    }
}
