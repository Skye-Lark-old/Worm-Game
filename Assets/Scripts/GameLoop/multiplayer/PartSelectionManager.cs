using System.Collections;
using System.Collections.Generic;
using Player;
using PurrNet;
using PurrNet.Packing;
using PurrNet.Transports;
using UnityEngine;

namespace GameLoop.multiplayer
{
    public class PartSelectionManager : PurrMonoBehaviour
    {
        private static List<SelectableCardsPacket> SentSelectionPackets;
        private static List<ReturnedCardPacket> ReturnedCardIdexes;
        public static PartSelectionManager Instance;
    
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            if (Network.instance.manager.isServer || Network.instance.manager.isHost)
            {
                SentSelectionPackets = new List<SelectableCardsPacket>();
                ReturnedCardIdexes = new List<ReturnedCardPacket>();
                StartCoroutine(PickCardOptions());
            }
        }

        public IEnumerator PickCardOptions()
        {
            yield return new WaitUntil(() => Network.instance.AllClientsReady());
            yield return StartCoroutine(Network.pinger.Ping());
            StartCoroutine(global::GameLoop.GameLoop.gameLoopTimer.Timer(global::GameLoop.GameLoop.timePerPartSelection));
        
            SelectableCardsPacket packet = new SelectableCardsPacket();
            List<PlayerID> players = new List<PlayerID>(Network.instance.manager.players);
            while (players.Count > 0)
            {
                (packet.Card1Index, packet.Card2Index) = Pick2RandomCards(); //RigTheElection();
                packet.receiver = players[0];
                SentSelectionPackets.Add(packet);
                Network.instance.manager.Send<SelectableCardsPacket>(packet.receiver, packet, Channel.ReliableOrdered);
                players.RemoveAt(0);
            }
        
        }
    
        private static int[] dummyResendCards = new[] {
            3, 3, 0, 5, 
            5, 3, 0, 4, 
            3, 4, 0, 2};

        private static int resendPointer;

        void RigResendCards()
        {
            foreach (PlayerID player in PlayerRegister.Players.Keys)
            {
                ResentCardPacket packet = new ResentCardPacket();
                packet.CardIndex = dummyResendCards[resendPointer++];
            
                packet.receiver = player;
            
                Network.instance.manager.Send<ResentCardPacket>(packet.receiver, packet, Channel.ReliableOrdered);
            
            }
        }

        IEnumerator ResendCards()
        {
            while (true)
            {
                yield return new WaitUntil(() => Network.instance.AllClientsReady());
                //RigResendCards();
                //yield return StartCoroutine(Network.pinger.Ping());
                //GameLoop.GameLoop.Instance.StartCreatureBuildingCoroutine();
                //break;
                if (ReturnedCardIdexes.Count >= SentSelectionPackets.Count)
                {
                    Shuffle(ReturnedCardIdexes);
                    for (int i = 0; i < SentSelectionPackets.Count; i++)
                    {
                        int escape = 0;
                        while (ReturnedCardIdexes[0].sender == SentSelectionPackets[i].receiver&&escape <= 100)
                        {
                            Shuffle(ReturnedCardIdexes);
                            escape++;
                        }
                        ResentCardPacket packet = new ResentCardPacket
                        {
                            CardIndex = ReturnedCardIdexes[0].CardIndex,
                            receiver = SentSelectionPackets[i].receiver,
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
    
        private void SetCardOptions(PlayerID player, SelectableCardsPacket data, bool asServer)
        {
            PartSelection.Instance.SetCardOptions(player,data,asServer);
        }
    
        private void HandleReturnedCard(PlayerID player, ReturnedCardPacket data, bool asServer)
        {
            if(ReturnedCardIdexes.Count == 0)StartCoroutine(ResendCards());
            ReturnedCardIdexes.Add(data);
        }
    
        private void HandleResentCard(PlayerID player, ResentCardPacket data, bool asServer)
        {
            LocalPlayer.Instance.wormPartsInInventory.Add(global::GameLoop.GameLoop.partCardsStatic[data.CardIndex]);
            print("Recived resent packet");
        }

        private static int[] dummyCards = new[] {1,0,  0,3,  0,0,  1,5,
            0,4,  1,3,  0,0,  1,4, 
            2,1,  1,4,  0,0,  4,2};
        private static int RigIndex;
        private (int, int) RigTheElection()
        {
            int card1Index = dummyCards[RigIndex];
            RigIndex++;
            if (RigIndex >= dummyCards.Length) RigIndex = 0;
            int card2Index = dummyCards[RigIndex];
            RigIndex++;
            if (RigIndex >= dummyCards.Length) RigIndex = 0;
        
        
            return (card1Index, card2Index);
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
    
    }
}
