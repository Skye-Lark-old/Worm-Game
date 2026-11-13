using System;
using UnityEngine;

namespace CreatureParts
{
    public class SkinnedMeshBaker : MonoBehaviour
    {
        public enum BakeMode
        {
            OnceOnStart, // Bake once, never update
            EveryAnimationFrame // Rebake when animation advances
        }

        [Header("Settings")] 
        #region settings
        public BakeMode bakeMode = BakeMode.OnceOnStart;
        public bool bakeCollider = true;
        public bool bakeOutlineMesh = false;
        public Vector3 meshScale = Vector3.one;
        
        #endregion
        
        [Header("References")]
        public SkinnedMeshRenderer skinnedMeshRenderer;

        #region variables
        public Animator animator;

        private MeshCollider meshCollider;
        private MeshFilter outlineMeshFilter;
        private int lastAnimatorHash;
        
        #endregion
        
        #region Built in Methods

        private void Awake()
        {
            if (skinnedMeshRenderer == null)
                skinnedMeshRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            if (skinnedMeshRenderer == null)
            {
                Debug.LogWarning($"[SkinnedMeshBaker] No SkinnedMeshRenderer found on {gameObject.name}");
                enabled = false;
                return;
            }

            if (bakeCollider)
            {
                meshCollider = GetComponent<MeshCollider>();
                if (meshCollider == null)
                    meshCollider = gameObject.AddComponent<MeshCollider>();
            }

            Bake();

            if (bakeMode == BakeMode.OnceOnStart)
                enabled = false;
        }
        
        #endregion

        #region Public methods

        public void EnableConvex()
        {
            //TODO: call this from creatureBuilding after multiplayer has been merged
            meshCollider.convex = true;
        }

        public void DisableConvex()
        {
            //TODO: call this from creatureBuilding after multiplayer has been merged
            meshCollider.convex = false;
        }

        public void Bake()
        {
            if (skinnedMeshRenderer == null) return;

            Mesh bakedMesh = new Mesh();
            skinnedMeshRenderer.BakeMesh(bakedMesh);
            Debug.Log($"RAW baked bounds: {bakedMesh.bounds}, first vertex: {bakedMesh.vertices[0]}");
            
            Quaternion smrRot = skinnedMeshRenderer.transform.rotation;
            Quaternion rootInvRot = Quaternion.Inverse(transform.rotation);
            Vector3 posOffset = transform.InverseTransformPoint(skinnedMeshRenderer.transform.position);

            Vector3[] vertices = bakedMesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = rootInvRot * (smrRot * vertices[i]) + posOffset;
                vertices[i].x *= meshScale.x;
                vertices[i].y *= meshScale.y;
                vertices[i].z *= meshScale.z;
            }
            bakedMesh.vertices = vertices;
            bakedMesh.RecalculateBounds();
            bakedMesh.RecalculateNormals();

            Debug.Log($"[SkinnedMeshBaker] After transform - bounds: {bakedMesh.bounds}");

            if (bakeCollider && meshCollider != null)
            {
                meshCollider.sharedMesh = null;
                meshCollider.sharedMesh = bakedMesh;
                Debug.Log($"[SkinnedMeshBaker] Collider world bounds: {meshCollider.bounds}");
            }
        }
        
        #endregion

        #region Private Methods

        private void OnDrawGizmosSelected()
        {
            var col = GetComponent<MeshCollider>();
            if (col == null || col.sharedMesh == null) return;

            Gizmos.color = Color.green;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireMesh(col.sharedMesh);
        }
        
        private void OnValidate()
        {
            if (!Application.isPlaying) return;
            if (skinnedMeshRenderer == null)
                skinnedMeshRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
            if (meshCollider == null)
                meshCollider = GetComponent<MeshCollider>();
            Bake();
        }
        
        #endregion
    }
}