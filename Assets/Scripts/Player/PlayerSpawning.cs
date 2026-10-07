using System;
using System.Collections;
using System.Collections.Generic;
using CreatureBuilder;
using CreatureParts;
using PurrNet;
using PurrNet.Prediction;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;


namespace Player
{
    public class PlayerSpawning : MonoBehaviour
    {
        #region public variables

        public Player player;
        public bool canRespawn = true;
        public DeathScreenUI deathScreenUI;

        public Vector3 spawnPoint = new Vector3(); //default
        private Vector3 CreatureBuildingSpawnPoint = new Vector3(0, 2, 0f);
        private Quaternion spawnRotation = Quaternion.Euler(0, 90, 0); // default
        private Quaternion headRotationInCreatureBuilder = Quaternion.Euler(0, 180, 0);

        private Coroutine respawnCoroutine;

        private bool isRegistered = false;
        private bool hasBeenSetup = false;
        private bool spawnPointSet = false;
        private bool hasBeenVisuallyEnabledInGameScene = false;

        private float TimeToWaitForSpawnpointSet = 1f;

        public event Action OnWormRespawn;

        #endregion

        #region Built-In Methods

        void Start()
        {
            player = GetComponent<Player>();
            player.playerSpawning = this;

            SceneManager.sceneLoaded += OnSceneLoaded;

            if (GameSceneList.IsSceneAGameScene(SceneManager.GetActiveScene().name))
            {
                deathScreenUI = FindFirstObjectByType<DeathScreenUI>();
                if (LocalPlayer.Instance != null)
                {
                    LocalPlayer.Instance.canDie = true;
                }
            }
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (player != null) LocalPlayer.Unregister(player);
        }

        // protected override void OnDespawned()
        // {
        //     LocalPlayer.Unregister(player);
        //     SceneManager.sceneLoaded -= OnSceneLoaded;
        // }

        #endregion
        
        #region Public Methods

        public void SetSpawnPoint(GameObject inputSpawnpoint)
        {
            spawnPoint = inputSpawnpoint.transform.position;
            spawnRotation = inputSpawnpoint.transform.rotation;
            spawnPointSet = true;
            Debug.Log("spawnpoint set with position: " + spawnPoint + " ,rotation: " + spawnRotation);
        }
        
        public void SetWormInGameScene()
        {
            if (!hasBeenVisuallyEnabledInGameScene) DisableWormVisually();
            
            if (player.isOwner)
            {
                SetWormInGameSceneAsOwner();
            }
            else
            {
                player.ActivatePlayer();
                StartCoroutine(AssignPlayerTeam());
                EnableWormVisually();
                hasBeenVisuallyEnabledInGameScene = true;
                GetComponent<WormPhysics>().ToggleWormCollisions(true);
                GetComponent<WormPhysics>().ToggleWormKinematics(false);   // forces can't move kinematic bodies
            }
            
        }
        
        // [ServerRpc]
        // public void SetKinematicStateServer(bool isKinematic, Player playerToUpdate)
        // {
        //     SetKinematicStateObserver(isKinematic, playerToUpdate);
        // }

        private void SetWormInGameSceneAsOwner()
        {
            Debug.Log($"Setting worm {player.PlayerName} in game scene as owner");
            StartCoroutine(SpawnAtSpawnPoint());
            player.ActivatePlayer();

            StartCoroutine(AssignPlayerTeam());
        }

        public IEnumerator AssignPlayerTeam()
        {
            Debug.Log($"AssignPlayerTeam | Players null: {PlayerRegister.Players == null} | playerID: {player.playerID} | owner: {player.owner}");
            yield return new WaitUntil(() =>
                PlayerRegister.Players != null &&
                PlayerRegister.Players.ContainsKey(player.playerID) &&
                player.RegisterData.team.ToString() != "None" &&
                player.RegisterData.team.ToString() != "");
            
            string team = player.RegisterData.team.ToString();
            player.SetPlayerTeam(team);
        }
        
        public IEnumerator SetWormInCreatureBuilderScene()
        {
            if (player.owner == player.predictionManager.localPlayer)
            {
                LocalPlayer.Instance.canDie = false;
                Debug.Log("setting worm in creature builder as owner");
                var wormPhysics = GetComponent<WormPhysics>();
                wormPhysics.MakeWormKinematic();
                SetWormSpawnRotation(headRotationInCreatureBuilder);
                wormPhysics.PositionWormSegments(CreatureBuildingSpawnPoint);
                player.DeactivatePlayer();
                GetComponent<PlayerPartAttachment>().AddAlreadyAttachedParts();
                player.wormVisualHead.rotation = headRotationInCreatureBuilder;
            }
            else
            {
                DisableWormVisually();
            }
            yield return null;
        }
        
