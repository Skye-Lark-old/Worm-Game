using System.Collections;
using PurrNet;
using PurrNet.Packing;
using UnityEngine;

public class LoadingScreenManager : PurrMonoBehaviour
{
    public static LoadingScreenManager instance;
    private Canvas canvas;
    private bool isDuplicate;
    private bool registered;
    private Coroutine waiter;
    private bool lastEnabled;
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

    private IEnumerator RegisterWhenReady()
    {
        while (NetworkManager.main == null) yield return null;
        waiter = null;
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
    

    void OnLoadingScreenRequest(PlayerID player, LoadingScreenRequest request, bool isServer)
    {
        Debug.Log($"[Loading] request display={request.display} asServer={isServer}");
        canvas.enabled = request.display;
    }
    
    public static void LoadingScreenForSelf(bool display)
    {
        Debug.Log($"[Loading] ForSelf({display})\n{System.Environment.StackTrace}");
        instance.canvas.enabled = display;
    }
    
    public static void LoadingScreenForAll(bool display)
    {
        if (!Network.instance.manager.isServer&&!Network.instance.manager.isHost)return;
        LoadingScreenRequest request;
        request.display = display;
        Network.instance.manager.SendToAll<LoadingScreenRequest>(request);
    }

    struct LoadingScreenRequest : IPackedAuto
    {
        public bool display;
    }
    
    public override void Subscribe(NetworkManager manager, bool asServer)
    {
        manager.Subscribe<LoadingScreenRequest>(OnLoadingScreenRequest, asServer);
    }

    public override void Unsubscribe(NetworkManager manager, bool asServer)
    {
        manager.Unsubscribe<LoadingScreenRequest>(OnLoadingScreenRequest, asServer);
    }
}
