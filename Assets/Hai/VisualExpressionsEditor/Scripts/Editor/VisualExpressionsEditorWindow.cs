using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Hai.VisualExpressionsEditor.Scripts
{
    public class VisualExpressionsEditorWindow : EditorWindow
    {
        private const int MinWidth = 150;
        public Animator animator;
        public bool showDifferences = true;
        public bool autoUpdateOnFocus = false;
        public int thumbnailSize = 100;
        public bool showHotspots;
        public bool useComputeShader = true;
        public Texture2D[][] smrToTex2ds = new Texture2D[0][];
        private Vector2 _scrollPos;
        private Animator _generatedFor;
        private int _generatedSize;

        private Vector3 _generatedTransformPosition;
        private Quaternion _generatedTransformRotation;
        private float _generatedFieldOfView;
        private bool _generatedOrthographic;
        private float _generatedNearClipPlane;
        private float _generatedFarClipPlane;
        private float _generatedOrthographicSize;
        private Rect m_area;
        private SkinnedMeshRenderer[] _allSkinnedMeshes;
        //

        public AnimationClip clip;
        public bool autoSelectClip = true;
        public bool advanced;
        public float normalizedTime;
        public HumanBodyBones focusedBone = HumanBodyBones.Head;
        public AnimationClip basePose;
        private Texture2D _clipTextureNullable;
        private HashSet<EditorCurveBinding> _manuallyActedChanges = new HashSet<EditorCurveBinding>();
        private Rect m_ActiveRect;
        private Vector2 _scrollPosActive;

        public VisualExpressionsEditorWindow()
        {
            titleContent = new GUIContent("VisualExpressionsEditor");
        }

        private void OnFocus()
        {
            if (!autoUpdateOnFocus) return;
            if (animator == null) return;
            if (!HasAnimatorGenerationParamsChanged()) return;

            TryExecuteAnimatorUpdate();
        }

        private void OnInspectorUpdate()
        {
            if (!autoSelectClip) return;

            var active = Selection.activeObject;
            if (active == null) return;
            if (!(active is AnimationClip)) return;
            if (clip == active) return;

            clip = (AnimationClip) active;
            _manuallyActedChanges = new HashSet<EditorCurveBinding>();
            TryExecuteClipChangeUpdate();
        }

        public void ChangeAnimator(Animator inAnimator)
        {
            animator = inAnimator;
            TryExecuteAnimatorUpdate();
            TryExecuteClipChangeUpdate();
        }

        public void ChangeClip(AnimationClip inClip)
        {
            if (clip == inClip) return;

            clip = inClip;
            _manuallyActedChanges = new HashSet<EditorCurveBinding>();
            TryExecuteClipChangeUpdate();
        }

        private void OnGUI()
        {
            var serializedObject = new SerializedObject(this);
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(animator)));
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(clip)));
            EditorGUILayout.LabelField("Auto Select", GUILayout.Width(100));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(autoSelectClip)), GUIContent.none, GUILayout.Width(25));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.IntSlider(serializedObject.FindProperty(nameof(thumbnailSize)), 100, 300);

            EditorGUI.BeginDisabledGroup(animator == null || AnimationMode.InAnimationMode());
            if (GUILayout.Button("Update"))
            {
                TryExecuteAnimatorUpdate();
                _manuallyActedChanges = new HashSet<EditorCurveBinding>();
                TryExecuteClipChangeUpdate();
            }
            EditorGUI.EndDisabledGroup();
            //

            advanced = EditorGUILayout.Foldout(advanced, "Advanced");
            if (advanced)
            {
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
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(focusedBone)));
                EditorGUILayout.Slider(serializedObject.FindProperty(nameof(normalizedTime)), 0f, 1f);
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(basePose)));
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

            var width = Mathf.Max(_generatedSize, MinWidth);
            var showCondition = smrToTex2ds.Length > 0;
            if (animator != null && showCondition && _generatedFor == animator)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.BeginVertical(GUILayout.Width(300));
                GUILayout.Box(_clipTextureNullable);
                var all0ValueBindings = ImmutableHashSet<EditorCurveBinding>.Empty;
                if (clip != null)
                {
                    var bindings = AnimationUtility.GetCurveBindings(clip);
                    all0ValueBindings = bindings
                        .Where(binding => binding.type == typeof(SkinnedMeshRenderer) && binding.propertyName.StartsWith("blendShape."))
                        .Where(binding => AnimationUtility.GetEditorCurve(clip, binding).keys.All(keyframe => keyframe.value == 0f))
                        .ToImmutableHashSet();
                    if (all0ValueBindings.Count > 0)
                    {
                        EditorGUILayout.BeginHorizontal();
                        if (ColoredBgButton(true, Color.yellow, () => GUILayout.Button($"Delete all 0-values ({all0ValueBindings.Count})", GUILayout.Width(300))))
                        {
                            Undo.SetCurrentGroupName($"VEE Remove {all0ValueBindings.Count} 0-values");
                            foreach (var binding in all0ValueBindings)
                            {
                                RemoveAnimatable(binding, false);
                            }
                            TryExecuteClipChangeUpdate();
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                }
                EditorGUILayout.EndVertical();
                RectOnRepaint(() => GUILayoutUtility.GetRect(width - 300, 350), rect => m_ActiveRect = rect);
                GUILayout.BeginArea(m_ActiveRect);
                _scrollPosActive = GUILayout.BeginScrollView(_scrollPosActive, GUILayout.Height(350));
                DisplayBlendshapeSelector(width, Screen.width - 300, all0ValueBindings, true);
                GUILayout.EndScrollView();
                GUILayout.EndArea();
                EditorGUILayout.EndHorizontal();

                _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(Screen.height - EditorGUIUtility.singleLineHeight * (
                    7
                    + (advanced ? 6 : 0)
                    + (basePose != null ? (advanced ? 3 : 5) : 0)
                    + (focusedBone != HumanBodyBones.Head ? 1 : 0)
                ) - 350));
                DisplayBlendshapeSelector(width, Screen.width, all0ValueBindings, false);
                GUILayout.EndScrollView();
            }
        }

        public static void RectOnRepaint(Func<Rect> rectFn, Action<Rect> applyFn)
        {
            var rect = rectFn();
            if (Event.current.type == EventType.Repaint)
            {
                // https://answers.unity.com/questions/515197/how-to-use-guilayoututilitygetrect-properly.html
                applyFn(rect);
            }
        }

        private void DisplayBlendshapeSelector(int width, int screenWidth, ImmutableHashSet<EditorCurveBinding> all0ValueBindings, bool summaryView)
        {
            var dpiFix = 96f / Screen.dpi;
            var intWidthDpiFix = (int) (screenWidth * dpiFix);
            var mod = Mathf.Max(1, intWidthDpiFix / (width + 15));
            var highlightColor = EditorGUIUtility.isProSkin ? new Color(0.92f, 0.62f, 0.25f) : new Color(0.74f, 0.47f, 0.1f);

            var allBindings = clip != null ? AnimationUtility.GetCurveBindings(clip).ToImmutableHashSet() : ImmutableHashSet<EditorCurveBinding>.Empty;
            var layoutActive = false;
            var layoutCounter = 0;
            for (var smrIndex = 0; smrIndex < _allSkinnedMeshes.Length; smrIndex++)
            {
                var skinnedMesh = _allSkinnedMeshes[smrIndex];
                var tex2ds = smrToTex2ds[smrIndex];
                if (!summaryView)
                {
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUILayout.ObjectField(skinnedMesh.name, skinnedMesh, typeof(SkinnedMeshRenderer), EditorStyles.boldFont);
                    EditorGUI.EndDisabledGroup();
                }
                var total = tex2ds.Length;

                var transformPath = AnimationUtility.CalculateTransformPath(skinnedMesh.transform, animator.transform);
                if (!summaryView)
                {
                    layoutCounter = 0;
                }
                for (var index = 0; index < total; index++)
                {
                    var blendShapeName = skinnedMesh.sharedMesh.GetBlendShapeName(index);

                    var binding = new EditorCurveBinding
                    {
                        path = transformPath,
                        propertyName = $"blendShape.{blendShapeName}",
                        type = typeof(SkinnedMeshRenderer)
                    };
                    var existsInAnimation = allBindings.Contains(binding);
                    var existsFirstValue = -1f;
                    var existsAllSameValue = false;
                    if (existsInAnimation)
                    {
                        if (all0ValueBindings.Contains(binding))
                        {
                            existsFirstValue = 0f;
                            existsAllSameValue = true;
                        }
                        else
                        {
                            var curve = AnimationUtility.GetEditorCurve(clip, binding);
                            existsFirstValue = curve.keys.Length > 0 ? curve.keys[0].value : -1f; // Defensive
                            existsAllSameValue = curve.keys.All(keyframe => keyframe.value == existsFirstValue);
                        }
                    }

                    if (summaryView && (!existsInAnimation || existsAllSameValue && existsFirstValue == 0 && !_manuallyActedChanges.Contains(binding)))
                    {
                        continue;
                    }

                    var texture2D = tex2ds[index];
                    if (layoutCounter % mod == 0)
                    {
                        layoutActive = true;
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();
                    }

                    EditorGUILayout.BeginVertical();
                    if (!summaryView)
                    {
                        if (ColoredBgButton(existsInAnimation, existsAllSameValue && existsFirstValue == 0f ? Color.yellow : Color.red, () => GUILayout.Button(texture2D, GUILayout.Width(texture2D.width), GUILayout.Height(texture2D.height))))
                        {
                            if (existsInAnimation && (!existsAllSameValue || existsFirstValue != 0))
                            {
                                RemoveAnimatable(binding);
                            }
                            else
                            {
                                AddAnimatable(binding);
                            }
                        }
                    }
                    else
                    {
                        GUILayout.Box(texture2D);
                    }

                    if (summaryView || existsInAnimation && existsAllSameValue && existsFirstValue == 0f)
                    {
                        EditorGUILayout.BeginHorizontal();
                        Colored(existsInAnimation, highlightColor, () => { EditorGUILayout.TextField(blendShapeName, existsInAnimation ? EditorStyles.boldLabel : EditorStyles.label, GUILayout.Width(width - 25)); });
                        if (ColoredBgButton(true, Color.red, () => GUILayout.Button("×", GUILayout.Width(20))))
                        {
                            RemoveAnimatable(binding);
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                    else
                    {
                        Colored(existsInAnimation, highlightColor, () => { EditorGUILayout.TextField(blendShapeName, existsInAnimation ? EditorStyles.boldLabel : EditorStyles.label, GUILayout.Width(width)); });
                    }

                    // var weight = serializedSkinnedMesh.FindProperty("m_BlendShapeWeights").GetArrayElementAtIndex(index);
                    // var isNonZero = weight.floatValue > 0f;
                    if (existsInAnimation
                        && existsAllSameValue
                        && (existsFirstValue != 0f || _manuallyActedChanges.Contains(binding))
                        // Some animations have constants outside threshold, and these will get silently edited to 100 if the editor is allowed
                        && existsFirstValue >= 0f && existsFirstValue <= 100f
                        )
                    {
                        var newValue = EditorGUILayout.Slider(GUIContent.none, existsFirstValue, 0f, 100f, GUILayout.Width(width));
                        if (newValue != existsFirstValue)
                        {
                            _manuallyActedChanges.Add(binding);
                            ModifyAnimatable(binding, newValue);
                        }
                    }
                    else if (existsInAnimation && existsAllSameValue && (existsFirstValue < 0f || existsFirstValue > 100f))
                    {
                        EditorGUILayout.LabelField(string.Format(CultureInfo.InvariantCulture, "[{0:0.##}]", existsFirstValue), GUILayout.Width(width));
                    }
                    else if (existsInAnimation && !existsAllSameValue)
                    {
                        var curve = AnimationUtility.GetEditorCurve(clip, binding);
                        var min = curve.keys.Select(keyframe => keyframe.value).Min();
                        var max = curve.keys.Select(keyframe => keyframe.value).Max();
                        EditorGUILayout.LabelField(string.Format(CultureInfo.InvariantCulture, "[{0:0.##} : {1:0.##}]", min, max), GUILayout.Width(width));
                    }

                    EditorGUILayout.EndVertical();

                    if ((layoutCounter + 1) % mod == 0 || (!summaryView && layoutCounter == total - 1))
                    {
                        GUILayout.FlexibleSpace();
                        EditorGUILayout.EndHorizontal();
                        layoutActive = false;
                    }

                    layoutCounter++;
                }
            }

            if (summaryView && layoutActive)
            {
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
        }

        private void AddAnimatable(EditorCurveBinding binding)
        {
            if (clip == null) return;
            Undo.RecordObject(clip, $"VEE Add {binding.propertyName} at {binding.path}");
            AnimationUtility.SetEditorCurve(clip, binding, AnimationCurve.Constant(0, 1/60f, 100f));
            TryExecuteClipChangeUpdate();
        }

        private void RemoveAnimatable(EditorCurveBinding binding, bool update = true)
        {
            if (clip == null) return;
            Undo.RecordObject(clip, $"VEE Remove {binding.propertyName} at {binding.path}");
            AnimationUtility.SetEditorCurve(clip, binding, null);
            if (update)
            {
                TryExecuteClipChangeUpdate();
            }
        }

        private void ModifyAnimatable(EditorCurveBinding binding, float newValue)
        {
            if (clip == null) return;
            Undo.RecordObject(clip, $"VEE Change {binding.propertyName} at {binding.path}");
            var curve = AnimationUtility.GetEditorCurve(clip, binding);
            curve.keys = curve.keys.Select(keyframe =>
            {
                keyframe.value = newValue;
                return keyframe;
            }).ToArray();
            AnimationUtility.SetEditorCurve(clip, binding, curve);
            TryExecuteClipChangeUpdate();
        }

        public void TryExecuteClipChangeUpdate()
        {
            if (AnimationMode.InAnimationMode()) return;

            GenerateClip();
        }

        public void TryExecuteAnimatorUpdate()
        {
            if (AnimationMode.InAnimationMode()) return;

            _allSkinnedMeshes = animator.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(renderer => renderer.sharedMesh != null && renderer.sharedMesh.blendShapeCount > 0)
                .OrderByDescending(renderer => renderer.sharedMesh.blendShapeCount)
                .ToArray();
            smrToTex2ds = new Texture2D[_allSkinnedMeshes.Length][];
            for (var index = 0; index < _allSkinnedMeshes.Length; index++)
            {
                var smr = _allSkinnedMeshes[index];
                smrToTex2ds[index] = GenerateBlendShapes(smr);
            }

            SaveAnimatorGenerationParams();
        }

        private void SaveAnimatorGenerationParams()
        {
            _generatedFor = animator;
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

        private bool HasAnimatorGenerationParamsChanged()
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

        private void UsingAnimator(Animator inAnimator)
        {
            animator = inAnimator;
        }

        private void GenerateClip()
        {
            TryRender(animator.gameObject);
        }

        private bool TryRender(GameObject root)
        {
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
            var viewer = new VisualExpressionsEditorGeneratorClip();
            try
            {
                viewer.Begin(copy);
                var animator = copy.GetComponent<Animator>();
                if (animator.isHuman && focusedBone != HumanBodyBones.LastBone)
                {
                    var head = animator.GetBoneTransform(focusedBone);
                    viewer.ParentCameraTo(head);
                }
                else
                {
                    viewer.ParentCameraTo(animator.transform);
                }

                var texture = new Texture2D(300, 300, TextureFormat.RGB24, true);

                var itemCount = 0;
                var localClip = clip;
                if (localClip == null) localClip = new AnimationClip(); // Defensive: Might happen if the clip gets deleted during an update
                if (basePose != null)
                {
                    var modifiedClip = Object.Instantiate(localClip);
                    var missingBindings = AnimationUtility.GetCurveBindings(basePose)
                        .Where(binding => AnimationUtility.GetEditorCurve(localClip, binding) == null)
                        .ToArray();
                    foreach (var missingBinding in missingBindings)
                    {
                        AnimationUtility.SetEditorCurve(modifiedClip, missingBinding, AnimationUtility.GetEditorCurve(basePose, missingBinding));
                    }
                    viewer.Render(modifiedClip, texture, normalizedTime);
                }
                else
                {
                    viewer.Render(localClip, texture, normalizedTime);
                }

                _clipTextureNullable = texture;

                // Warning: This is from the "Animation Viewer" project.
                // This may not apply here since there is no queue; the animator copy is only consumed once.
                // [[
                // This is a workaround for an issue where the muscles will not update
                // across multiple samplings of the animator on the same frame.
                // This issue is mainly visible when the update speed (number of animation
                // clips updated per frame) is greater than 1.
                // By disabling and enabling the animator copy, this allows us to resample it.
                copy.SetActive(false);
                copy.SetActive(true);
                // ]]
            }
            finally
            {
                viewer.Terminate();
            }
        }

        private Texture2D[] GenerateBlendShapes(SkinnedMeshRenderer skinnedMesh)
        {
            var module = new VisualExpressionsEditorGeneratorSingular();
            try
            {
                module.Begin(skinnedMesh.gameObject, showHotspots ? 0.95f : 0, useComputeShader);
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

                return results
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

        [MenuItem("Window/Haï/VisualExpressionsEditor")]
        public static void ShowWindow()
        {
            Obtain().Show();
        }

        [MenuItem("CONTEXT/Animator/Haï VisualExpressionsEditor")]
        public static void OpenEditor(MenuCommand command)
        {
            var window = Obtain();
            window.UsingAnimator((Animator) command.context);
            window.Show();
            window.TryExecuteAnimatorUpdate();
        }

        private static VisualExpressionsEditorWindow Obtain()
        {
            var editor = GetWindow<VisualExpressionsEditorWindow>(false, null, false);
            editor.titleContent = new GUIContent("VisualExpressionsEditor");
            return editor;
        }
    }
}