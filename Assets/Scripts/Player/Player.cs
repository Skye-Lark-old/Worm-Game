using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CreatureBuilder;
using CreatureParts;
using DG.Tweening;
using PurrNet;
using PurrNet.Pooling;
using PurrNet.Prediction;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;
using WormLeague;

namespace Player
{
    public enum WormState
    {
        Idle,
        Moving,
        Jumping,
        Attacking,
        AttackCooldown,
        Dead
    }

    public struct HitInfo
    {
        public float damage;
        public Vector3 contactPoint;
        public Vector3 direction;
    }

    public class Player : PredictedIdentity<Player.Input, Player.State>
    {
        
        #region Purrdiction
        
        private int ticksThisFrame, dbgFrames;

        protected override void Simulate(Input input, ref State state, float delta)
        {
            ticksThisFrame++;
            
            if (!state.wormBuilt && wormConstructor.CreateWormSegments(state.segmentIds))
                state.wormBuilt = true;                          

            if (!wormWired && wormConstructor.TryWireWorm(state.segmentIds))
            {
                wormWired = true;
                playerSpawning.OnWormWired();
            }

            if (!isPlayerActive) return;
            
            //if (Time.frameCount % 60 == 0) Debug.Log($"tick delta {delta:F4} vs fixed {Time.fixedDeltaTime:F4}");
            SetWormGrounding();
            inSimulate = true;
            
            wormForwardMovement.TickDelta = delta;
            wormForwardMovement.lookDirection = input.lookForward;
            wormForwardMovement.movementPhase = state.movementPhase;
            
            if (input.die && !state.isDead)
            {
                state.isDead = true;

                wormHeadCopy = DuplicatePartForDeath(wormHead.gameObject, headPrefab);
                foreach (Transform bodySegment in wormBodySegments)
                    DuplicatePartForDeath(bodySegment.gameObject, wormSegmentPrefab);
                foreach (GameObject attachedPart in attachedWormParts)
                    DuplicatePartForDeath(attachedPart, attachedPart.GetComponent<PartDragging>().partData.prefab);

                GetComponent<WormPhysics>().ToggleWormCollisions(false);
                GetComponent<WormPhysics>().ToggleWormKinematics(true);
            }
            else if (input.respawn)
            {
                state.isDead = false;
                playerSpawning.TeleportWorm(input.respawnPosition, input.respawnRotation);
                GetComponent<WormPhysics>().ToggleWormCollisions(true);
                GetComponent<WormPhysics>().ToggleWormKinematics(false);
            }

            if (state.isDead)
            {
                CurrentState = WormState.Dead;
                inSimulate = false;         
                return;
            }
            if (CurrentState == WormState.Dead) CurrentState = WormState.Idle;

            if (IsWormAttacking)
            {
                wormHeadBut.ReadyHeadbut();
                attackTimer -= delta;
                if (attackTimer <= 0f)
                {
                    CurrentState = WormState.AttackCooldown;
                    attackTimer = GameParameters.WormHeadButCoolDown;
                    wormHeadBut.EndHeadBut();
                    OnWormHeadbutLaunch?.Invoke();
                }
            }
            else if (IsWormInAttackCooldown)
            {
                wormHeadBut.WormheadbutCoolDown();
                attackTimer -= delta;
                if (attackTimer <= 0f) CurrentState = WormState.Idle;
            }

            if (CurrentState != WormState.Dead)
            {
                if (input.attack) Attack();
                if (input.jump) Jump();
                if (input.moveForward && CurrentState == WormState.Idle) StartWormMoving();
                else if (!input.moveForward && CurrentState == WormState.Moving) StopWormMoving();
                if (input.moveForward) MoveForward();
            }

            state.movementPhase = wormForwardMovement.movementPhase;
            inSimulate = false;
        }

        public struct State : IPredictedData<State>
        {
            public float movementPhase;
            public bool isDead;
            public bool wormBuilt;
            public DisposableList<PredictedObjectID> segmentIds;
            public void Dispose() {}
        }
        
