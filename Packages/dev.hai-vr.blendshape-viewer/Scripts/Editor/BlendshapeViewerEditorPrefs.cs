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
            get => EditorPrefs.GetBool(PrefsPrefix + nameof(AutoUpdateOnFocus), false);
            set => EditorPrefs.SetBool(PrefsPrefix + nameof(AutoUpdateOnFocus), value);
        }

        public int ThumbnailSize
        {
            get => EditorPrefs.GetInt(PrefsPrefix + nameof(ThumbnailSize), 100);
            set => EditorPrefs.SetInt(PrefsPrefix + nameof(ThumbnailSize), value);
        }

        public AltRole AltAction
        {
            get => (AltRole)EditorPrefs.GetInt(PrefsPrefix + nameof(AltAction), (int)AltRole.AltToShowOriginal);
            set => EditorPrefs.SetInt(PrefsPrefix + nameof(AltAction), (int)value);
        }
    }

    internal enum AltRole
    {
        DoNothing = 0,
        AltToShowOriginal = 1,
        AltToShowHotspots = 2,
        ShowHotspotsByDefault = 3,
    }
}