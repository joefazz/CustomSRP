using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CustomSRP
{
    public class Lighting
    {
        const string bufferName = "Lighting";
        const int maxDirLightCount = 4;
        Shadows shadows = new Shadows();

        static int dirLightColorsId = Shader.PropertyToID("_DirectionalLightColors"),
            dirLightDirectionsId = Shader.PropertyToID("_DirectionalLightDirections"),
            dirLightCountId = Shader.PropertyToID("_DirectionalLightCount"),
            dirLightShadowDataId = Shader.PropertyToID("_DirectionalLightShadowData");

        static Vector4[] dirLightColors = new Vector4[maxDirLightCount];
        static Vector4[] dirLightDirections = new Vector4[maxDirLightCount];
        static Vector4[] dirLightShadowData = new Vector4[maxDirLightCount];

        public CullingResults cullingResults;

        CommandBuffer m_Buffer = new CommandBuffer()
        {
            name = bufferName
        };

        public void Setup(ScriptableRenderContext context, CullingResults cullingResults, ShadowSettings shadowSettings)
        {
            this.cullingResults = cullingResults;
            m_Buffer.BeginSample(bufferName);
            shadows.Setup(context, cullingResults, shadowSettings);
            SetupLights();
            shadows.Render();
            m_Buffer.EndSample(bufferName);
            context.ExecuteCommandBuffer(m_Buffer);
            m_Buffer.Clear();
        }

        void SetupLights()
        {
            NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;

            int dirLightCount = 0;
            for (int i = 0; i < visibleLights.Length; i++)
            {
                VisibleLight light = visibleLights[i];
                if (light.lightType != LightType.Directional)
                    continue;

                SetupDirectionalLight(dirLightCount++, ref light);

                if (dirLightCount >= maxDirLightCount)
                    break;
            }

            m_Buffer.SetGlobalInt(dirLightCountId, visibleLights.Length);
            m_Buffer.SetGlobalVectorArray(dirLightShadowDataId, dirLightShadowData);
            m_Buffer.SetGlobalVectorArray(dirLightColorsId, dirLightColors);
            m_Buffer.SetGlobalVectorArray(dirLightDirectionsId, dirLightDirections);
        }

        void SetupDirectionalLight(int index, ref VisibleLight visibleLight)
        {
            dirLightColors[index] = visibleLight.finalColor;
            dirLightDirections[index] = -visibleLight.localToWorldMatrix.GetColumn(2);
            dirLightShadowData[index] = shadows.ReserveDirectionalShadows(visibleLight.light, index);
        }

        public void Cleanup()
        {
            shadows.Cleanup();
        }
    }
}
