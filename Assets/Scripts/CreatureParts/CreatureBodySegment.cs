using System.Collections;
using Unity.VisualScripting;
using UnityEngine;

namespace CreatureParts
{
    public class CreatureBodySegment : CreaturePart
    {
        #region public variables
        [Header("Public Variables")]
        public bool IsScrunched => currentState.ScrunchTimeLeft > 0f;
        public GameObject visualBodySegment;

        public CreaturePart previousSegment;
        public CreaturePart nextSegment;
        
        #endregion
        
        #region private variables
        [Header("Private Variables")]

        private Coroutine scrunchCoroutine;
        
        #endregion
        
        #region public methods
        
        public void SetIsScrunched()
        {
            ref var s = ref currentState;
            s.ScrunchTimeLeft = GameParameters.WormSegmentScrunchTime;
        }

        public Rigidbody AddJoint(Transform wormPart, Rigidbody previousSegmentRigidBody)
        {
            ConfigurableJoint existingJoint = wormPart.GetComponent<ConfigurableJoint>();
            if (existingJoint != null)
                Destroy(existingJoint);
                
            ConfigurableJoint joint = wormPart.AddComponent<ConfigurableJoint>();
            joint.connectedBody = previousSegmentRigidBody;
            joint.anchor = new Vector3(0, 0, -GameParameters.SegmentMaxPartDistance);
            
            joint.xMotion = joint.yMotion = joint.zMotion = ConfigurableJointMotion.Locked;
            joint.angularXMotion = joint.angularYMotion = joint.angularZMotion = ConfigurableJointMotion.Limited;

            float maxAngle = GameParameters.MaxJointAngle;
            
            joint.lowAngularXLimit = CreateLimit(-maxAngle);
            joint.highAngularXLimit = CreateLimit(maxAngle);
            joint.angularYLimit = CreateLimit(maxAngle * 0.05f); // Reduced twisting
            joint.angularZLimit = CreateLimit(maxAngle);
            
            SoftJointLimitSpring limitSpring = new SoftJointLimitSpring
            {
                spring = 1000000f,
                damper = 10000f
            };
            joint.angularXLimitSpring = joint.angularYZLimitSpring = limitSpring;
            
            JointDrive angularDrive = new JointDrive
            {
                positionSpring = 0f,
                positionDamper = 100f,
                maximumForce = 1000f
            };
            joint.angularXDrive = joint.angularYZDrive = angularDrive;
            joint.rotationDriveMode = RotationDriveMode.XYAndZ;

            return wormPart.GetComponent<Rigidbody>();
        }

        public new void SetMaterial(Material material)
        {
            foreach (MeshRenderer renderer in GetComponentsInChildren<MeshRenderer>())
            {
                renderer.material = material;
            }
        }
        
        public void ResetJointPhysics()
        {
            ConfigurableJoint joint = GetComponent<ConfigurableJoint>();
            if (joint == null) return;

            Rigidbody rb = GetComponent<Rigidbody>();
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            joint.targetPosition = Vector3.zero;
            joint.targetVelocity = Vector3.zero;
            joint.targetAngularVelocity = Vector3.zero;
            joint.targetRotation = Quaternion.identity;
        }
        
        #endregion
        
        #region private methods

        private SoftJointLimit CreateLimit(float angle)
        {
            return new SoftJointLimit
            {
                limit = angle,
                bounciness = 0.1f,
                contactDistance = 0f
            };
        }
        
        #endregion
    }
}
