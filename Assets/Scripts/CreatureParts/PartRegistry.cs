using System.Collections.Generic;
using System.Linq;
using CreatureParts;
using UnityEngine;

public static class PartRegistry
{
    private static Dictionary<int, CreaturePartData> byId;

    // keeps a stale dictionary from surviving a play session when domain reload is disabled
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => byId = null;

    private static string KeyOf(CreaturePartData d) => d.name.Replace("(Clone)", "").Trim();
    public static int IdOf(CreaturePartData data) => Animator.StringToHash(KeyOf(data));

    public static bool TryGet(int id, out CreaturePartData data)
    {
        data = null;
        if (byId == null) Build();
        return byId != null && byId.TryGetValue(id, out data);
    }

    private static void Build()
    {
        CreaturePartData[] found = Resources.LoadAll<CreaturePartData>("");
        var map = new Dictionary<int, CreaturePartData>();

        foreach (var d in found)
        {
            int id = IdOf(d);
            if (map.ContainsKey(id)) Debug.LogError($"[PartRegistry] duplicate part id for '{d.name}'");
            else map[id] = d;
        }

        if (map.Count == 0)
        {
            Debug.LogError("[PartRegistry] found no PartData assets. They must be inside a folder named 'Resources' (any depth).");
            return;     
        }

        byId = map;
        Debug.Log($"[PartRegistry] registered {map.Count}: {string.Join(", ", found.Select(f => f.name))}");
    }
}