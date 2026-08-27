using UnityEngine;

[ExecuteAlways]
public class SkyboxController : MonoBehaviour
{
    private static readonly int SunDirID = Shader.PropertyToID("_SunDir");
    private static readonly int MoonDirID = Shader.PropertyToID("_MoonDir");
    private static readonly int MoonSpaceMatrixID = Shader.PropertyToID("_MoonSpaceMatrix");

    [SerializeField] private Transform _Sun;
    [SerializeField] private Transform _Moon;
        
    void LateUpdate()
    {
        if (_Sun != null)
        {
            Shader.SetGlobalVector(SunDirID, -_Sun.transform.forward);
        }

        if (_Moon != null)
        {
            Shader.SetGlobalVector(MoonDirID, -_Moon.transform.forward);
            Shader.SetGlobalMatrix(MoonSpaceMatrixID, new Matrix4x4(-_Moon.transform.forward, _Moon.transform.up, -_Moon.transform.right, Vector4.zero).transpose);
        }
    }
}