        protected override State GetInitialState() => new State
        {
            segmentIds = DisposableList<PredictedObjectID>.Create()
        };

        public struct Input : IPredictedData
        { 
            public bool moveForward, jump, attack;
            public Vector3 lookForward;
            public bool die, respawn;
            public Vector3 respawnPosition;
            public Quaternion respawnRotation;
            public void Dispose() {}
        }
        
        protected override void UpdateInput(ref Input input)
        {
            input.moveForward = wantsMove;
            input.jump   |= wantsJump;    wantsJump = false;
            input.attack |= wantsAttack;  wantsAttack = false;
            
            if (thirdPersonCamera != null) input.lookForward = thirdPersonCamera.transform.forward;
            
            input.die |= wantsDie;  wantsDie = false;
            if (wantsRespawn)
            {
                input.respawn = true;
                input.respawnPosition = respawnPos;
                input.respawnRotation = respawnRot;
                wantsRespawn = false;
            }
        }
        
        private void LateUpdate()
        {
            if (isOwner && dbgFrames++ < 20) Debug.Log($"ticks this frame: {ticksThisFrame}");
            ticksThisFrame = 0;
        }
        
        public void RequestRespawn(Vector3 position, Quaternion rotation)
        {
            wantsRespawn = true; respawnPos = position; respawnRot = rotation; deathRequested = false;
        }
        
        protected override void UpdateView(State viewState, State? verified)
        {
            if (viewState.isDead == viewWasDead) return;
            viewWasDead = viewState.isDead;

            if (viewState.isDead) { OnWormDeath?.Invoke(); HandleDeathObservers(); }
            else playerSpawning.HandleRespawnVisuals();
        }
        
        private void OnRollbackFinished()
        {
            if (!wormWired) return;
            foreach (Transform s in wormBodySegments)     
                if (s == null) { wormWired = false; return; }
        }
        
        #endregion
        
        #region Public Properties
        [Header("Public Properties")]

        public string PlayerName = "";

        public PlayerID playerID;
        
        public WormState CurrentState { get; set; }
        
        public bool IsWormGrounded { get; set; }
        public bool IsWormGroundedBySegments { get; set; }
        public float MaxVelocity { get; set; }
        public bool IsWormJumping => CurrentState == WormState.Jumping;
        public bool IsWormAttacking => CurrentState == WormState.Attacking;
        public bool IsWormInAttackCooldown => CurrentState == WormState.AttackCooldown;
        
        public bool IsInvincible { get; set; }

        public bool canDie = false;

        public GameObject headPrefab;
        
        private bool wantsMove, wantsJump, wantsAttack, inSimulate;
        
        private bool wantsDie, wantsRespawn, deathRequested, viewWasDead;
        private Vector3 respawnPos;
        private Quaternion respawnRot;
        
        private float attackTimer;
        
        private WormConstructor wormConstructor;
        private bool wormWired, wormSetupDone; 
        
        #endregion
        
        #region public variables
        public PlayerRegister.PlayerData RegisterData => PlayerRegister.Players[playerID];
        
        public int playerScore = 1;
        public float maxPlayerHealth = GameParameters.DefaultPlayerHealth;
        public float currentPlayerHealth = GameParameters.DefaultPlayerHealth;

        public event Action<HitInfo> OnTakeDamage;

        public GameObject thirdPersonCamera;
        public CinemachineImpulseSource screenShakeImpulseSource;
        public GameObject wormSegmentPrefab;
        public Transform wormHead;
        public Transform wormVisualHead;
        public List<Transform> wormBodySegments = new( );
        public List<GameObject> attachedWormParts;
        public List<GameObject> wormPartsInInventory;

        public PlayerSpawning playerSpawning;
        public WormForwardMovement wormForwardMovement;
        
        public GameObject wormHeadCopy;
        
        public WormJump wormJump;
        public WormHeadBut wormHeadBut;
    
        public readonly int WormSegmentCount = GameParameters.WormSegmentCount;
        public readonly float MaxPartDistance = GameParameters.SegmentMaxPartDistance;

        public string playerTeam;
        
