using CustomSRP;
using Palmmedia.ReportGenerator.Core;
using UnityEditor;
using UnityEditor.Rendering.LookDev;
using UnityEngine;
using UnityEngine.Rendering;

public class Shadows
{
    const string bufferName = "Shadows";
    const int maxShadowsDirectionalLightCount = 4, maxCascades = 4;
    static int dirShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas");
    static int dirShadowMatricesId = Shader.PropertyToID("_DirectionalShadowMatrices");
    static int cascadeCountId = Shader.PropertyToID("_CascadeCount");
    static int cascadeCullingSpheresId = Shader.PropertyToID("_CascadeCullingSpheres");
    static int shadowDistanceFadeId = Shader.PropertyToID("_ShadowDistanceFade");
    static int shadowAtlasSizeId = Shader.PropertyToID("_ShadowAtlasSize");

    static int cascadeDataId = Shader.PropertyToID("_CascadeData");
    static string[] directionalFilterKeywords = {
        "_DIRECTIONAL_PCF3",
        "_DIRECTIONAL_PCF5",
        "_DIRECTIONAL_PCF7",
    };
    static string[] cascadeBlendKeywords = {
        "_CASCADE_BLEND_SOFT",
        "_CASCADE_BLEND_DITHER"
    };
    
    static Vector4[] cascadeCullingSpheres = new Vector4[maxCascades], cascadeData = new Vector4[maxCascades];
    static Matrix4x4[] dirShadowMatrices = new Matrix4x4[maxShadowsDirectionalLightCount * maxCascades];

    int m_ShadowedDirectionalLightCount;
    CommandBuffer m_Buffer = new CommandBuffer()
    {
        name = bufferName
    };

    struct ShadowedDirectionalLight
    {
        public int visibleLightIndex;
        public float slopeScaleBias;
        public float nearPlaneOffset;
    }

    ShadowedDirectionalLight[] m_ShadowedDirectionalLights = new ShadowedDirectionalLight[maxShadowsDirectionalLightCount];

    ScriptableRenderContext m_Context;
    CullingResults m_CullingResults;
    ShadowSettings m_ShadowSettings;

    public void Setup(ScriptableRenderContext context, CullingResults cullingResults, ShadowSettings shadowSettings)
    {
        m_Context = context;
        m_CullingResults = cullingResults;
        m_ShadowSettings = shadowSettings;

        m_ShadowedDirectionalLightCount = 0;
    }

    void ExecuteBuffer()
    {
        m_Context.ExecuteCommandBuffer(m_Buffer);
        m_Buffer.Clear();
    }

    public Vector3 ReserveDirectionalShadows(Light light, int visibleLightIndex)
    {
        if (m_ShadowedDirectionalLightCount == maxShadowsDirectionalLightCount
            || light.shadows == LightShadows.None
            || light.shadowStrength == 0f
            || !m_CullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds b))
            return Vector3.zero;

        m_ShadowedDirectionalLights[m_ShadowedDirectionalLightCount] = new ShadowedDirectionalLight()
        {
            visibleLightIndex = visibleLightIndex,
            slopeScaleBias = light.shadowBias,
            nearPlaneOffset = light.shadowNearPlane
        };

