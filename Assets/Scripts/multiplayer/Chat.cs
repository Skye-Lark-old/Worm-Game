using PurrNet;
using PurrNet.Packing;
using TMPro;
using UnityEngine;

namespace multiplayer
{
    public class Chat : PurrMonoBehaviour
    {
        #region public variables
    
        public GameObject chatTextPrefab;
        public Transform chatImage;
        public TMP_InputField chatInputField;
        public int maxMessages = 10;
    
        #endregion
    
        public struct ChatMessage : IPackedAuto
        {
            public string message;
            public PlayerID senderID;
        }
    
        #region public methods

        public void SendChatMessage(string message)
        {
            if (message == "") return;
    
            ChatMessage chatMessage = new ChatMessage
            {
                message = message
            };
    
            Network.instance.manager.SendToServer<ChatMessage>(chatMessage);
        }
    
        public void PostChatMessage(string message, PlayerID playerID)
        {
            if (message == "") return;
    
            string playerName = PlayerRegister.Players.ContainsKey(playerID) 
                ? PlayerRegister.Players[playerID].name 
                : "Unknown";
    
            string finalMessage = "<" + playerName + "> " + message;
    
            GameObject messageObject = Instantiate(chatTextPrefab, chatImage);
            TextMeshProUGUI messageText = messageObject.GetComponent<TextMeshProUGUI>();
            messageText.text = finalMessage;
    
            if (chatImage.childCount > maxMessages)
            {
                Destroy(chatImage.GetChild(0).gameObject);
            }
    
            chatInputField.text = "";
            chatInputField.ActivateInputField();
        }
    
        public override void Subscribe(NetworkManager manager, bool asServer)
        {
            manager.Subscribe<ChatMessage>(OnChatMessage, asServer);
        }
        
        public override void Unsubscribe(NetworkManager manager, bool asServer)
        {
            manager.Unsubscribe<ChatMessage>(OnChatMessage, asServer);
            
        }
        
        #endregion
    
        #region private methods

        // Called when a ChatMessage broadcast is sent from either the Server or a Client
        private void OnChatMessage(PlayerID player, ChatMessage data, bool asServer)
        {
            if (asServer)   // Server receives from client
            {
                data.senderID = player;
        
                // Broadcast to all clients with the correct sender ID embedded
                Network.instance.manager.SendToAll<ChatMessage>(data);
            }
            else    // Client receives from server
            {
        
                // Use the senderID from the message, not the callback parameter
                PostChatMessage(data.message, data.senderID);
            }
        }
    
        #endregion
    }
}
 