using System.Collections;
using GameLoop.multiplayer;
using PurrNet;
using UnityEngine;

namespace CreatureParts
{
    public class LegPart : AttachablePart
    {
        
        //TODO: refactor to have better code and inverse kinematics
        
        public GameObject foot;

        private bool wasGroundedLastFrame = false;
        private bool canStep = true;
        public float timeOffset = 0.1f;

        private bool isMoving;
        private Coroutine movementCoroutine;

        [Header("Leg Walking Behavior")]
        public float liftHeight = 0.15f;
        public float swingSpeed = 8f;
        public float forwardSwingDistance = 0.12f;

        [Tooltip("Speed at which the leg rotates to align with the ground")]
        public float rotationSpeed = 10f;

        [Tooltip("Speed at which the foot rotates to align with the ground")]
        public float footRotationSpeed = 150f;

        // Smoothing variables
        private Vector3 smoothedLocalPosition;
        private float smoothedScaleY;
        private bool isInitialized = false;

        void Start()
        {
            smoothedLocalPosition = transform.localPosition;
            smoothedScaleY = transform.localScale.y;
            isInitialized = true;
        }

        protected override void Simulate(ref PartState state, float delta)
        {
            base.Simulate(ref state, delta);

            if (!isInitialized)
            {
                smoothedLocalPosition = transform.localPosition;
                smoothedScaleY = transform.localScale.y;
                isInitialized = true;
            }

            // --- HANDLE GAIT DELAY ---
            if (IsGrounded && !wasGroundedLastFrame)
            {
                canStep = false;
                StartCoroutine(GaitDelayCoroutine());
            }

            if (!IsGrounded)
            {
                canStep = true;
            }

            wasGroundedLastFrame = IsGrounded;

            // --- LEG ROTATION ---
            if (IsGrounded && GroundObject != null)
            {
                Vector3 groundNormal = GetGroundNormal();

                Quaternion legTargetRotation =
                    Quaternion.FromToRotation(transform.up, groundNormal) * transform.rotation;

                float maxRotation = rotationSpeed * Time.fixedDeltaTime * 180f;
                transform.rotation = Quaternion.RotateTowards(transform.rotation, legTargetRotation, maxRotation);

                if (foot != null)
                {
                    Quaternion footTargetRotation =
                        Quaternion.FromToRotation(foot.transform.up, groundNormal) * foot.transform.localRotation;

                    foot.transform.localRotation = Quaternion.RotateTowards(
                        foot.transform.localRotation,
                        footTargetRotation,
                        footRotationSpeed * Time.fixedDeltaTime * 180f
                    );
                }
            }

            // --- LEG SWING / AIR MOVEMENT ---
            if (isMoving && !IsGrounded && canStep)
            {
                float stepProgress = Mathf.Clamp01(stepTimer / GameParameters.LegMoveTime);
                float verticalOffset = Mathf.Sin(stepProgress * Mathf.PI) * liftHeight;

                Vector3 swingTarget =
                    smoothedLocalPosition +
                    Player.LocalPlayer.Instance.wormVisualHead.forward.normalized * forwardSwingDistance * stepProgress;

                Vector3 targetPos = swingTarget + Vector3.up * verticalOffset;
                
                // Smooth the position update
                transform.localPosition = Vector3.Lerp(
                    transform.localPosition,
                    targetPos,
                    swingSpeed * Time.fixedDeltaTime
                );

                smoothedScaleY = Mathf.Lerp(smoothedScaleY, 0.2f, 15f * Time.fixedDeltaTime);
            }
            else
            {
                smoothedScaleY = Mathf.Lerp(smoothedScaleY, 0.3f, 15f * Time.fixedDeltaTime);
                // Update smoothed position when grounded
                if (IsGrounded)
                {
                    smoothedLocalPosition = transform.localPosition;
                }
            }

            transform.localScale = new Vector3(transform.localScale.x, smoothedScaleY, transform.localScale.z);

            // --- FOOT ROTATION RESET ---
            if (IsGrounded && foot != null)
            {
                foot.transform.localRotation = Quaternion.Slerp(
                    foot.transform.localRotation,
                    Quaternion.identity,
                    10f * Time.fixedDeltaTime
                );
            }

            // --- RESET MOVING FLAG ---
            if (canStep)
                isMoving = false;
        }
        
