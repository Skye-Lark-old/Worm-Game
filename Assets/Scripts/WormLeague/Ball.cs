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
                //test
            }
        }

        public void Reset()
        {
            Vector3 target = new Vector3(0f, 2f, 3f);
            
            PredictedRigidbody rigidBody = gameObject.GetComponent<PredictedRigidbody>();
            rigidBody.angularVelocity = Vector3.zero;
            rigidBody.linearVelocity  = Vector3.zero;
            rigidBody.rotation        = Quaternion.identity;
            rigidBody.position        = target;
            
            transform.SetPositionAndRotation(target, Quaternion.identity);
            Physics.SyncTransforms();
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