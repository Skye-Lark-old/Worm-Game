using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Player;

namespace CreatureParts
{ 
    public class ProjectilePart : AttachablePart 
    { 
        #region events and variables
        public event Action OnCannonShoot;
        
        public GameObject projectilePrefab;
        public Transform firePoint;
        public float recoilForce = 0.25f;
        public float fireCooldown = 0.5f;
        public float shootForce = 0.03f;
        public KeyCode shootKey = KeyCode.R;
        
        [SerializeField] private Animator animator;
        
        private float lastFireTime;
        private Rigidbody wormRb;
        
        #endregion

        #region Built in methods
        
        
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
            if (transform.parent != null &&
                Input.GetKeyDown(shootKey) &&
                Time.time >= lastFireTime + fireCooldown)
            {
                if (animator != null)
                {
                    animator.ResetTrigger("PlayAnimation");
                    animator.SetTrigger("PlayAnimation");
                }
                Shoot();
                lastFireTime = Time.time;
            }
        }
        
        #endregion

        #region Private Methods

        private void Shoot()
        {
            OnCannonShoot?.Invoke();
            
            GameObject projectile = Instantiate(
                projectilePrefab,
                firePoint.position,
                firePoint.rotation
            );

            projectile.GetComponent<FiredProjectile>().firingPlayer = GetComponentInParent<Player.Player>().gameObject;

            Rigidbody rb = projectile.GetComponent<Rigidbody>();
            

            if (rb != null)
            {
                rb.AddForce(firePoint.forward * shootForce, ForceMode.Impulse);
            }

            ApplyRecoil();
        }
        
        private void ApplyRecoil()
        {
            if (wormRb == null) return;

            Vector3 recoilDirection = -firePoint.forward;
            wormRb.AddForce(recoilDirection * recoilForce, ForceMode.Impulse);
        }
        
        #endregion
    }
}
