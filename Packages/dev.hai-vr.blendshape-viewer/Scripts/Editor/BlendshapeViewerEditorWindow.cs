using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hai.BlendshapeViewer.Scripts.Editor
{
    public class BlendshapeViewerEditorWindow : EditorWindow
    {
        private static class Phrases
        {
            public const string documentation_url = nameof(documentation_url);
            public const string auto_update_on_focus = nameof(auto_update_on_focus);
            public const string copy_to_clipboard = nameof(copy_to_clipboard);
            public const string mesh = nameof(mesh);
            public const string rendering = nameof(rendering);
            public const string rendering_progress = nameof(rendering_progress);
            public const string search = nameof(search);
            public const string show_differences = nameof(show_differences);
            public const string show_hotspots = nameof(show_hotspots);
            public const string thumbnail_size = nameof(thumbnail_size);
            public const string update = nameof(update);
        }

        private const int MinWidth = 150;
        private const int MaxSearchQueryLength = 100;
        
        public SkinnedMeshRenderer skinnedMesh;
        private readonly BlendshapeViewerEditorPrefs _editorPrefs = new();
        
        private static HaiEFLoc localize
        {
            get
            {
                _localize ??= NewLoc();
                return _localize;
            }
        }
        private static HaiEFLoc _localize;
        private static HaiEFLoc NewLoc() => new("dev.hai-vr.blendshape-viewer", "Packages/dev.hai-vr.blendshape-viewer/Scripts/Editor/Locale");
        
        public Texture2D[] tex2ds = new Texture2D[0];

        private string _search = "";
        
        private SkinnedMeshRenderer _generatedFor;
        private int _generatedSize;

        private Vector3 _generatedTransformPosition;
        private Quaternion _generatedTransformRotation;
        private float _generatedFieldOfView;
        private bool _generatedOrthographic;
        private float _generatedNearClipPlane;
        private float _generatedFarClipPlane;
        private float _generatedOrthographicSize;
        
        private const string SearchLabelText = "Search";

        private VisualElement _root;
        private VisualElement _container;
        private VisualElement _grid;
        private Button _updateButton;
        private List<BlendshapeElement> _elements = new List<BlendshapeElement>();

        private class BlendshapeElement
        {
            public VisualElement Root;
            public Image Image;
            public TextField TextField;
            public Slider Slider;
            public FloatField SliderValueField;
            public int Index;
            public string Name;

            public void SetVisible(bool visible)
            {
                Root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        public BlendshapeViewerEditorWindow()
        {
            titleContent = new GUIContent("Blendshape Viewer");
        }

        public void CreateGUI()
        {
            localize.RefreshIfNecessary();
            _root = rootVisualElement;
            var serializedObject = new SerializedObject(this);

            var topSettings = new VisualElement { style = { flexDirection = FlexDirection.Column, flexShrink = 0 } };
            _root.Add(topSettings);

            var meshRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            topSettings.Add(meshRow);

            var meshField = new PropertyField(serializedObject.FindProperty(nameof(skinnedMesh)), localize.Text(Phrases.mesh))
            {
                style = { flexGrow = 1 }
            };
            meshField.RegisterValueChangeCallback(evt => {
                if (_root != null)
                {
                    ReconstructGrid();
                }
            });
            meshRow.Add(meshField);

            var helpButton = new Button(() =>
            {
                var documentationUrl__unsafe = localize.Text(Phrases.documentation_url);
                if (documentationUrl__unsafe.StartsWith("https://docs.hai-vr.dev/"))
                {
                    Application.OpenURL(documentationUrl__unsafe);
                }
                else
                {
                    Application.OpenURL("https://docs.hai-vr.dev/docs/products/blendshape-viewer");
                }
            })
            {
                style =
                {
                    width = 16,
                    height = 16,
                    backgroundColor = Color.clear,
                    borderBottomWidth = 0,
                    borderLeftWidth = 0,
                    borderRightWidth = 0,
                    borderTopWidth = 0,
                    paddingBottom = 0,
                    paddingLeft = 0,
                    paddingRight = 0,
                    paddingTop = 0
                }
            };
            helpButton.Add(new Image
            {
                image = EditorGUIUtility.IconContent("_Help").image,
                scaleMode = ScaleMode.ScaleToFit,
                style =
                {
                    flexGrow = 1,
                    width = new Length(100, LengthUnit.Percent),
                    height = new Length(100, LengthUnit.Percent)
                }
            });
            helpButton.AddToClassList("unity-button--quiet");
            meshRow.Add(helpButton);

            var togglesRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            var showDifferencesToggle = new Toggle(localize.Text(Phrases.show_differences)) { value = _editorPrefs.ShowDifferences, style = { flexGrow = 1, flexBasis = 0 } };
            showDifferencesToggle.RegisterValueChangedCallback(evt => {
                _editorPrefs.ShowDifferences = evt.newValue;
                ReconstructGrid();
            });
            togglesRow.Add(showDifferencesToggle);
            var showHotspotsToggle = new Toggle(localize.Text(Phrases.show_hotspots)) { value = _editorPrefs.ShowHotspots, style = { flexGrow = 1, flexBasis = 0 } };
            showHotspotsToggle.RegisterValueChangedCallback(evt => {
                _editorPrefs.ShowHotspots = evt.newValue;
                ReconstructGrid();
            });
            togglesRow.Add(showHotspotsToggle);
            
            // Toggle visibility of hotspots based on showDifferences
            showHotspotsToggle.SetEnabled(_editorPrefs.ShowDifferences);
            showDifferencesToggle.RegisterValueChangedCallback(evt => {
                showHotspotsToggle.SetEnabled(evt.newValue);
            });

            var autoUpdateToggle = new Toggle(localize.Text(Phrases.auto_update_on_focus)) { value = _editorPrefs.AutoUpdateOnFocus, style = { flexGrow = 1, flexBasis = 0 } };
            autoUpdateToggle.RegisterValueChangedCallback(evt => _editorPrefs.AutoUpdateOnFocus = evt.newValue);
            togglesRow.Add(autoUpdateToggle);
            topSettings.Add(togglesRow);

            var sizeSlider = new SliderInt(localize.Text(Phrases.thumbnail_size), 100, 300) { value = _editorPrefs.ThumbnailSize };
            sizeSlider.RegisterValueChangedCallback(evt => {
                _editorPrefs.ThumbnailSize = evt.newValue;
                UpdateLayout();
            });
            topSettings.Add(sizeSlider);

            _updateButton = new Button(() => TryExecuteUpdate()) { text = localize.Text(Phrases.update) };
            _updateButton.SetEnabled(skinnedMesh != null && !AnimationMode.InAnimationMode());
            topSettings.Add(_updateButton);

            var controlsRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginTop = 5, marginBottom = 5 } };
            
            controlsRow.Add(new Label(localize.Text(Phrases.search)) { style = { width = 50 } });
            var searchField = new TextField { style = { flexGrow = 1 } };
            searchField.value = _search;
            searchField.RegisterValueChangedCallback(evt =>
            {
                _search = evt.newValue;
                if (_search.Length > MaxSearchQueryLength)
                {
                    _search = _search.Substring(0, MaxSearchQueryLength);
                    searchField.SetValueWithoutNotify(_search);
                }
                UpdateFiltering();
            });
            controlsRow.Add(searchField);
            topSettings.Add(controlsRow);

            _container = new ScrollView(ScrollViewMode.Vertical);
            _container.style.flexGrow = 1;
            _root.Add(_container);

            _grid = new VisualElement();
            _grid.style.flexDirection = FlexDirection.Row;
            _grid.style.flexWrap = Wrap.Wrap;
            _grid.style.justifyContent = Justify.Center;
            _container.Add(_grid);

            var footer = localize.CreateSelectorElement(() =>
            {
                _localize = NewLoc();
                _root.Clear();
                CreateGUI();
                return _localize;
            });
            footer.style.flexShrink = 0;
            _root.Add(footer);

            _root.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                if (evt.target == _root)
                {
                    UpdateLayout();
                }
            });
            
            _root.Bind(serializedObject);

            if (skinnedMesh != null && _generatedFor == skinnedMesh)
            {
                ReconstructGrid();
            }
        }

        private void UpdateFiltering()
        {
            var hasSearch = !string.IsNullOrEmpty(_search);
            foreach (var element in _elements)
            {
                element.SetVisible(!hasSearch || IsMatch(element.Name));
            }
        }

        private void UpdateLayout()
        {
            if (_grid == null) return;
            
            var width = Mathf.Max(_editorPrefs.ThumbnailSize, MinWidth);
            foreach (var element in _elements)
            {
                element.Root.style.width = width;
                element.Image.style.width = width;
                element.Image.style.height = _editorPrefs.ThumbnailSize;
                element.TextField.style.width = width - 25;
                element.Slider.style.flexGrow = 1;
                element.Slider.style.marginLeft = 0;
                element.Slider.style.marginRight = 0;
                element.SliderValueField.style.width = 35;
            }
        }

        private void ReconstructGrid()
        {
            _grid.Clear();
            _elements.Clear();

            if (skinnedMesh == null || skinnedMesh.sharedMesh == null) return;

            var total = skinnedMesh.sharedMesh.blendShapeCount;
            var serializedSkinnedMesh = new SerializedObject(skinnedMesh);
            var weightsProp = serializedSkinnedMesh.FindProperty("m_BlendShapeWeights");
            var highlightColor = EditorGUIUtility.isProSkin ? new Color(0.92f, 0.62f, 0.25f) : new Color(0.74f, 0.47f, 0.1f);
            var clipboardIcon = EditorGUIUtility.IconContent("Clipboard").image as Texture2D;

            for (var i = 0; i < total; i++)
            {
                var index = i;
                var blendShapeName = skinnedMesh.sharedMesh.GetBlendShapeName(index);
                var weightProp = weightsProp.GetArrayElementAtIndex(index);

                var itemRoot = new VisualElement { style = { 
                    marginTop = 5,
                    marginBottom = 5,
                    marginLeft = 5,
                    marginRight = 5,
                    flexShrink = 0,
                    flexGrow = 0
                } };
                
                var texture2D = index < tex2ds.Length ? tex2ds[index] : null;
                var image = new Image { image = texture2D };
                itemRoot.Add(image);

                var titleRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                var textField = new TextField { value = blendShapeName, isReadOnly = true };
                textField.style.marginLeft = 0;
                textField.style.marginRight = 0;
                textField.style.marginTop = 0;
                textField.style.marginBottom = 0;
                textField.style.paddingLeft = 0;
                textField.style.paddingRight = 0;
                textField.style.paddingTop = 0;
                textField.style.paddingBottom = 0;
                textField.ElementAt(0).style.backgroundColor = Color.clear;
                textField.ElementAt(0).style.borderBottomWidth = 0;
                textField.ElementAt(0).style.borderLeftWidth = 0;
                textField.ElementAt(0).style.borderRightWidth = 0;
                textField.ElementAt(0).style.borderTopWidth = 0;
                textField.ElementAt(0).style.paddingLeft = 0;
                textField.ElementAt(0).style.marginLeft = 0;
                
                Action updateHighlight = () =>
                {
                    var isNonZero = weightProp.floatValue > 0f;
                    textField.style.unityFontStyleAndWeight = isNonZero ? FontStyle.Bold : FontStyle.Normal;
                    textField.style.color = isNonZero ? highlightColor : StyleKeyword.Null;
                };
                
                itemRoot.TrackPropertyValue(weightProp, prop => updateHighlight());
                updateHighlight();

                titleRow.Add(textField);

                var copyButton = new Button(() => GUIUtility.systemCopyBuffer = blendShapeName)
                {
                    tooltip = localize.Format(Phrases.copy_to_clipboard, blendShapeName),
                    style =
                    {
                        width = 25,
                        paddingBottom = 0, paddingLeft = 0, paddingRight = 0, paddingTop = 0,
                        marginLeft = 0, marginRight = 0, marginTop = 0, marginBottom = 0
                    }
                };
                copyButton.Add(new Image
                {
                    image = clipboardIcon,
                    scaleMode = ScaleMode.ScaleToFit,
                    style =
                    {
                        flexGrow = 1,
                        width = new Length(100, LengthUnit.Percent),
                        height = new Length(100, LengthUnit.Percent)
                    }
                });
                titleRow.Add(copyButton);
                itemRoot.Add(titleRow);

                var sliderRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                var slider = new Slider(0, 100)
                {
                    style =
                    {
                        marginLeft = 0,
                        marginRight = 0,
                        flexGrow = 1
                    }
                };
                slider.BindProperty(weightProp);
                sliderRow.Add(slider);

                var sliderValueField = new FloatField
                {
                    style =
                    {
                        width = 35,
                        marginLeft = 2
                    }
                };
                sliderValueField.BindProperty(weightProp);
                sliderRow.Add(sliderValueField);
                
                itemRoot.Add(sliderRow);

                _grid.Add(itemRoot);
                var element = new BlendshapeElement
                {
                    Root = itemRoot,
                    Image = image,
                    TextField = textField,
                    Slider = slider,
                    SliderValueField = sliderValueField,
                    Index = index,
                    Name = blendShapeName
                };
                _elements.Add(element);
            }
            
            UpdateLayout();
            UpdateFiltering();
        }

        private void OnFocus()
        {
            UpdateUpdateButtonEnabledState();

            if (_editorPrefs.AutoUpdateOnFocus)
            {
                EditorApplication.delayCall += () => EditorApplication.delayCall += () =>
                {
                    if (skinnedMesh == null) return;
                    if (!HasGenerationParamsChanged()) return;

                    TryExecuteUpdate();
                };
            }
        }

        private void Update()
        {
            UpdateUpdateButtonEnabledState();
        }

        private void UpdateUpdateButtonEnabledState()
        {
            if (_updateButton != null)
            {
                _updateButton.SetEnabled(skinnedMesh != null && !AnimationMode.InAnimationMode());
            }
        }

        private void TryExecuteUpdate()
        {
            if (AnimationMode.InAnimationMode()) return;

            Generate();
            SaveGenerationParams();
            
            if (_root != null)
            {
                ReconstructGrid();
            }
        }

        private void SaveGenerationParams()
        {
            _generatedFor = skinnedMesh;
            _generatedSize = _editorPrefs.ThumbnailSize;

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
            if (SceneView.lastActiveSceneView == null || SceneView.lastActiveSceneView.camera == null) return false;
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
                module.Begin(skinnedMesh, _editorPrefs.ShowHotspots ? 0.95f : 0);
                Texture2D neutralTexture = null;
                if (_editorPrefs.ShowDifferences)
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
                        if (i % 10 == 0) EditorUtility.DisplayProgressBar(localize.Text(Phrases.rendering), localize.Format(Phrases.rendering_progress, i, results.Length), 1f * i / results.Length);

                        var currentWeight = skinnedMesh.GetBlendShapeWeight(i);
                        var isAlreadyAnimatedTo100 = Math.Abs(currentWeight - 100f) < 0.001f;

                        var result = NewTexture();
                        module.Render(clip, result);
                        if (i == 0)
                        {
                            // Workaround a weird bug where the first blendshape is always incorrectly rendered
                            module.Render(clip, result);
                        }
                        if (_editorPrefs.ShowDifferences)
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
            var newTexture = new Texture2D(Mathf.Max(_editorPrefs.ThumbnailSize, MinWidth), _editorPrefs.ThumbnailSize, TextureFormat.RGB24, false);
            newTexture.wrapMode = TextureWrapMode.Clamp;
            return newTexture;
        }
        
        private bool IsMatch(string thatName)
        {
            var propertyName = thatName.ToLowerInvariant();
            return _search.ToLowerInvariant().Split(' ').All(needle => propertyName.Contains(needle));
        }

        [MenuItem("Window/Haï/Blendshape Viewer")]
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
            editor.titleContent = new GUIContent("Blendshape Viewer");
            return editor;
        }
    }
}