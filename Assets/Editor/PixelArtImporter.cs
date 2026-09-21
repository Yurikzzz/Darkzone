using UnityEngine;
using UnityEditor;

public class PixelArtImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        TextureImporter importer = (TextureImporter)assetImporter;

        // 'importSettingsMissing' is true ONLY when a file is imported for the very first time.
        // This sets perfect defaults, but still allows you to manually tweak them later if needed.
        if (importer.importSettingsMissing)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 32;
            
            // Critical settings for crisp pixel art
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
        }
    }
}
