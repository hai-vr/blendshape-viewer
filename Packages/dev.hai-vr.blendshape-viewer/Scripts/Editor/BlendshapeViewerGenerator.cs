using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

namespace Hai.BlendshapeViewer.Scripts.Editor
{
    internal class BlendshapeViewerDiff
    {
        private static readonly int ShaderHotspots = Shader.PropertyToID("_Hotspots");
        private static readonly int ShaderNeutralTex = Shader.PropertyToID("_NeutralTex");
        private static readonly int ShaderRect = Shader.PropertyToID("_Rect");
        private bool _useComputeShader;
        private Material _material;
        private BlendshapeViewerDiffCompute _diffCompute;

        public void Begin()
        {
            _useComputeShader = SystemInfo.supportsComputeShaders;
            _material = new Material(_useComputeShader ? Shader.Find("Hai/BlendshapeViewerRectOnly") : Shader.Find("Hai/BlendshapeViewer"));

            if (_useComputeShader)
            {
                _diffCompute = new BlendshapeViewerDiffCompute();
            }
        }

        public void Diff(Texture2D source, Texture2D neutralTexture, Texture2D newTexture, RenderTexture renderTexture, float showHotspots)
        {
            _material.SetFloat(ShaderHotspots, showHotspots);
            _material.SetTexture(ShaderNeutralTex, neutralTexture);
            if (_useComputeShader)
            {
                _material.SetVector(ShaderRect, _diffCompute.Compute(source, neutralTexture));
            }
            Graphics.Blit(source, renderTexture, _material);
            BlendshapeViewerGenerator.RenderTextureTo(renderTexture, newTexture);
        }

        public void Terminate()
        {
            Object.DestroyImmediate(_material);
            if (_useComputeShader)
            {
                _diffCompute.Terminate();
            }
        }
    }
    
    internal class BlendshapeViewerGenerator
    {
        private Camera _camera;

        public void Begin()
        {
            _camera = new GameObject().AddComponent<Camera>();

            var sceneCamera = SceneView.lastActiveSceneView.camera;
            _camera.transform.position = sceneCamera.transform.position;
            _camera.transform.rotation = sceneCamera.transform.rotation;
            var whRatio = (1f * sceneCamera.pixelWidth / sceneCamera.pixelHeight);
            _camera.fieldOfView = whRatio < 1 ? sceneCamera.fieldOfView * whRatio : sceneCamera.fieldOfView;
            _camera.orthographic = sceneCamera.orthographic;
            _camera.nearClipPlane = sceneCamera.nearClipPlane;
            _camera.farClipPlane = sceneCamera.farClipPlane;
            _camera.orthographicSize = sceneCamera.orthographicSize;
            _camera.allowMSAA = true;
        }

        public void Terminate()
        {
            Object.DestroyImmediate(_camera.gameObject);
        }

        public void Render(BlendshapeViewerEditorWindow.BlendshapeState blendshapeToRender, Texture2D element, RenderTexture rt)
        {
            try
            {
                Profiler.BeginSample("BlendshapeViewer.BeginSampling");
                if (blendshapeToRender.index != -1)
                {
                    blendshapeToRender.skinnedMesh.SetBlendShapeWeight(blendshapeToRender.index, blendshapeToRender.desiredWeightForCapture);
                    blendshapeToRender.skinnedMesh.enabled = false; // This forces the SkinnedMeshRenderer to update its skinning. I don't know if there's a better way to do that.
                    blendshapeToRender.skinnedMesh.enabled = true; //
                }
                Profiler.EndSample();

                Profiler.BeginSample("BlendshapeViewer.RenderCamera");
                RenderCamera(rt, _camera);
                Profiler.EndSample();
                
                Profiler.BeginSample("BlendshapeViewer.RenderTextureTo");
                RenderTextureTo(rt, element);
                Profiler.EndSample();
            }
            finally
            {
                if (blendshapeToRender.index != -1)
                {
                    blendshapeToRender.skinnedMesh.SetBlendShapeWeight(blendshapeToRender.index, blendshapeToRender.initialWeight);
                    blendshapeToRender.skinnedMesh.enabled = blendshapeToRender.isSmrEnabled;
                }
            }
        }

        private static void RenderCamera(RenderTexture renderTexture, Camera camera)
        {
            var originalRenderTexture = camera.targetTexture;
            var originalAspect = camera.aspect;
            try
            {
                camera.targetTexture = renderTexture;
                camera.aspect = (float) renderTexture.width / renderTexture.height;
                camera.Render();
            }
            finally
            {
                camera.targetTexture = originalRenderTexture;
                camera.aspect = originalAspect;
            }
        }

        internal static void RenderTextureTo(RenderTexture renderTexture, Texture2D texture2D)
        {
            RenderTexture.active = renderTexture;
            texture2D.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
            texture2D.Apply();
            RenderTexture.active = null;
        }
    }
}