        public void OnWormRewired()
        {
            bool kinematic = player.isOwner && !GameSceneList.IsSceneAGameScene(SceneManager.GetActiveScene().name);
            var wp = GetComponent<WormPhysics>();
            wp.ToggleWormCollisions(true);
            wp.ToggleWormKinematics(kinematic);
        }
        
        public void DisableWormVisually()
        {
            GetComponent<WormRenderer>().DisableRendering();
            player.wormHead.GetComponent<WormHead>().wormVisualHeadWithMaterial.GetComponent<MeshRenderer>().enabled = false;
            
            if (transform.Find("WormMesh") != null) GetComponent<WormRenderer>().DestroyMesh();
            if (player.wormVisualHead.GetComponent<MeshRenderer>() != null) player.wormVisualHead.GetComponent<MeshRenderer>().enabled = false;
            
            GameObject visualHeadWithMaterial = player.wormHead.GetComponent<WormHead>().wormVisualHeadWithMaterial;
            visualHeadWithMaterial.GetComponent<MeshRenderer>().enabled = false;
            foreach (MeshRenderer mr in visualHeadWithMaterial.GetComponentsInChildren<MeshRenderer>())
            {
                mr.enabled = false;
            }
        }

        public void EnableWormVisually()
        {
            if (player.wormVisualHead.GetComponent<MeshRenderer>() != null)
                player.wormVisualHead.GetComponent<MeshRenderer>().enabled = true;
            GameObject visualHeadWithMaterial = player.wormHead.GetComponent<WormHead>().wormVisualHeadWithMaterial;
            visualHeadWithMaterial.GetComponent<MeshRenderer>().enabled = true;
            foreach (MeshRenderer mr in visualHeadWithMaterial.GetComponentsInChildren<MeshRenderer>())
                mr.enabled = true;
            
            foreach (MeshRenderer mr in visualHeadWithMaterial.GetComponentsInChildren<MeshRenderer>())
            {
                mr.enabled = true;
            }

            foreach (GameObject attachedPart in player.attachedWormParts)
            {
                //FIND all renderers that are children of the attached part, as long as the object itself is enabled
            }
            
        }
        
        #endregion
        
        
        // [ServerRpc(runLocally: true)]
        // private void SyncSpawnPointServer(Vector3 position, Quaternion rotation, Player syncPlayer)
        // {
        //     SyncSpawnPointObserver(position, rotation, syncPlayer);
        // }
        
        // [ObserversRpc(runLocally: true)]
        // private void SyncSpawnPointObserver(Vector3 position, Quaternion rotation, Player syncPlayer)
        // {
        //     if (syncPlayer != player) return;
        //     spawnPoint = position;
        //     spawnRotation = rotation;
        //     spawnPointSet = true;
        // }
        
        #region Private Methods

        // [ObserversRpc]
        // private void SetKinematicStateObserver(bool isKinematic, Player playertoUpdate)
        // {
        //     if (playertoUpdate != player) return;
        //     
        //     GetComponent<WormPhysics>().ToggleWormKinematics(isKinematic);
        // }
        
        private void SetWormSpawnRotation(Quaternion orientation)
        {
            player.wormHead.rotation = orientation;
            player.wormVisualHead.localRotation = Quaternion.identity;
            foreach (Transform segment in player.wormBodySegments)
            {
                segment.rotation = orientation;
            }
        }
        
