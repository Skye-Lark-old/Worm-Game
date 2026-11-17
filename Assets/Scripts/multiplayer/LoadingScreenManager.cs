using System.Collections;
using PurrNet;
using PurrNet.Packing;
using UnityEngine;

namespace multiplayer
{
    public class LoadingScreenManager : PurrMonoBehaviour
    {
    
        #region public variables
        public static LoadingScreenManager instance;
    
        #endregion
    
        #region private variables
    
        private Canvas canvas;
        private bool isDuplicate;
        private bool registered;
        private Coroutine waiter;
        private bool lastEnabled;
    
        #endregion
        
        struct LoadingScreenRequest : IPackedAuto
        {
            public bool Display;
        }
    
        #region built in methods
        private void Awake()
        {
            if (instance != null && instance != this)
            {
                isDuplicate = true;
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
            canvas = GetComponent<Canvas>();
            canvas.enabled = false;
        }
    
        private void LateUpdate()
        {
            if (canvas == null || canvas.enabled == lastEnabled) return;
            lastEnabled = canvas.enabled;
            Debug.Log($"[Loading] canvas.enabled -> {lastEnabled} (id {GetInstanceID()}, scene '{gameObject.scene.name}')", this);
        }
    
        public override void OnEnable()
        {
            if (isDuplicate) return;

            if (NetworkManager.main == null)
            {
                waiter = StartCoroutine(RegisterWhenReady());
                return;
            }
            base.OnEnable();
            registered = true;
        }
    
        public override void OnDisable()
        {
            if (waiter != null) { StopCoroutine(waiter); waiter = null; }
            if (!registered) return;
            registered = false;
            if (NetworkManager.main == null) return;   // manager already gone, nothing to unregister
            base.OnDisable();
        }
        
        public override void Subscribe(NetworkManager manager, bool asServer)
        {
            manager.Subscribe<LoadingScreenRequest>(OnLoadingScreenRequest, asServer);
        }

        public override void Unsubscribe(NetworkManager manager, bool asServer)
        {
            manager.Unsubscribe<LoadingScreenRequest>(OnLoadingScreenRequest, asServer);
        }
    
        #endregion
        
        #region public methods
        
        public static void LoadingScreenForSelf(bool display)
        {
            Debug.Log($"[Loading] ForSelf({display})\n{System.Environment.StackTrace}");
            instance.canvas.enabled = display;
        }
    
        public static void LoadingScreenForAll(bool display)
        {
            if (!Network.instance.manager.isServer&&!Network.instance.manager.isHost)return;
            LoadingScreenRequest request;
            request.Display = display;
            Network.instance.manager.SendToAll<LoadingScreenRequest>(request);
        }
        
        #endregion

        #region private methods
        private IEnumerator RegisterWhenReady()
        {
            while (NetworkManager.main == null) yield return null;
            waiter = null;
            base.OnEnable();
            registered = true;
        }
        
        private void OnLoadingScreenRequest(PlayerID player, LoadingScreenRequest request, bool isServer)
        {
            Debug.Log($"[Loading] request display={request.Display} asServer={isServer}");
            canvas.enabled = request.Display;
        }
        
        #endregion
    }
}