        return new Vector3(light.shadowStrength, m_ShadowSettings.directional.cascadeCount * m_ShadowedDirectionalLightCount++, light.shadowNormalBias);
    }

    public void Render()
    {
        if (m_ShadowedDirectionalLightCount == 0) return;

        RenderDirectionalShadows();
    }

    void RenderDirectionalShadows()
    {
        int atlasSize = (int) m_ShadowSettings.directional.atlasSize;
        m_Buffer.GetTemporaryRT(dirShadowAtlasId, atlasSize, atlasSize, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
        m_Buffer.SetRenderTarget(dirShadowAtlasId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        m_Buffer.ClearRenderTarget(true, false, Color.clear);
        m_Buffer.BeginSample(bufferName);
        ExecuteBuffer();

        int tiles = m_ShadowedDirectionalLightCount * m_ShadowSettings.directional.cascadeCount;
        int split = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;
        int tileSize = atlasSize / split;

        for (int i = 0; i < m_ShadowedDirectionalLightCount; i++)
        {
            RenderDirectionalShadows(i, split, tileSize);
        }

        float f = 1f - m_ShadowSettings.directional.cascadeFade;
        m_Buffer.SetGlobalVector(shadowDistanceFadeId, new Vector4(1f / m_ShadowSettings.MaxDistance, 1f / m_ShadowSettings.distanceFade, 1f / (1f - f * f)));
        m_Buffer.SetGlobalInt(cascadeCountId, m_ShadowSettings.directional.cascadeCount);
        m_Buffer.SetGlobalVectorArray(cascadeCullingSpheresId, cascadeCullingSpheres);
        m_Buffer.SetGlobalVectorArray(cascadeDataId, cascadeData);
        m_Buffer.SetGlobalMatrixArray(dirShadowMatricesId, dirShadowMatrices);
        m_Buffer.SetGlobalVector(shadowAtlasSizeId, new Vector4(atlasSize, 1f / atlasSize));
        SetKeywords(directionalFilterKeywords, (int)m_ShadowSettings.directional.filter - 1);
        SetKeywords(cascadeBlendKeywords, (int)m_ShadowSettings.directional.cascadeBlend - 1);
        m_Buffer.EndSample(bufferName);
        ExecuteBuffer();
    }

    void SetKeywords(string[] keywords, int enabledIndex)
    {
        for (int i = 0; i < keywords.Length; i++) {
            if (i == enabledIndex) {
                m_Buffer.EnableShaderKeyword(keywords[i]);
            }
            else {
                m_Buffer.DisableShaderKeyword(keywords[i]);
            }
        }
    }

    void RenderDirectionalShadows(int index, int split, int tileSize)
    {
        ShadowedDirectionalLight light = m_ShadowedDirectionalLights[index];
        var shadowSettings = new ShadowDrawingSettings(m_CullingResults, light.visibleLightIndex);
        int cascadeCount = m_ShadowSettings.directional.cascadeCount;
        int tileOffset = index * cascadeCount;
        Vector3 ratios = m_ShadowSettings.directional.CascadeRatios;

        float cullingFactor = Mathf.Max(0f, 0.8f - m_ShadowSettings.directional.cascadeFade);
        
        for (int i = 0; i < cascadeCount; i++)
        {
            m_CullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(
                light.visibleLightIndex,
                i,
                cascadeCount,
                ratios,
                tileSize,
                light.nearPlaneOffset,
                out Matrix4x4 viewMatrix,
                out Matrix4x4 projectionMatrix,
                out ShadowSplitData splitData);

            splitData.shadowCascadeBlendCullingFactor = cullingFactor;
            shadowSettings.splitData = splitData;
            if (index == 0) {
                cascadeCullingSpheres[i] = splitData.cullingSphere;
            }

            Vector4 cullingSphere = splitData.cullingSphere;
            SetCascadeData(i, cullingSphere, tileSize);
            
            int tileIndex = tileOffset + i;
            var offset = SetTileViewport(tileIndex, split, tileSize);
            dirShadowMatrices[tileIndex] = ConvertToAtlasMatrix(projectionMatrix * viewMatrix, offset, split);
            m_Buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
            m_Buffer.SetGlobalDepthBias(0f, light.slopeScaleBias);
            ExecuteBuffer();
            m_Context.DrawShadows(ref shadowSettings);
            m_Buffer.SetGlobalDepthBias(0f, 0f);
        }
    }

    Vector2 SetTileViewport(int index, int split, float tileSize)
    {
        Vector3 offset = new Vector2(index % split, index / split);
        m_Buffer.SetViewport(new Rect(offset.x * tileSize, offset.y * tileSize, tileSize, tileSize));
        return offset;
    }

    void SetCascadeData(int index, Vector4 cullingSphere, float tileSize)
    {
        float texelSize = 2f * cullingSphere.w / tileSize;
        float filterSize = texelSize * ((float) m_ShadowSettings.directional.filter + 1f);
        cullingSphere.w *= cullingSphere.w;
        cascadeCullingSpheres[index] = cullingSphere;
        cascadeData[index] = new Vector4(1f / cullingSphere.w, filterSize * 1.4142136f);
    }

    public void Cleanup()
    {
        m_Buffer.ReleaseTemporaryRT(dirShadowAtlasId);
        ExecuteBuffer();
    }

    Matrix4x4 ConvertToAtlasMatrix(Matrix4x4 m, Vector2 offset, int split)
    {
        if (SystemInfo.usesReversedZBuffer)
        {
            m.m20 = -m.m20;
            m.m21 = -m.m21;
            m.m22 = -m.m22;
            m.m23 = -m.m23;
        }

        float scale = 1f / split;
        m.m00 = (0.5f * (m.m00 + m.m30) + offset.x * m.m30) * scale;
        m.m01 = (0.5f * (m.m01 + m.m31) + offset.x * m.m31) * scale;
        m.m02 = (0.5f * (m.m02 + m.m32) + offset.x * m.m32) * scale;
        m.m03 = (0.5f * (m.m03 + m.m33) + offset.x * m.m33) * scale;
        m.m10 = (0.5f * (m.m10 + m.m30) + offset.y * m.m30) * scale;
        m.m11 = (0.5f * (m.m11 + m.m31) + offset.y * m.m31) * scale;
        m.m12 = (0.5f * (m.m12 + m.m32) + offset.y * m.m32) * scale;
        m.m13 = (0.5f * (m.m13 + m.m33) + offset.y * m.m33) * scale;
        m.m20 = 0.5f * (m.m20 + m.m30);
        m.m21 = 0.5f * (m.m21 + m.m31);
        m.m22 = 0.5f * (m.m22 + m.m32);
        m.m23 = 0.5f * (m.m23 + m.m33);

        return m;
    }
}
