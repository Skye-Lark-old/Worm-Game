using System.Collections;
using PurrNet;
using UnityEngine;

namespace CreatureParts
{
    public class DeadBodyPart : NetworkBehaviour
    {
        
        void Start()
        {
            if (isServer) return;
            
            gameObject.layer = LayerMask.NameToLayer("WormRagdoll");
            gameObject.tag = "Untagged";
            Destroy(gameObject.GetComponent<ConfigurableJoint>());
            gameObject.GetComponent<CreaturePart>().enabled = false;
            
            Rigidbody rb = GetComponent<Rigidbody>();
            rb.linearDamping = GameParameters.DeadPartLinearDamping;
            rb.mass = GameParameters.DeadPartMass;
            //StartCoroutine(SelfDestruct());
        }
        
        void OnCollisionStay(Collision col)
        {
            if (col.gameObject.layer == gameObject.layer)
            {
                float softness = GameParameters.DeadPartVelocityReduction;
                Vector3 normal = col.contacts[0].normal;
                float normalVelocity = Vector3.Dot(GetComponent<Rigidbody>().linearVelocity, normal);
        
                if (normalVelocity < 0)
                {
                    GetComponent<Rigidbody>().linearVelocity -= normal * normalVelocity * softness;
                }
            }
        }

        // private IEnumerator SelfDestruct()
        // {
        //     yield return new WaitForSeconds(GameParameters.DeadPartDeleteTime);
        //     Destroy(gameObject);
        // }
    }
}
