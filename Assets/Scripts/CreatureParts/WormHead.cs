using UnityEngine;

namespace CreatureParts
{
    public class WormHead : CreaturePart
    {
        public GameObject visualHead;
        public GameObject wormVisualHeadWithMaterial;
        
        public new void SetMaterial(Material material)
        {
            wormVisualHeadWithMaterial.GetComponent<MeshRenderer>().material = material;
        }
        
    }
}
