using System;
using UnityEngine;

namespace CreatureParts
{
    public class FiredProjectile : MonoBehaviour
    {
        #region events and variables
        
        public event Action OnProjectileHit;
        
        public GameObject firingPlayer;
        
        private bool canCollide = false;
        
        #endregion

        #region Built in Methods

        void Start()
        {
            Invoke(nameof(EnableCollision), 0.05f); //50 milliseconds
            Destroy(gameObject, 5f);
        }

        #endregion

        #region private methods
        
        private void EnableCollision()
        {
            canCollide = true;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!canCollide) return;

            if (collision.gameObject.GetComponent<CreaturePart>() != null)
            {
                OnProjectileHit?.Invoke();
            }
            
            Destroy(gameObject);
        }
        
        #endregion
    }
}
