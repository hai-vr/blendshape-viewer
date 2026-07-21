using UnityEditor;

namespace Hai.BlendshapeViewer.Scripts.Editor
{
    internal class BlendshapeViewerEditorPrefs
    {
        private const string PrefsPrefix = "Hai.BlendshapeViewer.";

        public bool ShowDifferences
        {
            get => EditorPrefs.GetBool(PrefsPrefix + nameof(ShowDifferences), true);
            set => EditorPrefs.SetBool(PrefsPrefix + nameof(ShowDifferences), value);
        }

        public bool AutoUpdateOnFocus
        {
            get => EditorPrefs.GetBool(PrefsPrefix + nameof(AutoUpdateOnFocus), true);
            set => EditorPrefs.SetBool(PrefsPrefix + nameof(AutoUpdateOnFocus), value);
        }

        public int ThumbnailSize
        {
            get => EditorPrefs.GetInt(PrefsPrefix + nameof(ThumbnailSize), 100);
            set => EditorPrefs.SetInt(PrefsPrefix + nameof(ThumbnailSize), value);
        }

        public bool ShowHotspots
        {
            get => EditorPrefs.GetBool(PrefsPrefix + nameof(ShowHotspots), false);
            set => EditorPrefs.SetBool(PrefsPrefix + nameof(ShowHotspots), value);
        }
    }
}