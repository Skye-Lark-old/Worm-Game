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
        // parts that have been created on the server but may not exist on this peer yet
        private struct PendingPart
        {
            public PredictedObjectID id;
            public int partId;
            public int segmentIndex;      // -1 = head
            public Vector3 localPos;
            public Quaternion localRot;
            public float enqueuedAt;
        }
        private readonly Queue<PendingPart> pending = new Queue<PendingPart>();

        #region Attaching

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

        private void AddPartToWorm(GameObject creaturePart)
        {
            AttachablePart attachablePart = creaturePart.GetComponent<AttachablePart>();
            PartDragging dragging = creaturePart.GetComponent<PartDragging>();
            Rigidbody attachedSegment = attachablePart.attachedSegmentRigidbody;
            if (attachedSegment == null)
            {
                Debug.LogError($"[Attach] {creaturePart.name} has no attachedSegmentRigidbody");
                return;
            }

            var p = GetComponent<Player>();

            int segmentIndex;
            if (attachedSegment.transform == p.wormHead) segmentIndex = -1;
            else
            {
                segmentIndex = p.wormBodySegments.IndexOf(attachedSegment.transform);
                if (segmentIndex < 0)
                {
                    Debug.LogError("[Attach] part is attached to something that isn't a worm segment");
                    return;
                }
            }

            int partId = PartRegistry.IdOf(dragging.partData);
            Vector3 localPos = attachedSegment.transform.InverseTransformPoint(attachablePart.attachmentPosition);
            Quaternion localRot = Quaternion.Inverse(attachedSegment.transform.rotation) * attachablePart.attachmentRotation;

            AddAttachedPartServerSide(partId, segmentIndex, localPos, localRot);
        }

        [ServerRpc]
        public void AddAttachedPartServerSide(int partId, int segmentIndex, Vector3 localPos, Quaternion localRot)
        {
            var p = GetComponent<Player>();
            if (!PartRegistry.TryGet(partId, out CreaturePartData data))
            {
                Debug.LogError($"[Attach] unknown part id {partId}");
                return;
            }

            Transform seg = segmentIndex < 0 ? p.wormHead
                          : segmentIndex < p.wormBodySegments.Count ? p.wormBodySegments[segmentIndex] : null;
            if (seg == null) { Debug.LogError("[Attach] bad segment index on server"); return; }

            Vector3 worldPos = seg.TransformPoint(localPos);
            Quaternion worldRot = seg.rotation * localRot;

            // created as a child of the player so GetComponentInParent<Player>() and transform.root work on every peer
            PredictedObjectID? id = p.predictionManager.hierarchy.Create(
                data.prefab,
                p.transform.InverseTransformPoint(worldPos),
                Quaternion.Inverse(p.transform.rotation) * worldRot,
                p,
                p.owner);

            if (id == null)
            {
                Debug.LogError("[Attach] part create failed (is the prefab in PredictedPrefabs?)");
                return;
            }

            AddAttachedPartForClients(id.Value, partId, segmentIndex, localPos, localRot);
        }

        [ObserversRpc(runLocally: true)]
        public void AddAttachedPartForClients(PredictedObjectID partObjectId, int partId, int segmentIndex, Vector3 localPos, Quaternion localRot)
        {
            pending.Enqueue(new PendingPart
            {
                id = partObjectId, partId = partId, segmentIndex = segmentIndex,
                localPos = localPos, localRot = localRot, enqueuedAt = Time.unscaledTime
            });
        }

        // Does nothing while the queue is empty. The part only exists on a client once a verified frame
        // containing it arrives, so we retry until it does. Only the front is tried so every peer
        // wires parts in the same order.
        private void Update()
        {
            if (pending.Count == 0) return;

            PendingPart d = pending.Peek();
            if (Time.unscaledTime - d.enqueuedAt > 10f)
            {
                Debug.LogWarning($"[Attach] gave up waiting for part {d.id} to appear on this peer");
                pending.Dequeue();
                return;
            }

            if (TryWire(d)) pending.Dequeue();
        }

        private bool TryWire(PendingPart d)
        {
            var p = GetComponent<Player>();
            if (p.wormBodySegments.Count < p.WormSegmentCount) return false;                        
            if (!p.predictionManager.hierarchy.TryGetGameObject(d.id, out GameObject part)) return false;   
            if (!PartRegistry.TryGet(d.partId, out CreaturePartData data)) return true;                     

            Transform seg = d.segmentIndex < 0 ? p.wormHead : p.wormBodySegments[d.segmentIndex];
            Rigidbody segRb = seg.GetComponent<Rigidbody>();
            var ap = part.GetComponent<AttachablePart>();
            var drag = part.GetComponent<PartDragging>();
            var rb = part.GetComponent<Rigidbody>();
            if (ap == null || rb == null)
            {
                Debug.LogError($"[Attach] {part.name} needs AttachablePart and a Rigidbody");
                return true;
            }

            // same relative pose on every peer before the joint is created
            part.transform.position = seg.TransformPoint(d.localPos);
            part.transform.rotation = seg.rotation * d.localRot;
            Physics.SyncTransforms();

            ap.attachedSegmentRigidbody = segRb;
            ap.attachmentPosition = part.transform.position;
            ap.attachmentRotation = part.transform.rotation;
            ap.SetLocalOffsets(d.localPos, d.localRot);

            p.attachedWormParts.Add(part);

            if (drag != null) { drag.DeselectPart(); drag.enabled = false; }

            rb.isKinematic = false;
            rb.useGravity = true;
            ap.ConfigureRigidBody(rb, data.mass);

            LegPart leg = part.GetComponent<LegPart>();
            if (leg != null)
            {
                p.MaxVelocity += GameParameters.LegMaxVelocityIncrease;
                leg.enabled = true;
            }

            RefreshLegOrder(p);

            Transform endPoint = drag != null ? drag.endPoint : null;
            if (endPoint == null) return true;

            ap.ConfigureHingeJoint(endPoint);                       // every peer, not only the owner
            p.GetComponent<WormPhysics>().IgnorePartCollisionWithWorm(part, segRb.transform);
            return true;
        }

        private void RefreshLegOrder(Player p)
        {
            var legParts = new List<LegPart>();
            foreach (GameObject part in p.attachedWormParts)
            {
                if (part == null) continue;
                LegPart leg = part.GetComponent<LegPart>();
                if (leg != null) legParts.Add(leg);
            }

            float totalTime = GameParameters.LegMoveTime;
            for (int i = 0; i < legParts.Count; i++)
                legParts[i].timeOffset = i * (totalTime / legParts.Count);
        }

        #endregion

        #region Removing / clearing

        // Same name and signature as before so existing callers keep working.
        public void DestroyPart(GameObject part)
        {
            if (part == null) return;

            var p = GetComponent<Player>();
            p.attachedWormParts.Remove(part);
            if (p.predictionManager.hierarchy.TryGetId(part, out PredictedObjectID id))
                DeletePartServerSide(id);        // a predicted object: delete through the hierarchy
            else
                Destroy(part);                   // a local builder copy
        }

        [ServerRpc]
        private void DeletePartServerSide(PredictedObjectID id)
        {
            GetComponent<Player>().predictionManager.hierarchy.Delete(id);
        }

        [ServerRpc]
        public void ClearAttachedParts()
        {
            ClearAttachedPartsObserverSide();
        }

        [ObserversRpc]
        private void ClearAttachedPartsObserverSide()
        {
            GetComponent<Player>().attachedWormParts.Clear();
        }

        #endregion

        public IEnumerator ReactivateAttachedParts()
        {
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            foreach (GameObject attachedPart in GetComponent<Player>().attachedWormParts)
            {
                if (attachedPart == null) continue;

                Debug.Log($"ReactivateAttachedParts: part instanceID={attachedPart.GetInstanceID()} name={attachedPart.name}");
                attachedPart.SetActive(true);

                // every peer owns a joint for this part now, so every peer resets it
                yield return StartCoroutine(attachedPart.GetComponent<AttachablePart>().ResetJoint());
            }

            GetComponent<WormPhysics>().IgnoreWormSelfCollision();
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