        public Material DeadBodyPartMaterial;
        
        #endregion
        
        #region Events
        
        public event Action<string> OnPlayerTeamChanged;
        public event Action OnWormMoveForwardStart;
        public event Action OnWormMoveForwardEnd;
        public event Action OnWormJump;
        
        public event Action OnWormHeadbutCharge;
        
        public event Action OnWormHeadbutLaunch;
        
        public event Action<Vector3> OnWormHeadbutHitBall;
        
        public event Action OnWormHeadbutHitPlayer;
        
        public event Action OnWormHeadbutHitShell;
        public event Action OnWormHeadbutHitOther;
        
        public event Action OnWormDeath;
        
        #endregion
        
        #region private variables

        private bool isPlayerActive = false;
        
        #endregion
    
        #region Built-In Methods
        
        
        private void FixedUpdate()
        {
            
            //Debug.Log(RegisterData.name);
            
            if (!isPlayerActive) return;
            
            //SetWormGrounding();

            if (thirdPersonCamera != null)
            {
                RotateVisualHead();
            }

            // if (IsWormAttacking)
            // {
            //     wormHeadBut.ReadyHeadbut();
            // }
            //
            // if (IsWormInAttackCooldown)
            // {
            //     wormHeadBut.WormheadbutCoolDown();
            // }

            if (isOwner)
            {
                if (currentPlayerHealth < maxPlayerHealth && CurrentState != WormState.Dead)
                {
                    currentPlayerHealth += GameParameters.PlayerHealthRegen;
                }
            }
        }

        protected override void LateAwake()
        {
            wormConstructor = GetComponent<WormConstructor>();
            playerSpawning = GetComponent<PlayerSpawning>();

            predictionManager.onRollbackFinished -= OnRollbackFinished;
            predictionManager.onRollbackFinished += OnRollbackFinished;

            playerID = owner.Value;
            
            if (isServer && PlayerRegister.Players != null && !PlayerRegister.Players.ContainsKey(playerID))
                PlayerRegister.RegisterClient(playerID, true, true);

            playerSpawning.InitialSpawn();
        }
        
        protected override void Destroyed()
        {
            if (predictionManager != null) predictionManager.onRollbackFinished -= OnRollbackFinished;
        }
        
        #endregion

        #region Public Methods
        
        public void ActivatePlayer() => isPlayerActive = true;
        public void DeactivatePlayer() => isPlayerActive = false;
        
        public void SetPlayernameFromLobby(string username, PlayerID playerID)
        {
            if (playerID == owner)
            {
                PlayerName = username;
            }
        }
        
        public void StartWormMoving()
        {
            if (!inSimulate) { wantsMove = true;  return; }
            
            if (CurrentState == WormState.Dead) return;
            CurrentState = WormState.Moving;
            
            OnWormMoveForwardStart?.Invoke();
        }
        
        public void StopWormMoving()
        {
            if (!inSimulate)
            {
                wantsMove = false;
                return;
            }

            if (CurrentState == WormState.Dead) return;
            CurrentState = WormState.Idle;
            
            OnWormMoveForwardEnd?.Invoke();
        }
        
        public void MoveForward()
        {
            if (!isPlayerActive || IsWormJumping || IsWormAttacking || IsWormInAttackCooldown || CurrentState == WormState.Dead) return;
            
            if (!inSimulate) { wantsMove = true;  return; }
            
            wormForwardMovement.MoveHead();
            wormForwardMovement.MoveWormBody();

            foreach (var part in attachedWormParts)
            {
                part.GetComponent<CreaturePart>().MoveForward();
            }
        }

        public void MoveInLobby(Vector3 movePosition)
        {
            //if (!GetComponent<PlayerSpawning>().isSetup) return;
            
            wormForwardMovement.MoveHeadTowardsPosition(movePosition);
            wormForwardMovement.MoveWormBody();
            wormForwardMovement.MoveHeadTowardsPosition(movePosition);
            wormForwardMovement.MoveWormBody();
            
        }

