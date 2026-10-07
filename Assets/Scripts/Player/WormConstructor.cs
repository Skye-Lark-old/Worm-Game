using System.Collections.Generic;
using CreatureParts;
using GameLoop.multiplayer;
using PurrNet;
using PurrNet.Pooling;
using PurrNet.Prediction;
using UnityEngine;

namespace Player
{
    public class WormConstructor: NetworkBehaviour
    {
        private Player player;

        void Awake()
        {
            player = GetComponent<Player>();
            Debug.Log($"player: {player}");
        }
        
        public bool CreateWormSegments(DisposableList<PredictedObjectID> ids)
        {
            if (player == null) player = GetComponent<Player>();

            for (int i = 0; i < player.WormSegmentCount; i++)
            {
                Vector3 segmentPosition = player.wormHead.position + -player.wormHead.forward * (player.MaxPartDistance * (i + 1));
                var segmentID = player.predictionManager.hierarchy.Create(player.wormSegmentPrefab, segmentPosition, player.wormHead.rotation, player.owner);
                if (segmentID == null)
                {
                    Debug.LogError("Worm segment create failed (is the prefab registered?)");
                    return false;
                }
                ids.Add(segmentID.Value);
            }
            return true;
        }

        // Runs once per peer; returns false until every segment object can be resolved
        public bool TryWireWorm(DisposableList<PredictedObjectID> ids)
        {
            if (player == null) player = GetComponent<Player>();
            if (ids.Count < player.WormSegmentCount) return false;

            var segments = new List<GameObject>(ids.Count);
            for (int i = 0; i < ids.Count; i++)
            {
                GameObject go = player.predictionManager.hierarchy.GetGameObject(ids[i]);
                if (go == null) return false;
                segments.Add(go);
            }

            // --- your original wiring, unchanged ---
            player.wormBodySegments.Clear();
            CreaturePart previousSegment = player.wormHead.GetComponent<CreaturePart>();

            for (int i = 0; i < segments.Count; i++)
            {
                GameObject newSegment = segments[i];
                newSegment.transform.SetParent(transform, true);
                newSegment.name = "Worm segment " + i;
                newSegment.GetComponent<CreatureBodySegment>().previousSegment = previousSegment;
                player.wormBodySegments.Add(newSegment.transform);
                previousSegment = newSegment.GetComponent<CreatureBodySegment>();
            }

            for (int i = 0; i < player.wormBodySegments.Count - 1; i++)
            {
                player.wormBodySegments[i].GetComponent<CreatureBodySegment>().nextSegment =
                    player.wormBodySegments[i + 1].GetComponent<CreatureBodySegment>();
            }

            GetComponent<WormPhysics>().AddCollidersToSegments();
            AddSegmentJoints();                                   // your original method

            var wp = GetComponent<WormPhysics>();
            if (player.isOwner) wp.ToggleWormKinematics(true);
            else { wp.ToggleWormCollisions(true); wp.ToggleWormKinematics(false); }

            return true;
        }

        public void ConstructWorm()
        {
            if (player == null) player = GetComponent<Player>();
            
            Vector3 currentPos = player.wormHead.position;
            Vector3 backDir = -player.wormHead.forward;

            //Debug.Log($"Construct worm called on {player.PlayerName}");
            
            for (int i = 0; i < player.wormBodySegments.Count; i++)
            {
                currentPos += backDir * player.MaxPartDistance;
                Transform segment = player.wormBodySegments[i];
                segment.position = currentPos;
                segment.rotation = player.wormHead.rotation;
                //Debug.Log($"positioning segment {segment}");
            }
        }

        [ServerRpc]
        public void AddSegmentJointsAsServer(Player playerToSetJoints)
        {
            AddSegmentJointsServerRpc(playerToSetJoints);
        }

        [ObserversRpc]
        public void AddSegmentJointsServerRpc(Player playerToSetJoints)
        {
            if (player == null) player = GetComponent<Player>();

            if (player != playerToSetJoints) return;
            
            AddSegmentJoints();
        }

        public void AddSegmentJoints()
        {
            Rigidbody previousRb = player.wormHead.GetComponent<Rigidbody>();
            
            for (int i = 0; i < player.wormBodySegments.Count; i++)
            {
                Transform segment = player.wormBodySegments[i];
                
                previousRb = segment.GetComponent<CreatureBodySegment>().AddJoint(segment, previousRb);
            }
        }
    }
    
    
}