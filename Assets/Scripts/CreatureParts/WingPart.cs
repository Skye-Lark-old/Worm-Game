using System;
using UnityEngine;
using Player;
using UnityEngine.SocialPlatforms;

namespace CreatureParts
{
    public class WingPart : AttachablePart
    {

        #region Events and Variables
        public event Action OnWingFlap;
        
        [SerializeField] private float baseForce;
        [SerializeField] private float wingForceDebuff;
        [SerializeField] private Transform forcePointA;
        [SerializeField] private Transform forcePointB;
        [SerializeField] private Animator animator;

        private Rigidbody wormRb;
        private float currentForce;
        private Vector3 savedLaunchDirection;
        
        #endregion
        
        #region Built in Methods

        private void Awake()
        {
            if (LocalPlayer.Instance != null)
            {
                wormRb = LocalPlayer.Instance.wormHead.GetComponent<Rigidbody>();
            }
            
            base.Awake();
        }

        void Update()
        {
            if (LocalPlayer.Instance.IsWormGrounded)
            {
                currentForce = baseForce;
                savedLaunchDirection = (forcePointB.position - forcePointA.position).normalized;
            }
            
            if (Input.GetKeyDown(KeyCode.Space))
            {
                if (predictionManager.localPlayer != owner) return;

                if (animator != null)
                {
                    animator.ResetTrigger("PlayAnimation");
                    animator.SetTrigger("PlayAnimation");
                }
                ApplyThrust();
            }
        }
        
        #endregion
        
        #region private methods

        private void ApplyThrust()
        {
            if (wormRb == null || savedLaunchDirection == null) return;
            
            OnWingFlap?.Invoke();
            
            if (currentForce >= 0)
            {
                wormRb.AddForce(savedLaunchDirection * currentForce, ForceMode.Force);
                currentForce = currentForce - wingForceDebuff;
                if (currentForce < 0)
                {
                    currentForce = 0;
                }
            }
        }
        
        #endregion
    }
}
