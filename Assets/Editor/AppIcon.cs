using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Aib.Editor
{
    /// <summary>
    /// Sets Assets/Art/AppIcon.png (made by Tools/gen_icon.py) as the default icon for all platforms.
    /// Menu: Ayb → Apply App Icon, or from the command line:
    ///   Unity -batchmode -quit -projectPath . -executeMethod Aib.Editor.AppIcon.Apply
    /// It is also applied once automatically when the editor loads and no default icon is set yet.
    /// The web favicon is a plain file in Assets/WebGLTemplates/Ayb and needs no step here.
    /// </summary>
    [InitializeOnLoad]
    public static class AppIcon
    {
        const string Path = "Assets/Art/AppIcon.png";

        static AppIcon()
        {
            EditorApplication.delayCall += () =>
            {
                var icons = PlayerSettings.GetIcons(NamedBuildTarget.Unknown, IconKind.Any);
                if ((icons.Length == 0 || icons[0] == null) && System.IO.File.Exists(Path)) Apply();
            };
        }

        [MenuItem("Ayb/Apply App Icon")]
        public static void Apply()
        {
            AssetDatabase.ImportAsset(Path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(Path);
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();

            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(Path);
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            AssetDatabase.SaveAssets();
            Debug.Log("App icon set: " + Path);
        }
    }
}
