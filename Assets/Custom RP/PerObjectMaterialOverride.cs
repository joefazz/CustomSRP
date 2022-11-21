using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PerObjectMaterialOverride : MonoBehaviour
{
    static int baseColorId = Shader.PropertyToID("_BaseColor");
    static MaterialPropertyBlock m_Block;
    
    [SerializeField]
    Color m_BaseColor = Color.white;
    
    void Awake()
    {
        
    }

    void OnValidate()
    {
        if (m_Block == null) {
            m_Block = new MaterialPropertyBlock();
        }
        m_Block.SetColor(baseColorId, m_BaseColor);
        GetComponent<Renderer>().SetPropertyBlock(m_Block);
    }
}
