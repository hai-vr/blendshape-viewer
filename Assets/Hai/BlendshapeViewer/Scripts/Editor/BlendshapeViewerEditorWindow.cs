using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Hai.BlendshapeViewer.Scripts.Editor
{
    public class BlendshapeViewerEditorWindow : EditorWindow
    {
        private const int MinWidth = 150;
        public SkinnedMeshRenderer skinnedMesh;
        public bool showDifferences = true;
        public bool autoUpdateOnFocus = true;
        public int thumbnailSize = 100;
        public bool showHotspots;
        public bool useComputeShader = true;
        public Texture2D[] tex2ds = new Texture2D[0];
        private Vector2 _scrollPos;
        private SkinnedMeshRenderer _generatedFor;
        private int _generatedSize;

        private Vector3 _generatedTransformPosition;
        private Quaternion _generatedTransformRotation;
        private float _generatedFieldOfView;
        private bool _generatedOrthographic;
        private float _generatedNearClipPlane;
        private float _generatedFarClipPlane;
        private float _generatedOrthographicSize;
        private Rect m_area;

        public BlendshapeViewerEditorWindow()
        {
            titleContent = new GUIContent("BlendshapeViewer");
        }

        private void OnFocus()
        {
            if (!autoUpdateOnFocus) return;
            if (skinnedMesh == null) return;
            if (!HasGenerationParamsChanged()) return;

            TryExecuteUpdate();
        }

        private void OnGUI()
        {
            var serializedObject = new SerializedObject(this);
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(skinnedMesh)));
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(showDifferences)));
            if (showDifferences)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(showHotspots)));
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(autoUpdateOnFocus)));
            if (SystemInfo.supportsComputeShaders)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(useComputeShader)));
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.IntSlider(serializedObject.FindProperty(nameof(thumbnailSize)), 100, 300);

            EditorGUI.BeginDisabledGroup(skinnedMesh == null || AnimationMode.InAnimationMode());
            if (GUILayout.Button("Update"))
            {
                TryExecuteUpdate();
            }
            EditorGUI.EndDisabledGroup();
            serializedObject.ApplyModifiedProperties();

            var width = Mathf.Max(_generatedSize, MinWidth);
            var total = tex2ds.Length;
            if (skinnedMesh != null && total > 0 && _generatedFor == skinnedMesh)
            {
                var serializedSkinnedMesh = new SerializedObject(skinnedMesh);

                _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(position.height - EditorGUIUtility.singleLineHeight * 7));

                var mod = Mathf.Max(1, (int)position.width / (width + 15));
                var highlightColor = EditorGUIUtility.isProSkin ? new Color(0.92f, 0.62f, 0.25f) : new Color(0.74f, 0.47f, 0.1f);
                for (var index = 0; index < total; index++)
                {
                    var texture2D = tex2ds[index];
                    if (index % mod == 0)
                    {
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();
                    }

                    EditorGUILayout.BeginVertical();
                    GUILayout.Box(texture2D);
                    var blendShapeName = skinnedMesh.sharedMesh.GetBlendShapeName(index);
                    var weight = serializedSkinnedMesh.FindProperty("m_BlendShapeWeights").GetArrayElementAtIndex(index);
                    var isNonZero = weight.floatValue > 0f;
                    Colored(isNonZero, highlightColor, () =>
                    {
                        EditorGUILayout.TextField(blendShapeName, isNonZero ? EditorStyles.boldLabel : EditorStyles.label, GUILayout.Width(width));
                    });
                    EditorGUILayout.Slider(weight, 0f, 100f, GUIContent.none, GUILayout.Width(width));
                    EditorGUILayout.EndVertical();

                    if ((index + 1) % mod == 0 || index == total - 1)
                    {
                        GUILayout.FlexibleSpace();
                        EditorGUILayout.EndHorizontal();
                    }
                }

                GUILayout.EndScrollView();
                serializedSkinnedMesh.ApplyModifiedProperties();
            }
        }

        private void TryExecuteUpdate()
        {
            if (AnimationMode.InAnimationMode()) return;

            Generate();
            SaveGenerationParams();
        }

        private void SaveGenerationParams()
        {
            _generatedFor = skinnedMesh;
            _generatedSize = thumbnailSize;

            var sceneCamera = SceneView.lastActiveSceneView.camera;
            _generatedTransformPosition = sceneCamera.transform.position;
            _generatedTransformRotation = sceneCamera.transform.rotation;
            var whRatio = (1f * sceneCamera.pixelWidth / sceneCamera.pixelHeight);
            _generatedFieldOfView = whRatio < 1 ? sceneCamera.fieldOfView * whRatio : sceneCamera.fieldOfView;
            _generatedOrthographic = sceneCamera.orthographic;
            _generatedNearClipPlane = sceneCamera.nearClipPlane;
            _generatedFarClipPlane = sceneCamera.farClipPlane;
            _generatedOrthographicSize = sceneCamera.orthographicSize;
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
            return false;
        }

        private void UsingSkinnedMesh(SkinnedMeshRenderer inSkinnedMesh)
        {
            skinnedMesh = inSkinnedMesh;
        }

        private void Generate()
        {
            var module = new BlendshapeViewerGenerator();
            try
            {
                module.Begin(skinnedMesh, showHotspots ? 0.95f : 0, useComputeShader);
                Texture2D neutralTexture = null;
                if (showDifferences)
                {
                    neutralTexture = NewTexture();
                    module.Render(EmptyClip(), neutralTexture);
                }

                var results = new [] {skinnedMesh}
                    .SelectMany(relevantSmr =>
                    {
                        var sharedMesh = relevantSmr.sharedMesh;

                        return Enumerable.Range(0, sharedMesh.blendShapeCount)
                            .Select(i =>
                            {
                                var blendShapeName = sharedMesh.GetBlendShapeName(i);
                                var currentWeight = relevantSmr.GetBlendShapeWeight(i);

                                // If the user has already animated this to 100, in normal circumstances the diff would show nothing.
                                // Animate the blendshape to 0 instead so that a diff can be generated.
                                var isAlreadyAnimatedTo100 = Math.Abs(currentWeight - 100f) < 0.001f;
                                var tempClip = new AnimationClip();
                                AnimationUtility.SetEditorCurve(
                                    tempClip,
                                    new EditorCurveBinding
                                    {
                                        path = "",
                                        type = typeof(SkinnedMeshRenderer),
                                        propertyName = $"blendShape.{blendShapeName}"
                                    },
                                    AnimationCurve.Constant(0, 1 / 60f, isAlreadyAnimatedTo100 ? 0f : 100f)
                                );

                                return tempClip;
                            })
                            .ToArray();
                    })
                    .ToArray();

                tex2ds = results
                    .Select((clip, i) =>
                    {
                        if (i % 10 == 0) EditorUtility.DisplayProgressBar("Rendering", $"Rendering ({i} / {results.Length})", 1f * i / results.Length);

                        var currentWeight = skinnedMesh.GetBlendShapeWeight(i);
                        var isAlreadyAnimatedTo100 = Math.Abs(currentWeight - 100f) < 0.001f;

                        var result = NewTexture();
                        module.Render(clip, result);
                        if (i == 0)
                        {
                            // Workaround a weird bug where the first blendshape is always incorrectly rendered
                            module.Render(clip, result);
                        }
                        if (showDifferences)
                        {
                            if (isAlreadyAnimatedTo100)
                            {
                                module.Diff(neutralTexture, result, result);
                            }
                            else
                            {
                                module.Diff(result, neutralTexture, result);
                            }
                        }
                        return result;
                    })
                    .ToArray();
            }
            finally
            {
                module.Terminate();
                EditorUtility.ClearProgressBar();
            }
        }

        private static void Colored(bool isActive, Color bgColor, Action inside)
        {
            var col = GUI.contentColor;
            try
            {
                if (isActive) GUI.contentColor = bgColor;
                inside();
            }
            finally
            {
                GUI.contentColor = col;
            }
        }

        private static AnimationClip EmptyClip()
        {
            var emptyClip = new AnimationClip();
            AnimationUtility.SetEditorCurve(
                emptyClip,
                new EditorCurveBinding
                {
                    path = "_ignored",
                    type = typeof(GameObject),
                    propertyName = "m_Active"
                },
                AnimationCurve.Constant(0, 1 / 60f, 100f)
            );
            return emptyClip;
        }

        private Texture2D NewTexture()
        {
            var newTexture = new Texture2D(Mathf.Max(thumbnailSize, MinWidth), thumbnailSize, TextureFormat.RGB24, false);
            newTexture.wrapMode = TextureWrapMode.Clamp;
            return newTexture;
        }

        [MenuItem("Window/Haï/BlendshapeViewer")]
        public static void ShowWindow()
        {
            Obtain().Show();
        }

        [MenuItem("CONTEXT/SkinnedMeshRenderer/Haï BlendshapeViewer")]
        public static void OpenEditor(MenuCommand command)
        {
            var window = Obtain();
            window.UsingSkinnedMesh((SkinnedMeshRenderer) command.context);
            window.Show();
            window.TryExecuteUpdate();
        }

        private static BlendshapeViewerEditorWindow Obtain()
        {
            var editor = GetWindow<BlendshapeViewerEditorWindow>(false, null, false);
            editor.titleContent = new GUIContent("BlendshapeViewer");
            return editor;
        }
    }
}