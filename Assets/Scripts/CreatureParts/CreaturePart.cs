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
            public float timeSinceLastGrounded;
            public float scrunchTimeLeft;
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
            if (state.scrunchTimeLeft > 0f) state.scrunchTimeLeft -= delta;
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

        private Vector3 GetGroundNormal(Collider groundCollider, float delta, ref PartState s)
        {
            if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 2f) && hit.collider == groundCollider)
            {
                s.timeSinceLastGrounded = 0f;
                return hit.normal;
            }
            s.timeSinceLastGrounded += delta;
            return Vector3.up;
        }
        
        protected override void LateAwake()
        {
            // //need to set owner here?
            // if (owner != null)
            // {
            //     //Debug.Log($"Part owner is not null, owner: {owner}");
            //     return;
            // }
            //
            // if (ParentPlayer == null)
            // {
            //     Debug.Log($"Parent player is null, owner: {owner}");
            //     return;
            // }
            //
            // if (ParentPlayer.owner.HasValue)
            // {
            //     Debug.Log($"giving ownership to parentPlayer owner. Old owner: {owner}" );
            //     //owner = (ParentPlayer.owner.Value);
            // }
            // else
            // {
            //     Debug.Log($"waiting for ownership parent to give ownership. Old owner: {owner}" );
            //     StartCoroutine(WaitForParentAndClaimOwnership());
            // }
        }
        
        // protected override void OnSpawned(bool asServer)
        // {
        //     if (!asServer) return;
        //     
        //     if (owner != null)
        //     {
        //         //Debug.Log($"Part owner is not null, owner: {owner}");
        //         return;
        //     }
        //
        //     if (ParentPlayer == null)
        //     {
        //         Debug.Log($"Parent player is null, owner: {owner}");
        //         return;
        //     }
        //
        //     if (ParentPlayer.owner.HasValue)
        //     {
        //         Debug.Log($"giving ownership to parentPlayer owner. Old owner: {owner} new owner: {parentPlayer.owner}" );
        //         GiveOwnership(ParentPlayer.owner.Value);
        //     }
        //     else
        //     {
        //         Debug.Log($"waiting for ownership parent to give ownership. Old owner: {owner}" );
        //         StartCoroutine(WaitForParentAndClaimOwnership());
        //     }
        // }
        
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
                else
                {
                    Player.Player thisPlayer = gameObject.GetComponentInParent<Player.Player>();
                    Player.Player otherPlayer = other.gameObject.GetComponentInParent<Player.Player>();
                    //Debug.Log("collision with other player. This player: " + thisPlayer.PlayerName + " " + gameObject.name + " Other player: " + otherPlayer.PlayerName + " " + other.gameObject.name + " force: " + other.impulse.magnitude);
                }
            }

            LocalPlayer.Instance.DamagePlayer(other, gameObject);
        }

        // protected override void OnOwnerChanged(PlayerID? previousOwner, PlayerID? newOwner, bool asServer)
        // {
        //     //Debug.Log($"[WormSegment] OnOwnerChanged '{gameObject.name}' | asServer={asServer} | prev={previousOwner} | new={newOwner}");
        // }

        #endregion
        
        #region Public Methods

        public virtual void Jump()
        {
        
        }

        public virtual void MoveForward()
        {
        
        }
        
        public void SetMaterial(Material material)
        {
            GetComponent<MeshRenderer>().material = material;
        }
        
        #endregion
        
        #region Private Methods

        // private void CheckGrounded(float delta)
        // {
        //     Vector3 bottom = partCollider.bounds.center - new Vector3(0, partCollider.bounds.extents.y, 0);
        //     Vector3 checkPos = bottom + Vector3.down * verticalDetectionOffset;
        //     float radius = partCollider.bounds.extents.x * detectionRadiusScale;
        //
        //     int hitCount = Physics.OverlapSphereNonAlloc(checkPos, radius, results, ~0, QueryTriggerInteraction.Ignore);
        //
        //     IsGrounded = false;
        //     GroundObject = null;
        //     GroundNormal = Vector3.up;
        //
        //     for (int i = 0; i < hitCount; i++)
        //     {
        //         Collider hit = results[i];
        //         if (hit.transform.root != transform.root)
        //         {
        //             IsGrounded = true;
        //             GroundObject = hit.gameObject;
        //             GroundNormal = GetGroundNormal(hit, delta);
        //         
        //             break;
        //         }
        //     }
        // }
        //
        // private Vector3 GetGroundNormal(Collider groundCollider, float delta)
        // {
        //     if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 2f))
        //     {
        //         if (hit.collider == groundCollider)
        //         {
        //             TimeSinceLastGrounded = 0f;
        //             return hit.normal;
        //         }
        //     }
        //
        //     TimeSinceLastGrounded += delta;
        //     return Vector3.up;
        // }
        
        private IEnumerator WaitForParentAndClaimOwnership()
        {
            PlayerID? parentOwner = null;

            float elapsed = 0f;
            while (elapsed < 3f)
            {
                if (ParentPlayer != null && ParentPlayer.owner.HasValue)
                {
                    parentOwner = ParentPlayer.owner;
                    break;
                }

                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }

            if (parentOwner == null)
            {
                yield break;
            }

            if (owner == null)
            {
                Debug.LogWarning("Could not properly give ownership because of switch to purrdiction");
                //GiveOwnership(parentOwner.Value);
            }
        }
        
        #endregion
    }
}