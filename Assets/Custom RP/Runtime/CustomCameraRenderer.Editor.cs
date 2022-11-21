using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace CustomSRP
{
    partial class CustomCameraRenderer
    {
#if UNITY_EDITOR
        static ShaderTagId[] legacyShaderTagIds = {
            new ShaderTagId("Always"),
            new ShaderTagId("ForwardBase"),
            new ShaderTagId("PrepassBase"),
            new ShaderTagId("Vertex"),
            new ShaderTagId("VertexLMRGBM"),
            new ShaderTagId("VertexLM")
        };

        public string SampleName = "Render Camera";

        partial void PrepareBuffer()
        {
            Profiler.BeginSample("Editor Only");
            m_Buffer.name = SampleName = m_Camera.name;
            Profiler.EndSample();
        }

        partial void DrawGizmos()
        {
            if (Handles.ShouldRenderGizmos())
            {
                m_Context.DrawGizmos(m_Camera, GizmoSubset.PreImageEffects);
                m_Context.DrawGizmos(m_Camera, GizmoSubset.PostImageEffects);
            }
        }

        partial void PrepareForSceneWindow()
        {
            if (m_Camera.cameraType == CameraType.SceneView)
                ScriptableRenderContext.EmitWorldGeometryForSceneView(m_Camera);
        }
        
        partial void DrawUnsupportedShaders()
        {
            if (m_ErrorMaterial == null)
                m_ErrorMaterial = new Material(Shader.Find("Hidden/InternalErrorShader"));

            var drawingSettings = new DrawingSettings(legacyShaderTagIds[0], new SortingSettings(m_Camera))
            {
                overrideMaterial = m_ErrorMaterial
            };
            
            for (int i = 1; i < legacyShaderTagIds.Length; i++)
            {
                drawingSettings.SetShaderPassName(i, legacyShaderTagIds[i]);
            }
            var filteringSettings = FilteringSettings.defaultValue;
            
            m_Context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
        }
    }
#endif
}
