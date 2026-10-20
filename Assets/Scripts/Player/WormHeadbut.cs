using System.Collections.Generic;
using CreatureParts;
using PurrNet.Prediction;
using UnityEngine;

namespace Player
{
    public class WormHeadBut : MonoBehaviour
    {
        #region Private Variables

        private List<Transform> wormParts;
        private PredictedRigidbody wormHead;
        private Player player;
        private Vector3 Look => player.wormForwardMovement.lookDirection;
        private float Dt => player.wormForwardMovement.TickDelta > 0f
            ? player.wormForwardMovement.TickDelta : Time.fixedDeltaTime;

        // cached once the worm is wired
        private readonly List<PredictedRigidbody> partRbs = new List<PredictedRigidbody>();
        private readonly List<CreatureBodySegment> partSegments = new List<CreatureBodySegment>();

        #endregion

        #region Built-In Methods

        private void Awake()
        {
            player = GetComponent<Player>();
            wormParts = player.wormBodySegments;
            wormHead = player.wormHead.GetComponent<PredictedRigidbody>();
        }

        #endregion

        #region Public Methods

        // Called by Player after the segments are (re)wired
        public void CacheParts()
        {
            partRbs.Clear();
            partSegments.Clear();
            foreach (Transform part in wormParts)
            {
                partRbs.Add(part.GetComponent<PredictedRigidbody>());
                partSegments.Add(part.GetComponent<CreatureBodySegment>());
            }
        }

        public void ReadyHeadbut()
        {
            if (partRbs.Count != wormParts.Count) CacheParts();

            int segmentCount = GameParameters.WormSegmentCount + 1; // head
            int liftedSegment = 0;

            if (!player.IsWormGrounded)
            {
                return;
            }

            //TODO: fix lifting logic
            LiftFrontSegments(wormHead, segmentCount, segmentCount);

            for (int i = 0; i < partRbs.Count; i++)
            {
                if (i > segmentCount / 2)
                {
                    GroundBackSegment(partRbs[i], partSegments[i]);
                }
                else
                {
                    liftedSegment++;
                    LiftFrontSegments(partRbs[i], liftedSegment, segmentCount);
                }
            }

            MoveHead();
        }

        public void EndHeadBut()
        {
            if (partRbs.Count != wormParts.Count) CacheParts();

            SnapHeadRotation();
            wormHead.AddForce(wormHead.transform.forward * GameParameters.WormHeadButHeadForce);
            for (int i = 0; i < partRbs.Count; i++)
            {
                if (i < ((GameParameters.WormSegmentCount + 1) / 2))
                {
                    partRbs[i].AddForce(wormHead.transform.forward * (GameParameters.WormHeadButForce / (i + 1)));
                }
                else
                {
                    GroundBackSegment(partRbs[i], partSegments[i]);
                }
            }
        }

        public void WormheadbutCoolDown()
        {
            if (partRbs.Count != wormParts.Count) CacheParts();

            for (int i = 0; i < partRbs.Count; i++)
            {
                if (i > ((GameParameters.WormSegmentCount + 1) / 2))
                {
                    GroundBackSegment(partRbs[i], partSegments[i]);
                }
            }
        }

        #endregion

        #region Private Methods

        private void LiftFrontSegments(PredictedRigidbody wormPart, int liftedSegment, int segmentCount)
        {
            float maxSegmentHeight = GameParameters.WormMaxHeightPerSegment / liftedSegment;

            if (wormPart.position.y < maxSegmentHeight)
            {
                float forceMultiplier = liftedSegment / (segmentCount / 2); //Do not update to a float, it breaks
                wormPart.AddForce((Vector3.up + (wormPart.transform.forward * GameParameters.WormHeadButForwardPercent)) * (GameParameters.WormHeadButLiftingForce * forceMultiplier));
            }
        }

        private void GroundBackSegment(PredictedRigidbody wormPart, CreatureBodySegment segment)
        {
            if (segment.IsGrounded)
            {
                Vector3 groundNormal = segment.GroundNormal;
                Vector3 velocity = wormPart.linearVelocity;

                // Remove velocity component moving away from ground
                float normalVelocity = Vector3.Dot(velocity, groundNormal);
                if (normalVelocity > 0)
                {
                    wormPart.linearVelocity = velocity - groundNormal * normalVelocity;
                }

                // Optional: gentle downward force
                wormPart.AddForce(-groundNormal * GameParameters.WormHeadbutGroundingForce);
            }
        }

        private void MoveHead()
        {
            RotateHeadUngrounded(GameParameters.WormHeadRotationSpeedWhileAttacking);
            wormHead.AddForce(GameParameters.WormMoveForce * wormHead.transform.forward);
        }

        private void RotateHeadUngrounded(float speed)
        {
            Vector3 camDirFlat = Flatten(Look);
            if (camDirFlat.sqrMagnitude < 0.01f) return;
            Quaternion targetYaw = Quaternion.LookRotation(camDirFlat);
            wormHead.rotation = Quaternion.Slerp(wormHead.rotation, targetYaw, speed * Dt);
        }

        private static Vector3 Flatten(Vector3 v)
        {
            v.y = 0f;
            return v.normalized;
        }

        private void SnapHeadRotation()
        {
            Vector3 camDir = Look;
            if (camDir.sqrMagnitude < 0.01f) return;
            camDir.Normalize();

            float pitch = CalculatePitch(camDir);

            // Clamp pitch to the allowed range
            pitch = Mathf.Clamp(pitch, -GameParameters.WormheadButMaxHeadVerticleAngle, GameParameters.WormheadButMaxHeadVerticleAngle);

            // Keep current yaw, only apply new pitch
            Vector3 currentForward = wormHead.transform.forward;
            Vector3 currentForwardFlat = Flatten(currentForward);
            Quaternion currentYaw = Quaternion.LookRotation(currentForwardFlat);

            Quaternion pitchRot = Quaternion.AngleAxis(-pitch, currentYaw * Vector3.right);
            wormHead.rotation = pitchRot * currentYaw;
        }

        private float CalculatePitch(Vector3 camDir)
        {
            Vector3 camRight = Vector3.Cross(Vector3.up, camDir).normalized;
            float camPitch = Vector3.SignedAngle(Vector3.ProjectOnPlane(camDir, Vector3.up), camDir, camRight);
            float normalized = Mathf.InverseLerp(GameParameters.MinCameraPitch, GameParameters.MaxCameraPitch, camPitch);
            normalized = 1f - Mathf.Clamp01(normalized);

            return Mathf.Lerp(-GameParameters.VisualHeadMaxDegrees, GameParameters.VisualHeadMaxDegrees, normalized);
        }

        #endregion
    }
}