using UnityEditor.PackageManager.UI;
using UnityEngine;
using UnityEngine.Rendering;

namespace CustomSRP
{
    public partial class CustomCameraRenderer
    {
        ScriptableRenderContext m_Context;
        Camera m_Camera;
        public CullingResults cullingResults;
        static Material m_ErrorMaterial;
        static readonly ShaderTagId UnlitShaderTagId = new ShaderTagId("SRPDefaultUnlit");
        static readonly ShaderTagId LitShaderTagId = new ShaderTagId("CustomLit");
        CommandBuffer m_Buffer = new CommandBuffer();
        Lighting lighting = new Lighting();

        public void Render(ScriptableRenderContext context, Camera camera, bool useDynamicBatching, bool useGPUInstancing, ShadowSettings shadowSettings)
        {
            m_Context = context;
            m_Camera = camera;

            PrepareBuffer();
            PrepareForSceneWindow();

            if (!Cull(shadowSettings.MaxDistance))
            {
                return;
            }

            m_Buffer.BeginSample(SampleName);
            ExecuteBuffer();
            lighting.Setup(context, cullingResults, shadowSettings);
            m_Buffer.EndSample(SampleName);
            SetupForCamera();
            DrawVisibleGeometry(useDynamicBatching, useGPUInstancing);
            DrawUnsupportedShaders();
            DrawGizmos();
            lighting.Cleanup();
            SubmitToGPU();
        }

        partial void DrawUnsupportedShaders();
        partial void DrawGizmos();

        partial void PrepareForSceneWindow();

        private void SetupForCamera()
        {
            m_Context.SetupCameraProperties(m_Camera);
            CameraClearFlags clearFlags = m_Camera.clearFlags;
            m_Buffer.ClearRenderTarget(clearFlags <= CameraClearFlags.Depth, clearFlags == CameraClearFlags.Color, clearFlags == CameraClearFlags.Color ? m_Camera.backgroundColor.linear : Color.clear);
            m_Buffer.BeginSample(SampleName);
            ExecuteBuffer();
            // m_Context.SetupCameraProperties(m_Camera);
        }

        void ExecuteBuffer()
        {
            m_Context.ExecuteCommandBuffer(m_Buffer);
            m_Buffer.Clear();
        }

        bool Cull(float maxShadowDistance)
        {
            if (m_Camera.TryGetCullingParameters(out ScriptableCullingParameters cullingParams))
            {
                cullingParams.shadowDistance = Mathf.Min(maxShadowDistance, m_Camera.farClipPlane);
                
                cullingResults = m_Context.Cull(ref cullingParams);
                return true;
            }

            Debug.LogError("Could not get culling parameters");
            return false;
        }

        private void DrawVisibleGeometry(bool useDynamicBatching, bool useGPUInstancing)
        {
            var sortingSettings = new SortingSettings(m_Camera)
            {
                criteria = SortingCriteria.CommonOpaque
            };

            var drawingSettings = new DrawingSettings(UnlitShaderTagId, sortingSettings)
            {
                enableDynamicBatching = useDynamicBatching,
                enableInstancing = useGPUInstancing
            };
            drawingSettings.SetShaderPassName(1, LitShaderTagId);

            var filteringSettings = new FilteringSettings(RenderQueueRange.opaque);

            // Draw opaques
            m_Context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);

            m_Context.DrawSkybox(m_Camera);

            // Transparent pass has to happen after skybox because transparent objects don't write to the depth
            // buffer so the skybox draws over them
            sortingSettings.criteria = SortingCriteria.CommonTransparent;
            drawingSettings.sortingSettings = sortingSettings;
            filteringSettings.renderQueueRange = RenderQueueRange.transparent;

            m_Context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
        }

        private void SubmitToGPU()
        {
            m_Buffer.EndSample(SampleName);
            ExecuteBuffer();
            m_Context.Submit();
        }

        partial void PrepareBuffer();
    }
}