        public void DamagePlayer(Collision other, GameObject hitGameObject)
        {
            if (!isOwner) return;

            if (IsInvincible) return;

            if (GameParameters.IsInvincibleInGame) return;
            
            if (wormBodySegments.Any(s => s.gameObject == hitGameObject) || 
                attachedWormParts.Contains(hitGameObject))
            {
                return;
            }
            
            float collisionForce = other.impulse.magnitude;

            if (hitGameObject.GetComponent<WormHead>() != null)
            {
                if ((CurrentState == WormState.Attacking || CurrentState == WormState.AttackCooldown))
                {
                    collisionForce *= GameParameters.HeadbutDamageReductionOnHead;

                    if (other.gameObject.TryGetComponent(out Ball ball))
                    {
                        ApplyHeadbuttScreenShake(other.impulse);
                        OnWormHeadbutHitBall?.Invoke(other.GetContact(0).point);
                    }
                    else if (other.gameObject.TryGetComponent(out ShellPart shellPart) && shellPart.ParentPlayer != this)
                    {
                        ApplyHeadbuttScreenShake(other.impulse);
                        OnWormHeadbutHitShell?.Invoke();
                    }
                    else if (other.gameObject.TryGetComponent(out CreaturePart creaturePart) && creaturePart.ParentPlayer != this)
                    {
                        ApplyHeadbuttScreenShake(other.impulse);
                        OnWormHeadbutHitPlayer?.Invoke();
                    }
                    else
                    {
                        OnWormHeadbutHitOther?.Invoke();
                    }
                }
                else
                {
                    collisionForce *= GameParameters.HeadDamageMultiplier;
                }
            }
            if (other.gameObject.GetComponent<ShellPart>() != null)
            {
                collisionForce *= GameParameters.ShellDamageReduction;
            }
            Vector3 contactPoint = other.GetContact(0).point;
            Vector3 hitDirection = other.impulse.normalized;

            if (other.gameObject.GetComponent<SpikePart>() != null)
            {
                if (collisionForce > GameParameters.MinSpikeCollisionForceToDamage)
                {
                    float damage = collisionForce * GameParameters.SpikeForceToDamageMultiplier;
                    TakeDamage(new HitInfo { damage = damage, contactPoint = contactPoint, direction = hitDirection });
                }
            }
            if (other.gameObject.GetComponent<FiredProjectile>() != null)
            {
                if (other.gameObject.GetComponent<FiredProjectile>().firingPlayer == gameObject) return;
                
                if (collisionForce > GameParameters.MinProjectileCollisionForceToDamage)
                {
                    float damage = collisionForce * GameParameters.ProjectileForceToDamageMultiplier;
                    TakeDamage(new HitInfo { damage = damage, contactPoint = contactPoint, direction = hitDirection });
                }
            }
            else if (collisionForce > GameParameters.MinBluntCollisionForceToDamage)
            {
                Debug.Log($"Blunt collision between {hitGameObject.name} and {other.gameObject.name} with force: {collisionForce}", other.gameObject);
                float damage = collisionForce * GameParameters.BluntForceToDamageMultiplier;
                if (LocalPlayer.Instance == this) TakeDamage(new HitInfo { damage = damage, contactPoint = contactPoint, direction = hitDirection });
            }

            if (isOwner)
            {
                if (CurrentState != WormState.Dead && currentPlayerHealth < 0)
                {
                    OnPlayerDeath();
                }
            } 
        }

        private void ApplyHeadbuttScreenShake(Vector3 velocity)
        {
            velocity *= GameParameters.HeadButtScreenShakeMultiplier;
            //print($"{velocity.magnitude} ");
            velocity = velocity.normalized * Mathf.Min(velocity.magnitude, GameParameters.MaxHeadbuttScreenShake);
            screenShakeImpulseSource.GenerateImpulseWithVelocity(velocity);
        }

        public void Jump()
        {
            if (!inSimulate) { wantsJump = true;  return; }
            
            if (!IsWormGrounded || IsWormAttacking || IsWormInAttackCooldown || CurrentState == WormState.Dead) return;
            
            wormJump.Jump();
            foreach (var part in attachedWormParts)
            {
                part.GetComponent<CreaturePart>().Jump();
            }
            
            OnWormJump?.Invoke();
        }

