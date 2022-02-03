using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Hai.AnimationViewer.Scripts.Editor
{
    [InitializeOnLoad]
    public class AnimationViewerEditorWindow : EditorWindow
    {
        public Animator animator;
        public bool autoUpdateOnFocus = true;
        public bool continuousUpdates;
        public int updateSpeed = 50;
        public bool advanced;
        public float normalizedTime;
        public HumanBodyBones focusedBone = HumanBodyBones.Head;
        public AnimationClip basePose;
        public int thumbnailSize = 96;
        public bool updateOnActivate = true;
        private Vector2 _scrollPos;
        // private Animator _generatedFor;
        private int _generatedSize;

        private Vector3 _generatedTransformPosition;
        private Quaternion _generatedTransformRotation;
        private float _generatedFieldOfView;
        private bool _generatedOrthographic;
        private float _generatedNearClipPlane;
        private float _generatedFarClipPlane;
        private float _generatedOrthographicSize;

        public AnimationViewerEditorWindow()
        {
            titleContent = new GUIContent("AnimationViewer");
            EditorApplication.projectWindowItemOnGUI -= DrawAnimationClipItem; // Clear any previously added
            EditorApplication.projectWindowItemOnGUI += DrawAnimationClipItem;

            try
            {
                _projectBrowserType = typeof(EditorWindow).Assembly.GetType("UnityEditor.ProjectBrowser");
                _projectBrowserListAreaField = _projectBrowserType.GetField("m_ListArea", BindingFlags.NonPublic | BindingFlags.Instance);

                var listAreaType = typeof(EditorWindow).Assembly.GetType("UnityEditor.ObjectListArea");
                _listAreaGridSizeField = listAreaType.GetProperty("gridSize", BindingFlags.Public | BindingFlags.Instance);

                _thumbnailSizeFeatureAvailable = true;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void Update()
        {
            if (!_enabled) return;
            if (!autoUpdateOnFocus) return;
            if (animator == null) return;
            if (!HasGenerationParamsChanged()) return;

            Invalidate();
            if (continuousUpdates)
            {
                EditorApplication.RepaintProjectWindow();
            }
        }

        private void OnGUI()
        {
            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(Screen.height - EditorGUIUtility.singleLineHeight));
            var serializedObject = new SerializedObject(this);
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(animator)));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(autoUpdateOnFocus)));
            EditorGUI.BeginDisabledGroup(!autoUpdateOnFocus);
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(continuousUpdates)));
            if (autoUpdateOnFocus && continuousUpdates)
            {
                EditorGUILayout.HelpBox("Continuous Updates will cause a dramatic slow down.\nDisable it when not in use.", MessageType.Warning);
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.IntSlider(serializedObject.FindProperty(nameof(updateSpeed)), 1, 100);
            if (_thumbnailSizeFeatureAvailable)
            {
                var thumbnailSizeSerialized = serializedObject.FindProperty(nameof(thumbnailSize));
                var previousSize = thumbnailSizeSerialized.intValue;
                EditorGUILayout.IntSlider(thumbnailSizeSerialized, 20, 300);
                if (_enabled)
                {
                    var editorWindow = GetWindow(_projectBrowserType, false, null, false);
                    var listArea = _projectBrowserListAreaField.GetValue(editorWindow);
                    _listAreaGridSizeField.SetValue(listArea, thumbnailSize);
                    if (previousSize != thumbnailSizeSerialized.intValue)
                    {
                        EditorApplication.RepaintProjectWindow();
                    }
                }
            }

            EditorGUI.BeginDisabledGroup(animator == null || AnimationMode.InAnimationMode());
            if (ColoredBgButton(_enabled, Color.red, () => GUILayout.Button("Activate Viewer")))
            {
                _enabled = !_enabled;
                if (_enabled && updateOnActivate)
                {
                    Invalidate();
                }
                EditorApplication.RepaintProjectWindow();
            }
            if (_enabled)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Update"))
                {
                    Invalidate();
                    EditorApplication.RepaintProjectWindow();
                }
                if (GUILayout.Button("Force", GUILayout.Width(50)))
                {
                    _projectRenderQueue.ForceClearAll();
                    EditorApplication.RepaintProjectWindow();
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUI.EndDisabledGroup();

            advanced = EditorGUILayout.Foldout(advanced, "Advanced");
            if (advanced)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(focusedBone)));
                EditorGUILayout.Slider(serializedObject.FindProperty(nameof(normalizedTime)), 0f, 1f);
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(basePose)));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(updateOnActivate)));
            }
            else
            {
                if (basePose != null)
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(basePose)));
                }
            }

            if (basePose != null)
            {
                EditorGUILayout.HelpBox("A base pose is specified. This will change the way animations look.", MessageType.Warning);
                if (GUILayout.Button("Discard base pose"))
                {
                    serializedObject.FindProperty(nameof(basePose)).objectReferenceValue = null;
                }
            }
            if (focusedBone != HumanBodyBones.Head)
            {
                if (GUILayout.Button("Reset focused bone to Head"))
                {
                    serializedObject.FindProperty(nameof(focusedBone)).intValue = (int)HumanBodyBones.Head;
                }
            }
            serializedObject.ApplyModifiedProperties();

            if (animator != null)
            {
                _focusedObjectNullable = animator.gameObject;
                _projectRenderQueue.QueueSize(updateSpeed);
                _projectRenderQueue.Bone(focusedBone);
                _projectRenderQueue.NormalizedTime(normalizedTime);
                _projectRenderQueue.BasePose(basePose);
            }
            EditorGUILayout.EndScrollView();
        }

        private void SaveGenerationParams()
        {
            // _generatedFor = animator;

            var sceneCamera = SceneView.lastActiveSceneView.camera;
            _generatedTransformPosition = sceneCamera.transform.position;
            _generatedTransformRotation = sceneCamera.transform.rotation;
            var whRatio = (1f * sceneCamera.pixelWidth / sceneCamera.pixelHeight);
            _generatedFieldOfView = whRatio < 1 ? sceneCamera.fieldOfView * whRatio : sceneCamera.fieldOfView;
            _generatedOrthographic = sceneCamera.orthographic;
            _generatedNearClipPlane = sceneCamera.nearClipPlane;
            _generatedFarClipPlane = sceneCamera.farClipPlane;
            _generatedOrthographicSize = sceneCamera.orthographicSize;
            _generatedNormalizedTime = normalizedTime;
        }

        private bool HasGenerationParamsChanged()
        {
            var sceneCamera = SceneView.lastActiveSceneView.camera;
            if (_generatedTransformPosition != sceneCamera.transform.position) return true;
            if (_generatedTransformRotation != sceneCamera.transform.rotation) return true;
            var whRatio = (1f * sceneCamera.pixelWidth / sceneCamera.pixelHeight);
            if (Math.Abs(_generatedFieldOfView - (whRatio < 1 ? sceneCamera.fieldOfView * whRatio : sceneCamera.fieldOfView)) > 0.001f) return true;
            if (_generatedOrthographic != sceneCamera.orthographic) return true;
            if (Math.Abs(_generatedNearClipPlane - sceneCamera.nearClipPlane) > 0.001f) return true;
            if (Math.Abs(_generatedFarClipPlane - sceneCamera.farClipPlane) > 0.001f) return true;
            if (Math.Abs(_generatedOrthographicSize - sceneCamera.orthographicSize) > 0.001f) return true;
            if (Math.Abs(_generatedNormalizedTime - normalizedTime) > 0.001f) return true;
            return false;
        }

        private void UsingAnimator(Animator inAnimator)
        {
            animator = inAnimator;
        }

        private static bool ColoredBgButton(bool isActive, Color bgColor, Func<bool> inside)
        {
            var col = GUI.backgroundColor;
            try
            {
                if (isActive) GUI.backgroundColor = bgColor;
                return inside();
            }
            finally
            {
                GUI.backgroundColor = col;
            }
        }

        [MenuItem("Window/Haï/AnimationViewer")]
        public static void ShowWindow()
        {
            Obtain().Show();
        }

        [MenuItem("CONTEXT/Animator/Haï AnimationViewer")]
        public static void OpenEditor(MenuCommand command)
        {
            var window = Obtain();
            window.UsingAnimator((Animator) command.context);
            window.Show();
            _enabled = true;
            window.Invalidate();
            EditorApplication.RepaintProjectWindow();
        }

        private static AnimationViewerEditorWindow Obtain()
        {
            var editor = GetWindow<AnimationViewerEditorWindow>(false, null, false);
            editor.titleContent = new GUIContent("AnimationViewer");
            return editor;
        }

        private static readonly AnimationViewerRenderQueue _projectRenderQueue;
        private static bool _delayedThisFrame;
        private static GameObject _focusedObjectNullable;
        private static bool _enabled;
        private float _generatedNormalizedTime;
        private readonly bool _thumbnailSizeFeatureAvailable;
        private readonly FieldInfo _projectBrowserListAreaField;
        private readonly PropertyInfo _listAreaGridSizeField;
        private readonly Type _projectBrowserType;

        static AnimationViewerEditorWindow()
        {
            _projectRenderQueue = new AnimationViewerRenderQueue();
        }

        private static void DrawAnimationClipItem(string guid, Rect selectionRect)
        {
            if (!_enabled) return;

            var assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (!assetPath.EndsWith(".anim")) return;

            var texture = _projectRenderQueue.RequireRender(assetPath);

            GUI.Box(selectionRect, texture);

            if (!_delayedThisFrame)
            {
                EditorApplication.delayCall += Rerender;
                _delayedThisFrame = true;
            }
        }

        private static void Rerender()
        {
            _delayedThisFrame = false;

            if (AnimationMode.InAnimationMode()) return;

            if (_focusedObjectNullable == null) return;

            var animator = _focusedObjectNullable.transform.GetComponentInParent<Animator>();
            if (animator == null) return;

            _projectRenderQueue.TryRender(animator.gameObject);
        }

        private void Invalidate()
        {
            _projectRenderQueue.ForceInvalidate();
            SaveGenerationParams();
        }
    }

    // Note: I tried to implement a UnityEditor.Editor for AnimationClip, through the "RenderStaticPreview" method,
    // but the project view would not display the icon, despite the inspector displaying the asset icon as expected.
    // Afterwards, I've settled with using the "projectWindowItemOnGUI" callback, as seen above.
    public class AnimationViewerRenderQueue
    {
        private readonly Dictionary<string, Texture2D> _pathToTexture;
        private readonly List<string> _invalidation;
        private Queue<string> _queue;
        private int _queueSize;
        private HumanBodyBones _bone = HumanBodyBones.Head;
        private float _normalizedTime;
        private AnimationClip _basePose;

        public AnimationViewerRenderQueue()
        {
            _pathToTexture = new Dictionary<string, Texture2D>();
            _invalidation = new List<string>();
            _queue = new Queue<string>();
        }

        public void ForceClearAll()
        {
            _pathToTexture.Clear();
            _queue.Clear();
            _invalidation.Clear();
        }

        public void ForceInvalidate()
        {
            _invalidation.AddRange(_pathToTexture.Keys);
        }

        public Texture2D RequireRender(string assetPath)
        {
            if (_pathToTexture.ContainsKey(assetPath)
                && _pathToTexture[assetPath] != null) // Can happen when the texture is destroyed (Unity invalid object)
            {
                if (!_queue.Contains(assetPath) && _invalidation.Contains(assetPath))
                {
                    _invalidation.RemoveAll(inList => inList == assetPath);
                    _queue.Enqueue(assetPath);
                }
                return _pathToTexture[assetPath];
            }

            var texture = new Texture2D(300, 300, TextureFormat.RGB24, true);
            _pathToTexture[assetPath] = texture; // TODO: Dimensions

            _queue.Enqueue(assetPath);

            return texture;
        }

        public bool TryRender(GameObject root)
        {
            if (_queue.Count == 0) return false;

            var originalAvatarGo = root;
            GameObject copy = null;
            var wasActive = originalAvatarGo.activeSelf;
            try
            {
                copy = Object.Instantiate(originalAvatarGo);
                copy.SetActive(true);
                originalAvatarGo.SetActive(false);
                Render(copy);
            }
            finally
            {
                if (wasActive) originalAvatarGo.SetActive(true);
                if (copy != null) Object.DestroyImmediate(copy);
            }

            return true;
        }

        private void Render(GameObject copy)
        {
            var viewer = new AnimationViewerGenerator();
            try
            {
                viewer.Begin(copy);
                var animator = copy.GetComponent<Animator>();
                if (animator.isHuman && _bone != HumanBodyBones.LastBone)
                {
                    var head = animator.GetBoneTransform(_bone);
                    viewer.ParentCameraTo(head);
                }
                else
                {
                    viewer.ParentCameraTo(animator.transform);
                }

                var itemCount = 0;
                while (_queue.Count > 0 && itemCount < _queueSize)
                {
                    var path = _queue.Dequeue();
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                    if (clip == null) clip = new AnimationClip(); // Defensive: Might happen if the clip gets deleted during an update
                    if (_basePose != null)
                    {
                        var modifiedClip = Object.Instantiate(clip);
                        var missingBindings = AnimationUtility.GetCurveBindings(_basePose)
                            .Where(binding => AnimationUtility.GetEditorCurve(clip, binding) == null)
                            .ToArray();
                        foreach (var missingBinding in missingBindings)
                        {
                            AnimationUtility.SetEditorCurve(modifiedClip, missingBinding, AnimationUtility.GetEditorCurve(_basePose, missingBinding));
                        }
                        viewer.Render(modifiedClip, _pathToTexture[path], _normalizedTime);
                    }
                    else
                    {
                        viewer.Render(clip, _pathToTexture[path], _normalizedTime);
                    }

                    // This is a workaround for an issue where the muscles will not update
                    // across multiple samplings of the animator on the same frame.
                    // This issue is mainly visible when the update speed (number of animation
                    // clips updated per frame) is greater than 1.
                    // By disabling and enabling the animator copy, this allows us to resample it.
                    copy.SetActive(false);
                    copy.SetActive(true);

                    itemCount++;
                }
            }
            finally
            {
                viewer.Terminate();
            }
        }

        public void QueueSize(int queueSize)
        {
            _queueSize = queueSize;
        }

        public void Bone(HumanBodyBones bone)
        {
            _bone = bone;
        }

        public void NormalizedTime(float normalizedTime)
        {
            _normalizedTime = normalizedTime;
        }

        public void BasePose(AnimationClip basePose)
        {
            _basePose = basePose;
        }
    }
}