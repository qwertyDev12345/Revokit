using System;
using UnityEditor;
using UnityEngine;

public static class ConesTextureSettings
{
    [MenuItem("Revokit/Optimize UI artwork")]
    public static void Apply()
    {
        const string path = "Assets/Cones/Resources/ConesHero.jpg";
        AssetDatabase.Refresh();
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Cones artwork is missing.");
        importer.maxTextureSize = 1024;
        importer.mipmapEnabled = false;
        importer.isReadable = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.alphaSource = TextureImporterAlphaSource.None;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Compressed;
        var ios = importer.GetPlatformTextureSettings("iPhone");
        ios.name = "iPhone";
        ios.overridden = true;
        ios.maxTextureSize = 1024;
        ios.format = TextureImporterFormat.ASTC_6x6;
        ios.compressionQuality = 100;
        importer.SetPlatformTextureSettings(ios);
        importer.SaveAndReimport();
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture == null || texture.width > 1024 || texture.height > 1024 || texture.mipmapCount != 1 || texture.isReadable)
            throw new InvalidOperationException("Artwork import did not match its size/memory settings.");
        ios = importer.GetPlatformTextureSettings("iPhone");
        if (!ios.overridden || ios.format != TextureImporterFormat.ASTC_6x6)
            throw new InvalidOperationException("iOS artwork compression was not applied.");
        Debug.Log($"CONES_ARTWORK_PASS: {texture.width}x{texture.height}, {texture.format}, no mipmaps/readable copy; iOS ASTC 6x6, max 1024.");
    }
}
