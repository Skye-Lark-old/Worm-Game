using System;
using System.Collections;
using System.Linq;
using Player;
using PurrNet;
using PurrNet.Prediction;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CreatureParts
{
    public class CreaturePart : PredictedIdentity<CreaturePart.PartState>
    {

        public struct PartState : IPredictedData<PartState>
        {
            public float TimeSinceLastGrounded;
            public float ScrunchTimeLeft;
            public void Dispose() {}
        }
        
        #region Public Variables
        [Header("Public Variables")]
        
        public bool IsGrounded { get; private set; }
        public GameObject GroundObject { get; private set; }
        public Vector3 GroundNormal { get; private set; }
        public float TimeSinceLastGrounded { get; private set; }

        public Player.Player ParentPlayer
        {
            get
            {
                if (parentPlayer == null)
                {
                    parentPlayer = GetComponentInParent<Player.Player>();
                    if (parentPlayer == null) throw new Exception($"Could not get Parent Player of Creature Part {gameObject}");
                } 
                return parentPlayer;
            }
        }

        private Player.Player parentPlayer;

        #endregion

        #region Private Variables
        [Header("Private Variables")]
        
        private Collider partCollider;
    
        private readonly Collider[] results = new Collider[GameParameters.GroundColliderMaxHeldCollisions];
        private readonly float verticalDetectionOffset = GameParameters.GroundingColliderVerticalDetectionOffset;
        private readonly float detectionRadiusScale = GameParameters.GroundColliderDetectionRadiusScale;

        #endregion

        #region Built-In Methods
        
        protected void Awake()
        {
            partCollider = GetComponent<Collider>();
            GroundObject = null;
        }

        protected override void Simulate(ref PartState state, float delta)
        {
            if (state.ScrunchTimeLeft > 0f) state.ScrunchTimeLeft -= delta;
        }

        #endregion
        
        #region Public Methods

        public virtual void Jump()
        {
        
        }

        public virtual void MoveForward()
        {
        
        }
        
        public void RefreshGrounding(float delta)
        {
            ref var s = ref currentState;
            Vector3 bottom = partCollider.bounds.center - new Vector3(0, partCollider.bounds.extents.y, 0);
            Vector3 checkPos = bottom + Vector3.down * verticalDetectionOffset;
            float radius = partCollider.bounds.extents.x * detectionRadiusScale;

            int hitCount = Physics.OverlapSphereNonAlloc(checkPos, radius, results, ~0, QueryTriggerInteraction.Ignore);

            IsGrounded = false;
            GroundObject = null;
            GroundNormal = Vector3.up;

            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = results[i];
                if (hit.transform.root != transform.root)
                {
                    IsGrounded = true;
                    GroundObject = hit.gameObject;
                    GroundNormal = GetGroundNormal(hit, delta, ref s);
                    break;
                }
            }
        }
        
        #endregion
        
        #region Private Methods
        
        private Vector3 GetGroundNormal(Collider groundCollider, float delta, ref PartState s)
        {
            if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 2f) && hit.collider == groundCollider)
            {
                s.TimeSinceLastGrounded = 0f;
                return hit.normal;
            }
            s.TimeSinceLastGrounded += delta;
            return Vector3.up;
        }
        
        private void OnCollisionEnter(Collision other)
        {
            if (LocalPlayer.Instance == null) return;

            if (!isOwner) return; 
            
            if ((other.gameObject.GetComponent<CreaturePart>() != null))
            {
                if (other.gameObject.GetComponent<CreaturePart>().owner == owner)
                {
                    return;
                }
            }

            LocalPlayer.Instance.DamagePlayer(other, gameObject);
        }
        
        #endregion
    }
}