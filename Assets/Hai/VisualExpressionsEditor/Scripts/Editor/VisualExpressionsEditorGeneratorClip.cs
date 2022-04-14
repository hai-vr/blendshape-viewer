using UnityEditor;
using UnityEngine;

namespace Hai.VisualExpressionsEditor.Scripts.Editor
{
    public class VisualExpressionsEditorGeneratorClip
    {
        private GameObject _animatedRoot;
        private Camera _camera;

        public void Begin(GameObject animatedRoot)
        {
            _animatedRoot = animatedRoot;

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
        }

        public void ParentCameraTo(Transform newParent)
        {
            _camera.transform.parent = newParent;
        }

        public void Terminate()
        {
            Object.DestroyImmediate(_camera.gameObject);
        }

        public void Render(AnimationClip clip, Texture2D element, float normalizedTime)
        {
            var initPos = _animatedRoot.transform.position;
            var initRot = _animatedRoot.transform.rotation;
            try
            {
                AnimationMode.StartAnimationMode();
                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(_animatedRoot.gameObject, clip, normalizedTime * clip.length);
                AnimationMode.EndSampling();
                // This is a workaround for an issue where for some reason, the animator moves to the origin
                // after sampling despite the animation having no RootT/RootQ properties.
                _animatedRoot.transform.position = initPos;
                _animatedRoot.transform.rotation = initRot;

                var renderTexture = RenderTexture.GetTemporary(element.width, element.height, 24);
                renderTexture.wrapMode = TextureWrapMode.Clamp;

                RenderCamera(renderTexture, _camera);
                RenderTextureTo(renderTexture, element);
                RenderTexture.ReleaseTemporary(renderTexture);
            }
            finally
            {
                AnimationMode.StopAnimationMode();
                _animatedRoot.transform.position = initPos;
                _animatedRoot.transform.rotation = initRot;
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

        private static void RenderTextureTo(RenderTexture renderTexture, Texture2D texture2D)
        {
            RenderTexture.active = renderTexture;
            texture2D.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
            texture2D.Apply();
            RenderTexture.active = null;
        }
    }
}