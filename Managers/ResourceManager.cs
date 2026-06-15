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
        private static bool _bundleMissingWarningShown;

        // 初始化加载 AssetBundle 或者散装资源目录
        public static void Initialize(string bundlePath)
        {
            if (_uiBundle != null) return;

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
                if (!_bundleMissingWarningShown)
                {
                    _bundleMissingWarningShown = true;
                    MelonLogger.Warning($"AssetBundle not found at: {bundlePath}, will attempt to load loose Assets.");
                }
            }
        }

        // 获取贴图，优先查 Bundle，次查散装文件，缺失时返回兜底紫黑图防止崩溃
        public static Sprite GetSprite(string name)
        {
            if (_spriteCache.TryGetValue(name, out var cached))
            {
                if (TryGetTexture(cached, out _))
                {
                    return cached;
                }

                _spriteCache.Remove(name);
            }

            if (_uiBundle != null)
            {
                try
                {
                    var obj = _uiBundle.LoadAsset(name);
                    if (obj != null)
                    {
                        var tex = obj.Cast<Texture2D>();
                        if (tex != null)
                        {
                            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                            _spriteCache[name] = sprite;
                            MelonLogger.Msg($"Successfully loaded texture from bundle: {name}");
                            return sprite;
                        }
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"Failed to load bundle texture {name}: {ex.Message}");
                }
            }

            // 1.5 尝试从嵌入的 DLL 资源中加载
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            string[] allRes = assembly.GetManifestResourceNames();
            string targetRes = null;
            foreach (var res in allRes)
            {
                if (res.EndsWith(name, StringComparison.OrdinalIgnoreCase))
                {
                    targetRes = res;
                    break;
                }
            }

            if (targetRes != null)
            {
                try
                {
                    using (var stream = assembly.GetManifestResourceStream(targetRes))
                    {
                        if (stream != null)
                        {
                            byte[] fileData = new byte[stream.Length];
                            stream.Read(fileData, 0, (int)stream.Length);
                            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                            if (ImageConversion.LoadImage(tex, fileData))
                            {
                                var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                                _spriteCache[name] = sprite;
                                MelonLogger.Msg($"Successfully loaded embedded texture: {name}");
                                return sprite;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"Failed to read embedded texture {name}: {ex}");
                }
            }

            // 2. 尝试从散装目录加载
            string assetsDir = System.IO.Path.Combine(MelonLoader.Utils.MelonEnvironment.UserDataDirectory, Constants.ModName, Constants.AssetsDirName);
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

        public static Texture2D GetTexture(string name)
        {
            var sprite = GetSprite(name);
            return TryGetTexture(sprite, out var texture) ? texture : null;
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

        private static List<string> _randomBannerResourceNames;

        // 从嵌入的 DLL 资源中随机获取一张贴图作为 Banner
        public static Texture2D GetRandomBannerTexture()
        {
            if (_randomBannerResourceNames == null)
            {
                _randomBannerResourceNames = new List<string>();
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                string[] allRes = assembly.GetManifestResourceNames();

                foreach (var res in allRes)
                {
                    if (res.Contains("Banners") && res.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    {
                        _randomBannerResourceNames.Add(res);
                    }
                }

                if (_randomBannerResourceNames.Count > 0)
                    MelonLogger.Msg($"Successfully loaded {_randomBannerResourceNames.Count} embedded random banners.");
                else
                    MelonLogger.Warning("No embedded banners found.");
            }

            if (_randomBannerResourceNames != null && _randomBannerResourceNames.Count > 0)
            {
                int index = UnityEngine.Random.Range(0, _randomBannerResourceNames.Count);
                return GetTexture(_randomBannerResourceNames[index]);
            }
            return null;
        }

        private static bool TryGetTexture(Sprite sprite, out Texture2D texture)
        {
            texture = null;
            if (sprite == null) return false;

            try
            {
                texture = sprite.texture;
                return texture != null;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Invalid sprite texture skipped: {ex.Message}");
                return false;
            }
        }
    }
}
