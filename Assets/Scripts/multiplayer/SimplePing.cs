using System.Collections;
using PurrNet;
using PurrNet.Packing;
using PurrNet.Transports;
using UnityEngine;

namespace multiplayer
{
    public class SimplePing : PurrMonoBehaviour
    {
        private int responses = 0;

        private struct ping : IPackedAuto
        {
            public bool IsResponse;
        }

        public IEnumerator Ping()
        {
            responses = 0;
            Network.instance.manager.SendToAll<ping>(new ping(), Channel.ReliableOrdered);
            print("Ping");
            yield return new WaitUntil(() => responses == Network.instance.manager.playerCount);
        }

        private void Pong(PlayerID id, ping p, bool asserver)
        {
            if (!p.IsResponse)
            {
                p.IsResponse = true;
                manager.SendToServer<ping>(p, Channel.ReliableOrdered);
                print("Pong");
            }
            else
            {
                if (Network.instance.manager.isServer || Network.instance.manager.isHost)
                {
                    responses++;
                }
                else
                {
                    Debug.LogWarning("A Client has received a ping response. This should not happen!");
                }
            }
        }

        public override void Subscribe(NetworkManager manager, bool asServer)
        {
            manager.Subscribe<ping>(Pong, asServer);
        }

        public override void Unsubscribe(NetworkManager manager, bool asServer)
        {
            manager.Unsubscribe<ping>(Pong, asServer);
        }
    }
}