using System.Collections;
using System.Collections.Generic;
using CreatureBuilder;
using Graphics;
using PurrNet;
using UnityEngine;
using TMPro;
using Unity.Cinemachine;
using Unity.VisualScripting;
using UnityEngine.SceneManagement;

namespace Player
{
    public class PlayerGraphics : MonoBehaviour
    {
        public TextMeshPro usernameText;
        
        private Camera mainCamera;
        private HighlightOutline playerOutline;
        
        private WormRenderer wormRenderer;
        private Color? teamColor;
        
        private Player player;
        void Awake() => player = GetComponent<Player>();
        
        void Start()
        {
            //SceneManager.sceneLoaded += OnSceneLoaded;

            if (GameSceneList.IsSceneAGameScene(SceneManager.GetActiveScene().name))
            {
                EnterGameScene();
            }
        }

        void Update()
        {
            UpdateBrokenTexture();
        }

        private void UpdateBrokenTexture()
        {
            float healthPercentage = Mathf.Clamp01(player.currentPlayerHealth / player.maxPlayerHealth);

            Material brokenTexture = wormRenderer.wormMeshObject.GetComponent<MeshRenderer>().materials[1];
            Color brokenColor = brokenTexture.color;
            brokenColor.a = 1f - healthPercentage;
            brokenTexture.color = brokenColor;
        }

        void OnEnable()
        {
            wormRenderer = GetComponent<WormRenderer>();
            wormRenderer.OnMeshReady += ApplyOutline;

            GetComponent<Player>().OnPlayerTeamChanged += HandleTeamChanged;
            CinemachineCore.CameraUpdatedEvent.AddListener(OnCinemachineUpdate);
            SceneManager.sceneLoaded += OnSceneLoaded;

            GetComponent<Player>().OnWormDeath += OnPlayerDeath;
            GetComponent<PlayerSpawning>().OnWormRespawn += OnWormRespawn;
        }

        void OnDisable()
        {
            if (wormRenderer != null) wormRenderer.OnMeshReady -= ApplyOutline;

            GetComponent<Player>().OnPlayerTeamChanged -= HandleTeamChanged;
            CinemachineCore.CameraUpdatedEvent.RemoveListener(OnCinemachineUpdate);
            SceneManager.sceneLoaded -= OnSceneLoaded;

            GetComponent<Player>().OnWormDeath -= OnPlayerDeath;
            GetComponent<PlayerSpawning>().OnWormRespawn -= OnWormRespawn;
        }

        private void OnPlayerDeath()
        {
            usernameText.enabled = false;
        }

        private void OnWormRespawn()
        {
            usernameText.enabled = true;
        }

        private void OnCinemachineUpdate(CinemachineBrain brain)
        {
            UsernameFaceCamera();
        }
        
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (GameSceneList.IsSceneAGameScene(scene.name))
            {
                EnterGameScene();
            }
            else
            {
                teamColor = null;
                if (playerOutline != null) playerOutline.RemoveHighlight();
                if (usernameText != null) usernameText.enabled = false;
            }
        }

        private void EnterGameScene()
        {

            if (mainCamera == null) mainCamera = Camera.main;

            StartCoroutine(SetUserNameTag());
        }

        IEnumerator SetUserNameTag()
        {
            yield return new WaitUntil(() => usernameText != null);
            usernameText.enabled = true;
            usernameText.text = "<mark=#000000aa>" + GetComponent<Player>().PlayerName + "</mark>";
        }

        private void UsernameFaceCamera()
        {
            if (mainCamera != null && usernameText != null)
            {
                usernameText.transform.forward = mainCamera.transform.forward;
            }
        }
        
        #region PlayerOutline

        private void HandleTeamChanged(string team)
        {
            teamColor = team == "Red" ? Color.red : team == "Blue" ? Color.blue : Color.white;
            ApplyOutline();   
        }
        
        private void ApplyOutline()
        {
            if (teamColor == null || wormRenderer == null || wormRenderer.wormMeshObject == null) return;
            
            if (playerOutline == null)
                playerOutline = wormRenderer.wormMeshObject.AddComponent<HighlightOutline>();

            playerOutline.HighlightPart(teamColor.Value, 0.1f);
        }


        // [ServerRpc]
        // private void ServerHandleTeamChanged(GameObject player, Color teamColor)
        // {
        //     ObserverHandleTeamChanged(player, teamColor);
        // }
        //
        // [ObserversRpc]
        // private void ObserverHandleTeamChanged(GameObject player, Color teamColor)
        // {
        //     if (player != gameObject) return;
        //     StartCoroutine(HandleTeamChanged(player, teamColor));
        // }

        // private IEnumerator HandleTeamChanged(GameObject player, Color teamColor)
        // {
        //     Transform wormMeshTransform = null;
        //     float timeout = 5f;
        //     float elapsed = 0f;
        //
        //     while (wormMeshTransform == null)
        //     {
        //         wormMeshTransform = GetComponent<WormRenderer>().wormMeshObject.transform;
        //
        //         if (wormMeshTransform == null)
        //         {
        //             if (elapsed >= timeout)
        //             {
        //                 Debug.LogWarning($"WormMesh not found on {gameObject.name} after {timeout}s, skipping outline.");
        //                 yield break;
        //             }
        //             elapsed += Time.deltaTime;
        //             yield return null;
        //         }
        //     }
        //
        //     GameObject wormMesh = wormMeshTransform.gameObject;
        //     if (playerOutline == null) playerOutline = wormMesh.AddComponent<HighlightOutline>();
        //     playerOutline.HighlightPart(teamColor, 0.1f);
        // }
        
        #endregion
        
    }
}