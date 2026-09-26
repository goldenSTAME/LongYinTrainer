using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using UnityEngine;

public static partial class TrainerBehaviour
{
    // This index contains only current game assets, never exported PNG metadata.
    private sealed class RuntimeIconRegion
    {
        public Texture2D Texture = null!;
        public Rect Rect;
        public Sprite? Sprite;
    }

    private static readonly Dictionary<string, RuntimeIconRegion> RuntimeIconRegions = new(StringComparer.Ordinal);
    private static bool _runtimeIconsLoaded;
    private static bool _runtimeIconWarningLogged;
    private static float _nextRuntimeIconRetry;
    private static readonly Dictionary<string, Sprite> NativeItemSprites = new(StringComparer.Ordinal);

    private static void RefreshRuntimeIconIndex()
    {
        _nextRuntimeIconRetry = Time.realtimeSinceStartup + 5f;
        try
        {
            // The actual game icons also use native Unity Sprites. Keep the
            // original sprite (including packed UVs); never destroy game assets.
            NativeItemSprites.Clear();
            foreach (var obj in Resources.FindObjectsOfTypeAll(Il2CppType.Of<Sprite>()))
            {
                var sprite = obj?.TryCast<Sprite>();
                if (sprite == null || string.IsNullOrEmpty(sprite.name) || sprite.name.StartsWith("Trainer")) continue;
                NativeItemSprites.TryAdd(sprite.name, sprite);
            }
            // Load atlas assets directly. Do not instantiate inventory prefabs or
            // run their controllers: doing so can alter the live inventory UI.
            var ngui = Resources.FindObjectsOfTypeAll(Il2CppType.Of<NGUIAtlas>());
            var legacy = Resources.FindObjectsOfTypeAll(Il2CppType.Of<UIAtlas>());
            var found = new Dictionary<string, RuntimeIconRegion>(StringComparer.Ordinal);
            foreach (var obj in ngui)
            {
                var atlas = obj?.TryCast<NGUIAtlas>();
                if (atlas != null) IndexRuntimeAtlas(found, atlas.texture?.TryCast<Texture2D>(), atlas.spriteList);
            }
            foreach (var obj in legacy)
            {
                var atlas = obj?.TryCast<UIAtlas>();
                if (atlas != null) IndexRuntimeAtlas(found, atlas.texture?.TryCast<Texture2D>(), atlas.spriteList);
            }
            foreach (var pair in RuntimeIconRegions)
                if ((!found.TryGetValue(pair.Key, out var next) || !ReferenceEquals(next, pair.Value)) && pair.Value.Sprite != null)
                    UnityEngine.Object.Destroy(pair.Value.Sprite);
            RuntimeIconRegions.Clear();
            foreach (var pair in found) RuntimeIconRegions.Add(pair.Key, pair.Value);
            _runtimeIconsLoaded = true;
            LongYinTrainerPlugin.Logger.LogInfo($"Runtime-only icon index: {NativeItemSprites.Count} native sprites, {found.Count} atlas regions; PNG fallback DISABLED.");
        }
        catch (Exception ex)
        {
            if (!_runtimeIconWarningLogged)
            {
                _runtimeIconWarningLogged = true;
                LongYinTrainerPlugin.Logger.LogWarning($"Runtime icon discovery failed; PNG fallback DISABLED: {ex}");
            }
        }
    }

    private static void IndexRuntimeAtlas(Dictionary<string, RuntimeIconRegion> found, Texture2D? texture,
        Il2CppSystem.Collections.Generic.List<UISpriteData>? sprites)
    {
        if (texture == null || sprites == null) return;
        foreach (var data in sprites)
        {
            if (data == null || string.IsNullOrEmpty(data.name) || found.ContainsKey(data.name)) continue;
            var y = texture.height - data.y - data.height;
            if (data.width <= 0 || data.height <= 0 || data.x < 0 || y < 0 ||
                data.x + data.width > texture.width || y + data.height > texture.height) continue;
            var rect = new Rect(data.x, y, data.width, data.height);
            if (RuntimeIconRegions.TryGetValue(data.name, out var old) && old.Texture == texture && old.Rect == rect)
                found.Add(data.name, old);
            else found.Add(data.name, new RuntimeIconRegion { Texture = texture, Rect = rect });
        }
    }

    private static Sprite? GetRuntimeItemSprite(string name)
    {
        if (!_runtimeIconsLoaded && Time.realtimeSinceStartup >= _nextRuntimeIconRetry) RefreshRuntimeIconIndex();
        if (NativeItemSprites.TryGetValue(name, out var native) && native != null) return native;
        if (!RuntimeIconRegions.TryGetValue(name, out var region) || region.Texture == null) return null;
        if (region.Sprite != null) return region.Sprite;
        try
        {
            // Sprite.Create references the game's texture; no CPU readback or
            // isReadable requirement, and only visible icons allocate sprites.
            region.Sprite = Sprite.Create(region.Texture, region.Rect, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            region.Sprite.name = "TrainerRuntime_" + name;
            return region.Sprite;
        }
        catch (Exception ex)
        {
            if (!_runtimeIconWarningLogged)
            {
                _runtimeIconWarningLogged = true;
                LongYinTrainerPlugin.Logger.LogWarning($"Runtime icon '{name}' failed: {ex.Message}");
            }
            return null;
        }
    }
}