        private void SetWormSpawnPosition(Vector3 spawnPosition)
        {
            Debug.Log("spawning with position: " + spawnPosition);
            if (player.wormHead == null) return;

            player.wormHead.position = spawnPosition;
            Rigidbody headRb = player.wormHead.GetComponent<Rigidbody>();
            if (headRb != null)
            {
                headRb.position = spawnPosition;
            }

            Vector3 currentPos = player.wormHead.position;
            Vector3 backDir = -player.wormHead.forward;

            for (int i = 0; i < player.wormBodySegments.Count; i++)
            {
                currentPos += backDir * GameParameters.SegmentMaxPartDistance;
                Transform segment = player.wormBodySegments[i];
                segment.position = currentPos;
                segment.rotation = player.wormHead.rotation;

                Rigidbody rb = segment.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.position = currentPos;
                    rb.rotation = player.wormHead.rotation;
                }
            }
        }
        
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "CreatureBuilderScene")
            {
                StartCoroutine(SetWormInCreatureBuilderScene());
            }
            else if (GameSceneList.IsSceneAGameScene(scene.name))
            {
                
                SetWormInGameScene();
            }
            else if (scene.name == "GameLobbyScene")
            {
                SpawnPlayerInLobbyScene();
            }
            else
            {
                player.IsInvincible = true;
                hasBeenVisuallyEnabledInGameScene = false;
                GetComponent<WormPhysics>().ToggleWormCollisions(false);
                //TODO: ensure collisions are enabled in lobby
                GetComponent<Player>().DeactivatePlayer();
            }
        }
        
        #endregion
        
        #region Initial Spawning
        
        public void InitialSpawn()
        {
            if (player == null) player = GetComponent<Player>();
            isRegistered = true;
            Debug.Log($"Player spawned | owner: {player.owner} | isOwner: {player.isOwner} | localPlayer: {player.predictionManager.localPlayer}");

            // every peer, owner or not
            player.CurrentState = WormState.Idle;
            player.IsWormGrounded = false;
            player.MaxVelocity = GameParameters.WormMaxVelocity;

            player.wormForwardMovement = GetComponent<WormForwardMovement>();
            player.wormJump = GetComponent<WormJump>();
            player.wormHeadBut = GetComponent<WormHeadBut>();

            if (player.owner == player.predictionManager.localPlayer)
                LocalPlayer.Register(player);
        }

        // private IEnumerator InitialSpawnAsOwner()
        // {
        //     LocalPlayer.Register(player);
        //         
        //     player.CurrentState = WormState.Idle;
        //     player.IsWormGrounded = false;
        //     player.MaxVelocity = GameParameters.WormMaxVelocity;
        //
        //     player.wormForwardMovement = GetComponent<WormForwardMovement>();
        //     player.wormJump = GetComponent<WormJump>();
        //     player.wormHeadBut = GetComponent<WormHeadBut>();
        //     
        //     player.wormBodySegments.Clear();
        //     //GetComponent<WormConstructor>().CreateWormSegments();
        //     
        //     //GetComponent<WormConstructor>().ConstructWorm();
        //     //GetComponent<WormPhysics>().AddCollidersToSegments();
        //     //GetComponent<WormConstructor>().AddSegmentJointsAsServer(player);
        //     //GetComponent<WormConstructor>().AddSegmentJoints();
        //     //GetComponent<WormPhysics>().ToggleWormKinematics(true);
        //
        //     if (GameSceneList.IsSceneAGameScene(SceneManager.GetActiveScene().name))
        //         SetWormInGameScene();
        //     else if (SceneManager.GetActiveScene().name == "CreatureBuilderScene" && gameObject.activeSelf)
        //         StartCoroutine(SetWormInCreatureBuilderScene());
        //     else if (SceneManager.GetActiveScene().name == "GameLobbyScene")
        //     {
        //         SpawnPlayerInLobbyScene();
        //     }
        //     
        //     yield return null;
        // }
        
        public void OnWormWired()
        {
            if (player.owner == player.predictionManager.localPlayer)
                GetComponent<WormPhysics>().ToggleWormKinematics(true);

            string scene = SceneManager.GetActiveScene().name;

            if (GameSceneList.IsSceneAGameScene(scene))
                SetWormInGameScene();
            else if (scene == "CreatureBuilderScene" && gameObject.activeSelf)
                StartCoroutine(SetWormInCreatureBuilderScene());
            else if (scene == "GameLobbyScene")
                SpawnPlayerInLobbyScene();
        }

        private void SpawnPlayerInLobbyScene()
        {
            if (player.isOwner)
            {
                Debug.Log($"spawning player in lobby: {player.owner}" );
                player.IsInvincible = true;
                hasBeenVisuallyEnabledInGameScene = false;
            
                GetComponent<WormPhysics>().ToggleWormKinematics(true);
            }
            else //hard coded as all players are non kinematic in lobby
            {
                GetComponent<WormPhysics>().ToggleWormKinematics(false);
            }
            
        }

        // [ServerRpc]
        // private IEnumerator SpawnAsServer(Player playerToSpawn)
        // {
        //     Debug.Log($"Trying to spawn player {playerToSpawn.owner} as server, this player: {player.owner}.");
        //     if (playerToSpawn != player) yield break;
        //     
        //     Debug.Log($"Spawning player {player.PlayerName} as server.");
        //     
        //     playerToSpawn.wormBodySegments.Clear();
        //     playerToSpawn.GetComponent<WormConstructor>().CreateWormSegments();
        //
        //     yield return null;
        // }
        
        #endregion
        
        #region Respawning
        
        public void TryToRespawn()
        {
            if (canRespawn)
            {
                respawnCoroutine = StartCoroutine(RespawnTimer());
            }
        }
        
        public IEnumerator RespawnTimer()
        {
            if (GameSceneList.IsSceneAGameScene(SceneManager.GetActiveScene().name))
            {
                float timeLeft = GameParameters.PlayerRespawnTimeInSeconds;
    
                while (timeLeft > 0)
                {
                    deathScreenUI.respawnText.text = "Respawning in " + Mathf.Ceil(timeLeft);
                    yield return new WaitForSeconds(1f);
                    timeLeft -= 1f;
                }
    
                deathScreenUI.respawnText.text = "Respawning...";
                
                StartCoroutine(RespawnPlayer());
            }
        }

        private IEnumerator RespawnPlayer()
        {
            // if (GameSceneList.IsSceneAGameScene(SceneManager.GetActiveScene().name))
            // {
            //     OnWormRespawn?.Invoke();
            // }

            if (player == LocalPlayer.Instance)
            {
                yield return StartCoroutine(RespawnPlayerAsOwner());
            }

            //RespawnPlayerAsNonOwner();
        }
        
        private IEnumerator RespawnPlayerAsOwner()
        {
            Debug.Log("Respawning player as Owner" + player.PlayerName);
            player.CurrentState = WormState.Idle;
            player.currentPlayerHealth = GameParameters.DefaultPlayerHealth;
            
            if (GameSceneList.IsSceneAGameScene(SceneManager.GetActiveScene().name))
            {
                player.thirdPersonCamera.GetComponent<CinemachineBrain>().enabled = true;
                deathScreenUI.DisableDeathUI();
                StartCoroutine(AssignPlayerTeam());
            }
            
            //yield return StartCoroutine(SpawnAtSpawnPoint());
            player.canDie = true;
            player.currentPlayerHealth = player.maxPlayerHealth;
            player.RequestRespawn(spawnPoint, spawnRotation);
            player.IsInvincible = false;
            yield return null;
        }

        // [ServerRpc]
        // private void RespawnPlayerAsNonOwnerServerRPC(Player playerToRespawn)
        // {
        //     RespawnPlayerAsNonOwnerObserversRPC(playerToRespawn);
        // }
        
        // [ObserversRpc]
        // private void RespawnPlayerAsNonOwnerObserversRPC(Player playerToRespawn)
        // {
        //     if (playerToRespawn != player) return;
        //     
        //     RespawnPlayerAsNonOwner();
        // }

        public void RespawnPlayerAsNonOwner()
        {
            EnableWormVisually();
            Debug.Log("Respawning player as Nonowner" + player.PlayerName);
            
            player.EnablePartForRespawn(player.wormHead.gameObject);
            
            foreach (Transform bodySegment in player.wormBodySegments)
                player.EnablePartForRespawn(bodySegment.gameObject);
            
            StartCoroutine(GetComponent<PlayerPartAttachment>().ReactivateAttachedParts());
        }
        
        private IEnumerator SpawnAtSpawnPoint()
        {
            float elapsed = 0f;
            while (!spawnPointSet && elapsed < TimeToWaitForSpawnpointSet)
            {
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }

            if (!spawnPointSet)
            {
                Debug.LogWarning("SpawnAtSpawnPoint timed out waiting for spawn point to be set.");
            }
            
            yield return null;

            deathScreenUI = FindFirstObjectByType<DeathScreenUI>();
            player.thirdPersonCamera = Camera.main?.gameObject;
            player.canDie = true;
            player.currentPlayerHealth = player.maxPlayerHealth;
            
            GetComponent<WormPhysics>().ToggleWormKinematics(true);
            yield return new WaitUntil(() => player.wormBodySegments.Count == player.WormSegmentCount);
            player.GetComponent<WormConstructor>().ConstructWorm();
            //GetComponent<WormPhysics>().AddCollidersToSegments();
            Debug.Log("SAS: constructed");

            yield return new WaitForFixedUpdate();
            Debug.Log("SAS: after fixed update");

            SetWormSpawnRotation(spawnRotation);
            SetWormSpawnPosition(spawnPoint);
            
            EnableWormLocal();
            
            GetComponent<WormPhysics>().ToggleWormKinematics(false);
            player.IsInvincible = false;
        }

        // [ServerRpc]
        // private void EnableWormServerRPC(Player playerToEnable)
        // {
        //     EnableWormObserverRPC(playerToEnable);
        // }
        
        private void EnableWormLocal()
        {
            hasBeenVisuallyEnabledInGameScene = true;
            EnableWormVisually();
            GetComponent<WormPhysics>().ToggleWormCollisions(true);
        }
        
        public void TeleportWorm(Vector3 position, Quaternion rotation)
        {
            SetWormSpawnRotation(rotation);
            SetWormSpawnPosition(position);
        }

        public void HandleRespawnVisuals()
        {
            OnWormRespawn?.Invoke();
            RespawnPlayerAsNonOwner();
        }

        #endregion
    }
}
