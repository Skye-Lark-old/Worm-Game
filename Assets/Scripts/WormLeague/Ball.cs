using System;
using GameLoop.multiplayer;
using PurrNet;
using PurrNet.Prediction;
using UnityEngine;

namespace WormLeague
{
    public class Ball : NetworkBehaviour
    {
        void Start()
        {
            if (!isServer && !isHost)
            {
                Destroy(this);
            }
        }
        public Player.Player LastTouchingPlayer { get; private set; }

        protected override void OnSpawned(bool asServer)
        {
            if (!isServer)
            {
                //GetComponent<Rigidbody>().isKinematic = true;
            }
        }

        public void Reset()
        {
            PredictedRigidbody rigidBody = gameObject.GetComponent<PredictedRigidbody>();
            rigidBody.angularVelocity = new Vector3(0,0,0);
            rigidBody.linearVelocity = new Vector3(0,0,0);
            rigidBody.rotation = Quaternion.identity;
            gameObject.transform.position = new Vector3(0,2,3);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!collision.gameObject.CompareTag("CreaturePart") && 
                !collision.gameObject.CompareTag("WormBodySegment"))
                return;
            
            LastTouchingPlayer = collision.gameObject.GetComponentInParent<Player.Player>();
            
        }
    }
}