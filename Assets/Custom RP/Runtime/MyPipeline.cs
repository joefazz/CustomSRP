using UnityEngine;
using UnityEngine.Rendering;

namespace CustomSRP
{
    public class MyPipeline : RenderPipeline
    {
        CustomCameraRenderer m_Renderer = new CustomCameraRenderer();
        public bool useGPUInstancing;
        public bool useDynamicBatching;
        public ShadowSettings shadowSettings;

        public MyPipeline(bool useDynamicBatching, bool useGPUInstancing, bool useSRPBatcher, ShadowSettings shadowSettings)
        {
            this.useDynamicBatching = useDynamicBatching;
            this.useGPUInstancing = useGPUInstancing;
            GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBatcher;
            GraphicsSettings.lightsUseLinearIntensity = true;
            this.shadowSettings = shadowSettings;
        }


        protected override void Render(ScriptableRenderContext context, Camera[] cameras)
        {
            foreach (var camera in cameras)
            {
                m_Renderer.Render(context, camera, useDynamicBatching, useGPUInstancing, shadowSettings);
            }
        }
    }
}