        private void OnEnable()
        {
            movementCoroutine = null;
            isMoving = false;
            canStep = true;
            stepTimer = 0f;
        }

        private float stepTimer = 0f;

        private IEnumerator GaitDelayCoroutine()
        {
            yield return new WaitForSeconds(timeOffset);
            canStep = true;
        }

        private Vector3 GetGroundNormal()
        {
            Vector3 groundNormal = Vector3.up;

            Collider groundCollider = GroundObject.GetComponent<Collider>();
            if (groundCollider != null)
            {
                Ray ray = new Ray(transform.position, -transform.up);
                if (groundCollider.Raycast(ray, out RaycastHit hit, 1f))
                {
                    groundNormal = hit.normal;
                }
            }
            return groundNormal;
        }

        public override void MoveForward()
        {
            if (!IsGrounded || !canStep) return;

            isMoving = true;

            if (movementCoroutine == null && GroundObject != null)
            {
                Vector3 groundNormal = GetGroundNormal();
                
                // Get the perpendicular direction from ground (straight up from foot)
                Vector3 groundUpward = groundNormal;
                
                // Get the direction from ground to upper leg
                Vector3 groundToLeg = (transform.position - foot.transform.position).normalized;
                
                // Project the ground-to-leg direction onto the plane perpendicular to ground normal
                Vector3 legAngleOnGround = Vector3.ProjectOnPlane(groundToLeg, groundNormal).normalized;
                
                // Combine upward and angle to create improved ground tangent
                Vector3 groundTangent = (groundUpward * 0.5f + legAngleOnGround * 0.2f).normalized;
                
                Vector3 legForward = transform.forward;
                Vector3 headForward = Player.LocalPlayer.Instance.wormVisualHead.forward;

                float groundTangentWeight = 0.2f;
                float legForwardWeight = 0.3f;
                float headForwardWeight = 0.5f;

                Vector3 moveDirection =
                    (groundTangent * groundTangentWeight +
                     legForward * legForwardWeight +
                     headForward * headForwardWeight).normalized;

                Vector3 stepUp = transform.up * liftHeight;
                Vector3 arcDirection = (moveDirection + stepUp).normalized;

                Rigidbody rb = GroundObject.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.AddForceAtPosition(-GameParameters.LegMoveForce * (-arcDirection), transform.position);
                }
                else
                {
                    GetComponent<NetworkedPhysicsObject>().AddForce(-GameParameters.LegMoveForce * (-arcDirection));
                }

                stepTimer = 0f;
                movementCoroutine = StartCoroutine(StepRoutine(arcDirection));
            }

            smoothedScaleY = 0.3f;
        }

        private IEnumerator StepRoutine(Vector3 moveDirection)
        {
            NetworkedPhysicsObject rb = GetComponent<NetworkedPhysicsObject>();

            float liftTime = 0.08f;
            float pushTime = GameParameters.LegMoveTime;

            float totalTime = liftTime + pushTime;

            while (stepTimer < totalTime)
            {
                stepTimer += Time.fixedDeltaTime;
                float stepProgress = Mathf.Clamp01(stepTimer / totalTime);

                // Lift and push multipliers
                float liftMultiplier = Mathf.Sin(stepProgress * Mathf.PI);
                float pushMultiplier = Mathf.Sin(stepProgress * Mathf.PI * 0.5f);

                rb.AddForce(transform.up * GameParameters.LegMoveForce * 0.03f * liftMultiplier);
                rb.AddForce(moveDirection * GameParameters.LegMoveForce * 0.03f * pushMultiplier);

                yield return new WaitForFixedUpdate();
            }

            movementCoroutine = null;
        }

        public override void Jump()
        {
            if (IsGrounded)
            {
                Vector3 jumpDirection =
                    Vector3.Slerp(transform.up, Vector3.up, GameParameters.WormJumpAngle).normalized;

                GetComponent<NetworkedPhysicsObject>().AddForce(jumpDirection * GameParameters.LegJumpForce);
            }
        }
    }
}