using System;
using multiplayer;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class IPText : MonoBehaviour
{
    [SerializeField]
    private TMP_InputField text;
    
    
    public void OnUpdate()
    {
        Network.targetAddress = text.text;
    }
}