        public void Attack()
        {
            if (!inSimulate)
            {
                wantsAttack = true;
                return;
            }

            if (!IsWormGrounded || IsWormAttacking || IsWormInAttackCooldown || CurrentState == WormState.Dead) return;
            
            CurrentState = WormState.Attacking;
            attackTimer = GameParameters.WormHeadbutTime;
            OnWormHeadbutCharge?.Invoke();
        }

        public void ResetPlayer()
        {
            foreach (GameObject part in attachedWormParts)
            {
                Destroy(part);
            }
            attachedWormParts.Clear();

            foreach (GameObject part in wormPartsInInventory)
            {
                Destroy(part);
            }
            
            wormPartsInInventory.Clear();
            
            CurrentState = WormState.Idle;
            DeactivatePlayer();
        }
        
        public void OnPlayerDeath()
        {
            Debug.Log("Player died");
            if (!GameSceneList.IsSceneAGameScene(SceneManager.GetActiveScene().name))
            {
                return;
            }
            
            if (CurrentState == WormState.Dead || deathRequested) return;
            
            if (CurrentState == WormState.Dead) return;
            if (!canDie)
            {
                return;
            }
            deathRequested = true;
            
            //OnWormDeath?.Invoke();
            
            CurrentState = WormState.Dead;
            if (isOwner) currentPlayerHealth = 0;

            thirdPersonCamera.GetComponent<CinemachineBrain>().enabled = false;

            if (playerSpawning.deathScreenUI != null)
            {
                playerSpawning.deathScreenUI.EnableDeathUI();
            }
            else
            {
                playerSpawning.deathScreenUI = FindFirstObjectByType<DeathScreenUI>();
            }

            IsInvincible = true;
            
            //this.GetComponent<PlayerNetwork>().RequestDeath();
            wantsDie = true;
            
            playerSpawning.TryToRespawn();
        }

        public void SetColor(Material bodyMaterial, Material headMaterial, Material deadMaterial)
        {
            wormHead.GetComponent<WormHead>().SetMaterial(headMaterial);
            DeadBodyPartMaterial = deadMaterial;

            foreach (GameObject wormSegment in wormPartsInInventory)
            {
                wormSegment.GetComponent<CreatureBodySegment>().SetMaterial(bodyMaterial);
            }
            
            GetComponent<WormRenderer>().SetMaterial(bodyMaterial);
        }
        
        public void RaiseTakeDamage(HitInfo hitInfo)
        {
            OnTakeDamage?.Invoke(hitInfo);
        }

        public void SetPlayerTeam(string team)
        {
            Debug.Log("setPlayerTeamCalled with team " + team);
            playerTeam = team;
            OnPlayerTeamChanged?.Invoke(team);
        }
        
        #endregion
        
        #region Private Methods

        private void TakeDamage(HitInfo hitInfo)
        {
            currentPlayerHealth -= hitInfo.damage;
            // Only invoke damage screenShake locally
            screenShakeImpulseSource.GenerateImpulseWithVelocity(Vector3.down * GameParameters.TakeDamageScreenShakeIntensity);
            //this.GetComponent<PlayerNetwork>().ObserversOnTakeDamage(hitInfo);
            RaiseTakeDamage(hitInfo);
        }
        
        public void HandleDeathObservers()
        {
            playerSpawning.DisableWormVisually();
    
            //wormHeadCopy = DuplicatePartForDeath(wormHead.gameObject);
            DisablePartForDeath(wormHead.gameObject);
            
            foreach (Transform bodySegment in wormBodySegments)
            {
                //DuplicatePartForDeath(bodySegment.gameObject);
                DisablePartForDeath(bodySegment.gameObject);
            }
    
            foreach (GameObject attachedPart in attachedWormParts)
            {
                //DuplicatePartForDeath(attachedPart);
                attachedPart.SetActive(false);
            }

            // if (isOwner && owner == predictionManager.localPlayer)
            // {
            //     GetComponent<WormPhysics>().ToggleWormKinematics(true);
            // }
        }

