using System.Collections;
using System.Collections.Generic;
using GameLoop;
using Player;
using PurrNet;
using PurrNet.Packing;
using PurrNet.Transports;
using UnityEngine;

namespace multiplayer
{
    public class PartSelectionManager : PurrMonoBehaviour
    {
        #region private variables
        
        private static List<SelectableCardsPacket> _sentSelectionPackets;
        private static List<ReturnedCardPacket> _returnedCardIdexes;
        private static int resendPointer;
        
        #endregion
        
        #region structs
        
        public struct SelectableCardsPacket : IPackedAuto
        {
            public int Card1Index;
            public int Card2Index;
            public PlayerID receiver;
        }
    
        public struct ResentCardPacket : IPackedAuto
        {
            public int CardIndex;
            public PlayerID receiver;
        }
    
        public struct ReturnedCardPacket : IPackedAuto
        {
            public int CardIndex;
            public PlayerID sender;
        }
        
        #endregion
        
        #region built in methods
        
        void Start()
        {
            if (Network.instance.manager.isServer || Network.instance.manager.isHost)
            {
                _sentSelectionPackets = new List<SelectableCardsPacket>();
                _returnedCardIdexes = new List<ReturnedCardPacket>();
                StartCoroutine(PickCardOptions());
            }
        }
        
        public override void Subscribe(NetworkManager manager, bool asServer)
        {
            manager.Subscribe<SelectableCardsPacket>(SetCardOptions, asServer);
            manager.Subscribe<ReturnedCardPacket>(HandleReturnedCard, asServer);
            manager.Subscribe<ResentCardPacket>(HandleResentCard, asServer);

        }

        public override void Unsubscribe(NetworkManager manager, bool asServer)
        {
            manager.Unsubscribe<SelectableCardsPacket>(SetCardOptions, asServer);
            manager.Unsubscribe<ReturnedCardPacket>(HandleReturnedCard, asServer);
            manager.Unsubscribe<ResentCardPacket>(HandleResentCard, asServer);
        }
        
        #endregion
        
        #region public methods

        public IEnumerator PickCardOptions()
        {
            yield return new WaitUntil(() => Network.instance.AllClientsReady());
            yield return StartCoroutine(Network.pinger.Ping());
            StartCoroutine(global::GameLoop.GameLoop.gameLoopTimer.Timer(global::GameLoop.GameLoop.timePerPartSelection));
        
            SelectableCardsPacket packet = new SelectableCardsPacket();
            List<PlayerID> players = new List<PlayerID>(Network.instance.manager.players);
            while (players.Count > 0)
            {
                (packet.Card1Index, packet.Card2Index) = Pick2RandomCards();
                packet.receiver = players[0];
                _sentSelectionPackets.Add(packet);
                Network.instance.manager.Send<SelectableCardsPacket>(packet.receiver, packet, Channel.ReliableOrdered);
                players.RemoveAt(0);
            }
        
        }
        
        public static void ReturnCard(SelectableCardsPacket packet, bool selectedFirst = true)
        {
            ReturnedCardPacket returnPacket = new ReturnedCardPacket();
            if (selectedFirst)
            {
                returnPacket.CardIndex = packet.Card2Index;
            }
            else
            {
                returnPacket.CardIndex = packet.Card1Index;
            }
            returnPacket.sender = packet.receiver;
            Network.instance.manager.SendToServer<ReturnedCardPacket>(returnPacket,Channel.ReliableUnordered);
        }
        
        public static void Shuffle<T>(List<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                int k = Random.Range(0,n);
                n--;
                (list[k], list[n]) = (list[n], list[k]);
            }
        }
        
        #endregion
        
        #region private methods
        private IEnumerator ResendCards()
        {
            while (true)
            {
                yield return new WaitUntil(() => Network.instance.AllClientsReady());
                if (_returnedCardIdexes.Count >= _sentSelectionPackets.Count)
                {
                    Shuffle(_returnedCardIdexes);
                    for (int i = 0; i < _sentSelectionPackets.Count; i++)
                    {
                        int escape = 0;
                        while (_returnedCardIdexes[0].sender == _sentSelectionPackets[i].receiver&&escape <= 100)
                        {
                            Shuffle(_returnedCardIdexes);
                            escape++;
                        }
                        ResentCardPacket packet = new ResentCardPacket
                        {
                            CardIndex = _returnedCardIdexes[0].CardIndex,
                            receiver = _sentSelectionPackets[i].receiver,
                        };
                        print("Sending card to: "+packet.receiver);
                        Network.instance.manager.Send<ResentCardPacket>(packet.receiver, packet, Channel.ReliableOrdered);
                    
                    
 
                    }
                    yield return StartCoroutine(Network.pinger.Ping());
                    global::GameLoop.GameLoop.Instance.StartCreatureBuildingCoroutine();
                    break;
                }
            }
        }
    
        private (int, int) Pick2RandomCards()
        {
            int card1Index = Random.Range(0, global::GameLoop.GameLoop.Instance.partCards.Count);
            int card2Index = Random.Range(0, global::GameLoop.GameLoop.Instance.partCards.Count - 1);
        
            if (card2Index >= card1Index)
                card2Index++;
        
            return (card1Index, card2Index);
        }
        
        private void SetCardOptions(PlayerID player, SelectableCardsPacket data, bool asServer)
        {
            PartSelection.Instance.SetCardOptions(player,data,asServer);
        }
    
        private void HandleReturnedCard(PlayerID player, ReturnedCardPacket data, bool asServer)
        {
            if(_returnedCardIdexes.Count == 0)StartCoroutine(ResendCards());
            _returnedCardIdexes.Add(data);
        }
    
        private void HandleResentCard(PlayerID player, ResentCardPacket data, bool asServer)
        {
            LocalPlayer.Instance.wormPartsInInventory.Add(global::GameLoop.GameLoop.partCardsStatic[data.CardIndex]);
        }
        
        #endregion
    }
}
