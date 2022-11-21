using UnityEngine;

public class MeshBall : MonoBehaviour {

    static int baseColorId = Shader.PropertyToID("_BaseColor");
    Matrix4x4[] matrices = new Matrix4x4[1023];
    Vector4[] baseColors = new Vector4[1023];

    MaterialPropertyBlock block;
    
    [SerializeField]
    Mesh mesh = default;

    [SerializeField]
    Material material = default;
    
    void Awake () {
        for (int i = 0; i < matrices.Length; i++) {
            matrices[i] = Matrix4x4.TRS(
                Random.insideUnitSphere * 10f, Quaternion.Euler(Random.value * 360, Random.value * 360, Random.value * 360), Vector3.one * Random.Range(0.5f, 1.5f)
            );
            baseColors[i] =
                new Vector4(Random.value, Random.value, Random.value, 1f);
        }
    }
    
    void Update () {
        if (block == null) {
            block = new MaterialPropertyBlock();
            block.SetVectorArray(baseColorId, baseColors);
        }
        Graphics.DrawMeshInstanced(mesh, 0, material, matrices, 1023, block);
    }
}