        public void DisablePartForDeath(GameObject part)
        {
            MeshRenderer meshrenderer = part.GetComponent<MeshRenderer>();
            Rigidbody rigidbody = part.GetComponent<Rigidbody>();
            Collider collider = part.GetComponent<Collider>();
            CreaturePart creaturePart = part.GetComponent<CreaturePart>();
            CreatureBodySegment creatureBodySegment = part.GetComponent<CreatureBodySegment>();
            AttachablePart attachablePart = part.GetComponent<AttachablePart>();

            if (meshrenderer != null)
            {
                meshrenderer.enabled = false;
            }

            if (collider != null)
            {
                collider.enabled = false;
            }

            if (creaturePart != null)
            {
                creaturePart.enabled = false;
            }

            if (creatureBodySegment != null)
            {
                creatureBodySegment.visualBodySegment.SetActive(false);
            }

            if (attachablePart != null)
            {
                for (int i = 0; i < part.transform.childCount; i++)
                {
                    GameObject childGameobject = part.transform.GetChild(i).gameObject;
                    if (childGameobject.GetComponent<MeshRenderer>() != null)
                    {
                        childGameobject.GetComponent<MeshRenderer>().enabled = false;
                    }
                    if (childGameobject.GetComponent<Collider>() != null)
                    {
                        childGameobject.GetComponent<Collider>().enabled = false;
                    }
                }
            }
        }
        
        public void EnablePartForRespawn(GameObject part)
        {
            MeshRenderer meshrenderer = part.GetComponent<MeshRenderer>();
            Collider collider = part.GetComponent<Collider>();
            CreaturePart creaturePart = part.GetComponent<CreaturePart>();
            CreatureBodySegment creatureBodySegment = part.GetComponent<CreatureBodySegment>();
            AttachablePart attachablePart = part.GetComponent<AttachablePart>();

            if (meshrenderer != null)
            {
                meshrenderer.enabled = true;
            }

            if (collider != null)
            {
                collider.enabled = true;
            }

            if (creaturePart != null)
            {
                creaturePart.enabled = true;
            }
            
            if (creatureBodySegment != null)
            {
                creatureBodySegment.visualBodySegment.SetActive(true);
            }
            
            if (attachablePart != null)
            {
                for (int i = 0; i < part.transform.childCount; i++)
                {
                    GameObject childGameobject = part.transform.GetChild(i).gameObject;
                    if (childGameobject.GetComponent<MeshRenderer>() != null)
                    {
                        childGameobject.GetComponent<MeshRenderer>().enabled = true;
                    }
                    if (childGameobject.GetComponent<Collider>() != null)
                    {
                        childGameobject.GetComponent<Collider>().enabled = true;
                    }
                }
            }
        }
        
