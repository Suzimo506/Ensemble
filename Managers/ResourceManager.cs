using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using MelonLoader;

namespace MDEN.Managers
{
    // 从 AssetBundle 加载 UI 素材
    public static class ResourceManager
    {
        private static AssetBundle _uiBundle;
        private static readonly Dictionary<string, Sprite> _spriteCache = new Dictionary<string, Sprite>();

        // 初始化加载 AssetBundle 或者散装资源目录
        public static void Initialize(string bundlePath)
        {
            // 如果存在 bundle 则加载 bundle
            if (File.Exists(bundlePath))
            {
                try
                {
                    _uiBundle = AssetBundle.LoadFromFile(bundlePath);
                    MelonLogger.Msg("UI AssetBundle loaded successfully!");
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"Failed to load AssetBundle: {ex}");
                }
            }
            else
            {
                MelonLogger.Warning($"AssetBundle not found at: {bundlePath}, will attempt to load loose Assets.");
            }
        }

        // 获取贴图，优先查 Bundle，次查散装文件，缺失时返回兜底紫黑图防止崩溃
        public static Sprite GetSprite(string name)
        {
            if (_spriteCache.TryGetValue(name, out var cached))
            {
                return cached;
            }

            // 1. 尝试从 Bundle 加载
            if (_uiBundle != null)
            {
                var obj = _uiBundle.LoadAsset(name);
                if (obj != null)
                {
                    var sprite = obj.Cast<Sprite>();
                    _spriteCache[name] = sprite;
                    return sprite;
                }
            }

            // 2. 尝试从散装目录加载
            string assetsDir = System.IO.Path.Combine(MelonLoader.Utils.MelonEnvironment.UserDataDirectory, "MDEN", "Assets");
            if (Directory.Exists(assetsDir))
            {
                try
                {
                    var files = Directory.GetFiles(assetsDir, name, SearchOption.AllDirectories);
                    if (files.Length > 0)
                    {
                        string filePath = files[0];
                        byte[] fileData = File.ReadAllBytes(filePath);
                        // 关闭 mipmap 生成，防止 UI 缩放时变得模糊
                        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                        if (ImageConversion.LoadImage(tex, fileData))
                        {
                            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                            _spriteCache[name] = sprite;
                            MelonLogger.Msg($"Successfully loaded loose texture: {name}");
                            return sprite;
                        }
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"Failed to read loose texture {name}: {ex}");
                }
            }

            MelonLogger.Warning($"Missing texture: {name}, returning fallback.");
            var fallback = CreateFallbackSprite();
            _spriteCache[name] = fallback;
            return fallback;
        }

        // 生成紫黑色错误提示图
        private static Sprite CreateFallbackSprite()
        {
            var tex = new Texture2D(64, 64);
            var colors = new Color[64 * 64];
            for (int i = 0; i < colors.Length; i++) 
            {
                colors[i] = new Color(0.5f, 0f, 0.5f);
            }
            tex.SetPixels(colors);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f));
        }
    }
}