        private GameObject DuplicatePartForDeath(GameObject original, GameObject sourcePrefab)
        { 
            PredictedObjectID? id = predictionManager.hierarchy.Create(
                sourcePrefab, original.transform.position, original.transform.rotation);
            GameObject copy = id.GetGameObject(predictionManager);
            if (copy == null)
            {
                Debug.LogWarning($"Could not create dead part from {sourcePrefab.name}, is it in the registered prefab list?");
                return null; 
            } 
            
            
           copy.AddComponent<DeadBodyPart>();
           if (copy.GetComponent<CreatureBodySegment>() != null)
           {
               copy.GetComponent<CreatureBodySegment>().visualBodySegment.GetComponent<MeshRenderer>().material =
                   original.GetComponent<CreatureBodySegment>().visualBodySegment.GetComponent<MeshRenderer>().material;
               print("copy material set to: " + copy.GetComponent<CreatureBodySegment>().visualBodySegment.GetComponent<MeshRenderer>().material);
           }
           else if (copy.GetComponent<WormHead>() != null)
           {
               copy.GetComponent<WormHead>().wormVisualHeadWithMaterial.GetComponent<MeshRenderer>().material =
                   original.GetComponent<WormHead>().wormVisualHeadWithMaterial.GetComponent<MeshRenderer>().material;
           }
           
           Rigidbody originalRb = original.GetComponent<Rigidbody>();
           Rigidbody copyRb = copy.GetComponent<Rigidbody>();
    
            if (originalRb != null && copyRb != null)
            {
                copyRb.isKinematic = false;
                copyRb.useGravity = true;
                copyRb.linearVelocity = originalRb.linearVelocity * GameParameters.DeadPartVelocityMultiplier;
                copyRb.angularVelocity = originalRb.angularVelocity * GameParameters.DeadPartVelocityMultiplier;
            }
            
            // if (!original.TryGetComponent<AttachablePart>(out _))
            // {
            //     if (copy.TryGetComponent<CreatureBodySegment>(out var segment))
            //     {
            //         segment.SetMaterial(DeadBodyPartMaterial);
            //         segment.visualBodySegment.GetComponent<MeshRenderer>().enabled = true;
            //     }
            //     else
            //         foreach (MeshRenderer renderer in copy.GetComponentsInChildren<MeshRenderer>())
            //             renderer.material = DeadBodyPartMaterial;
            // }
            
            foreach (Joint joint in copy.GetComponents<Joint>())
                Destroy(joint);
            foreach (Joint joint in copy.GetComponentsInChildren<Joint>())
                Destroy(joint);
            
            // Debug.Log($"DeadBodyPartMaterial: {(DeadBodyPartMaterial ? DeadBodyPartMaterial.name : "NULL")} | part {original.name}");
            // foreach (var r in original.GetComponentsInChildren<Renderer>(true))
            //     Debug.Log($"  ORIGINAL {r.name} | enabled {r.enabled} | active {r.gameObject.activeInHierarchy} | mat {(r.sharedMaterial ? r.sharedMaterial.name : "NULL")}");
            // foreach (var r in copy.GetComponentsInChildren<Renderer>(true))
            //     Debug.Log($"  DEBRIS   {r.name} | enabled {r.enabled} | active {r.gameObject.activeInHierarchy} | mat {(r.sharedMaterial ? r.sharedMaterial.name : "NULL")}");

            return copy;
        }
        
        // private IEnumerator AttackSequence()
        // {
        //     OnWormHeadbutCharge?.Invoke();
        //     yield return new WaitForSeconds(GameParameters.WormHeadbutTime);
        //     CurrentState = WormState.AttackCooldown;
        //     wormHeadBut.EndHeadBut();
        //     OnWormHeadbutLaunch?.Invoke();
        //     yield return new WaitForSeconds(GameParameters.WormHeadButCoolDown);
        //     CurrentState = WormState.Idle;
        // }
        
        private void SetWormGrounding()
        {
            IsWormGrounded = false;
            
            foreach (var segment in wormBodySegments)
            {
                if (segment.GetComponent<CreaturePart>().IsGrounded)
                {
                    IsWormGrounded = true;
                    IsWormGroundedBySegments = true;
                    return;
                }
            }

            IsWormGroundedBySegments = false;

            foreach (var part in attachedWormParts)
            {
                if (part.GetComponent<CreaturePart>().IsGrounded)
                {
                    IsWormGrounded = true;
                    return;
                }
            }
        }

        private void RotateVisualHead()
        {
            if (thirdPersonCamera == null)
            {
                Debug.LogWarning("RotateVisualHead: thirdPersonCamera is NULL!");
                return;
            }

            Vector3 cameraForward = thirdPersonCamera.transform.forward;
            cameraForward.y += GameParameters.VisualHeadVerticalOffset;
            cameraForward.Normalize();

            if (cameraForward.sqrMagnitude < 0.01f) return;

            float signedAngle = Vector3.SignedAngle(wormHead.forward, cameraForward, Vector3.up);
            float clampedAngle = Mathf.Clamp(signedAngle, -90f, 90f);
            wormVisualHead.rotation = Quaternion.AngleAxis(clampedAngle, Vector3.up) * wormHead.rotation;
        }

        #endregion
    }
}