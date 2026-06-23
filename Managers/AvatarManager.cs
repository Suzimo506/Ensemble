using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;

namespace MDEN.Managers
{
    public static class AvatarManager
    {
        public const string DefaultAvatarName = "head_0";

        private const string AvatarDirectoryName = "Avatars";
        private const string AvatarLibraryDirectoryName = "AvatarLibrary";
        private const string CustomAvatarPrefix = "avatar_";
        private const int AvatarSize = 160;
        private const int MinAvatarSize = 64;
        private const int MaxSourceBytes = 4 * 1024 * 1024;
        private const int MaxSourceDimension = 2048;
        private const int MaxAvatarPngBytes = 49152;
        private const int MaxLibraryItems = 128;

        private static readonly Dictionary<string, Sprite> SpriteCache = new Dictionary<string, Sprite>();
        private static readonly Dictionary<string, PreviewCacheEntry> PreviewCache = new Dictionary<string, PreviewCacheEntry>(StringComparer.OrdinalIgnoreCase);
        private static readonly int[] AvatarEncodeSizes = { AvatarSize, 128, 96, 80, MinAvatarSize };
        private static Sprite _defaultSprite;

        public static Sprite GetCurrentAvatarSprite()
        {
            return GetAvatarSprite(PlayerManager.CurrentUid, ModConfigManager.PlayerAvatarName, GetLocalAvatarData());
        }

        public static Texture2D GetCurrentAvatarTexture()
        {
            return GetTexture(GetCurrentAvatarSprite());
        }

        public static Sprite GetAvatarSprite(string uid, string avatarName, string avatarData = null)
        {
            var normalizedName = NormalizeAvatarName(avatarName);
            if (!IsCustomAvatarName(normalizedName))
            {
                return GetDefaultAvatarSprite();
            }

            var cacheKey = normalizedName;
            if (SpriteCache.TryGetValue(cacheKey, out var cached) && GetTexture(cached) != null)
            {
                return cached;
            }

            if (!TryGetCustomAvatarBytes(normalizedName, avatarData, out var avatarBytes))
            {
                return GetDefaultAvatarSprite();
            }

            var sprite = CreateSpriteFromImageBytes(avatarBytes, normalizedName);
            if (sprite == null)
            {
                return GetDefaultAvatarSprite();
            }

            SpriteCache[cacheKey] = sprite;
            return sprite;
        }

        public static Texture2D GetAvatarTexture(string uid, string avatarName, string avatarData = null)
        {
            return GetTexture(GetAvatarSprite(uid, avatarName, avatarData));
        }

        public static string GetLocalAvatarData()
        {
            var avatarName = NormalizeAvatarName(ModConfigManager.PlayerAvatarName);
            if (!IsCustomAvatarName(avatarName)) return null;

            var path = GetAvatarPath(avatarName);
            if (path == null || !File.Exists(path)) return null;

            try
            {
                var info = new FileInfo(path);
                if (info.Length <= 0 || info.Length > MaxAvatarPngBytes) return null;

                var bytes = File.ReadAllBytes(path);
                if (!IsSafeImageBytes(bytes)) return null;
                if (!AvatarNameMatchesBytes(avatarName, bytes)) return null;
                return Convert.ToBase64String(bytes);
            }
            catch (Exception ex)
            {
                ClientLogManager.Warning($"Read local avatar failed: {ex.Message}");
                return null;
            }
        }

        public static string ImportAvatarFromPath(string sourcePath)
        {
            var safePath = NormalizeSourcePath(sourcePath);
            if (string.IsNullOrWhiteSpace(safePath) || !File.Exists(safePath))
            {
                throw new InvalidOperationException("头像文件不存在");
            }

            var extension = Path.GetExtension(safePath)?.ToLowerInvariant();
            if (extension != ".png" && extension != ".jpg" && extension != ".jpeg")
            {
                throw new InvalidOperationException("头像只支持 png、jpg、jpeg");
            }

            var info = new FileInfo(safePath);
            if (info.Length <= 0 || info.Length > MaxSourceBytes)
            {
                throw new InvalidOperationException("头像文件过大，最大支持 4MB");
            }

            var sourceBytes = File.ReadAllBytes(safePath);
            if (!TryGetImageDimensions(sourceBytes, out var sourceWidth, out var sourceHeight) ||
                sourceWidth <= 0 ||
                sourceHeight <= 0 ||
                sourceWidth > MaxSourceDimension ||
                sourceHeight > MaxSourceDimension)
            {
                throw new InvalidOperationException("头像尺寸无效，最大支持 2048x2048");
            }

            var sourceTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Texture2D circular = null;
            try
            {
                if (!ImageConversion.LoadImage(sourceTexture, sourceBytes))
                {
                    throw new InvalidOperationException("头像图片读取失败");
                }

                if (sourceTexture.width <= 0 ||
                    sourceTexture.height <= 0 ||
                    sourceTexture.width > MaxSourceDimension ||
                    sourceTexture.height > MaxSourceDimension)
                {
                    throw new InvalidOperationException("头像尺寸无效，最大支持 2048x2048");
                }

                if (!TryCreateEncodedAvatar(sourceTexture, out circular, out var pngBytes))
                {
                    throw new InvalidOperationException("头像压缩后仍太大，请换一张尺寸更小或细节更少的图片");
                }

                var avatarName = BuildAvatarName(pngBytes);
                var avatarPath = GetAvatarPath(avatarName);
                if (avatarPath == null)
                {
                    throw new InvalidOperationException("头像保存路径无效");
                }

                Directory.CreateDirectory(Path.GetDirectoryName(avatarPath));
                File.WriteAllBytes(avatarPath, pngBytes);
                CacheAvatarSprite(avatarName, circular);
                circular = null;
                return avatarName;
            }
            finally
            {
                if (circular != null)
                {
                    UnityEngine.Object.Destroy(circular);
                }

                UnityEngine.Object.Destroy(sourceTexture);
            }
        }

        private static bool TryCreateEncodedAvatar(Texture2D sourceTexture, out Texture2D circular, out byte[] pngBytes)
        {
            circular = null;
            pngBytes = null;

            foreach (var size in AvatarEncodeSizes)
            {
                Texture2D candidate = null;
                try
                {
                    candidate = CreateCircularTexture(sourceTexture, size);
                    var bytes = ImageConversion.EncodeToPNG(candidate);
                    if (bytes != null && bytes.Length > 0 && bytes.Length <= MaxAvatarPngBytes)
                    {
                        circular = candidate;
                        pngBytes = bytes;
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    ClientLogManager.Warning($"Encode avatar failed at {size}px: {ex.Message}");
                }

                if (candidate != null)
                {
                    UnityEngine.Object.Destroy(candidate);
                }
            }

            return false;
        }

        public static AvatarLibraryItem[] GetAvatarLibraryItems(bool loadPreviewTextures = true)
        {
            var folder = GetAvatarLibraryFolder();
            var items = new List<AvatarLibraryItem>();
            items.Add(new AvatarLibraryItem(
                AvatarLibraryItem.DefaultId,
                "默认头像",
                null,
                loadPreviewTextures ? GetTexture(GetDefaultAvatarSprite()) : null));

            if (!Directory.Exists(folder))
            {
                TryCreateDirectory(folder);
                if (loadPreviewTextures)
                {
                    TrimPreviewCache(new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                }

                return items.ToArray();
            }

            var visiblePreviewPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in EnumerateAvatarSourceFiles(folder))
            {
                if (items.Count >= MaxLibraryItems + 1) break;
                if (!TryCreateLibraryItem(path, loadPreviewTextures, out var item))
                {
                    RemovePreviewCache(path);
                    continue;
                }

                items.Add(item);
                if (loadPreviewTextures)
                {
                    var previewPath = NormalizeSourcePath(path);
                    if (!string.IsNullOrWhiteSpace(previewPath))
                    {
                        visiblePreviewPaths.Add(previewPath);
                    }
                }
            }

            if (loadPreviewTextures)
            {
                TrimPreviewCache(visiblePreviewPaths);
            }

            return items.ToArray();
        }

        public static string ImportAvatarFromLibraryItem(AvatarLibraryItem item)
        {
            if (item == null || item.IsDefault)
            {
                return DefaultAvatarName;
            }

            return ImportAvatarFromPath(item.Path);
        }

        public static string GetAvatarLibraryFolder()
        {
            var folder = ModConfigManager.PlayerAvatarLibraryPath;
            if (string.IsNullOrWhiteSpace(folder))
            {
                folder = GetDefaultAvatarLibraryFolder();
            }

            return NormalizeFolderPath(folder) ?? GetDefaultAvatarLibraryFolder();
        }

        public static string GetDefaultAvatarLibraryFolder()
        {
            return Path.Combine(
                MelonLoader.Utils.MelonEnvironment.UserDataDirectory,
                Constants.ModName,
                AvatarLibraryDirectoryName);
        }

        public static bool TryValidateAvatarLibraryFolder(string folderPath, out string normalizedPath, out string error)
        {
            normalizedPath = NormalizeFolderPath(folderPath);
            error = null;

            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                error = "头像文件夹路径不能为空";
                return false;
            }

            if (!Directory.Exists(normalizedPath))
            {
                error = "头像文件夹不存在";
                return false;
            }

            return true;
        }

        public static string NormalizeAvatarName(string avatarName)
        {
            if (string.IsNullOrWhiteSpace(avatarName)) return DefaultAvatarName;

            var value = avatarName.Trim();
            if (value == DefaultAvatarName) return DefaultAvatarName;
            return IsCustomAvatarName(value) ? value.ToLowerInvariant() : DefaultAvatarName;
        }

        private static bool TryGetCustomAvatarBytes(string avatarName, string avatarData, out byte[] bytes)
        {
            bytes = null;

            if (!string.IsNullOrWhiteSpace(avatarData) && TryDecodeAvatarData(avatarName, avatarData, out bytes))
            {
                TrySaveAvatarBytes(avatarName, bytes);
                return true;
            }

            var path = GetAvatarPath(avatarName);
            if (path == null || !File.Exists(path)) return false;

            try
            {
                var info = new FileInfo(path);
                if (info.Length <= 0 || info.Length > MaxAvatarPngBytes) return false;

                bytes = File.ReadAllBytes(path);
                return IsSafeImageBytes(bytes) && AvatarNameMatchesBytes(avatarName, bytes);
            }
            catch
            {
                bytes = null;
                return false;
            }
        }

        public static bool IsValidAvatarPayload(string avatarName, string avatarData)
        {
            return TryDecodeAvatarData(avatarName, avatarData, out _);
        }

        private static bool TryDecodeAvatarData(string avatarName, string avatarData, out byte[] bytes)
        {
            bytes = null;
            if (string.IsNullOrWhiteSpace(avatarData)) return false;
            if (avatarData.Length > MaxAvatarPngBytes * 2) return false;

            try
            {
                bytes = Convert.FromBase64String(avatarData);
            }
            catch
            {
                return false;
            }

            return bytes.Length > 0 &&
                   bytes.Length <= MaxAvatarPngBytes &&
                   IsSafeImageBytes(bytes) &&
                   AvatarNameMatchesBytes(avatarName, bytes);
        }

        private static void TrySaveAvatarBytes(string avatarName, byte[] bytes)
        {
            var path = GetAvatarPath(avatarName);
            if (path == null || bytes == null || bytes.Length == 0 || bytes.Length > MaxAvatarPngBytes) return;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                if (!File.Exists(path))
                {
                    File.WriteAllBytes(path, bytes);
                }
            }
            catch (Exception ex)
            {
                ClientLogManager.Warning($"Cache avatar failed: {ex.Message}");
            }
        }

        private static Sprite CreateSpriteFromImageBytes(byte[] bytes, string avatarName, bool allowSourceImage = false)
        {
            var circular = CreateCircularTextureFromImageBytes(bytes, allowSourceImage);
            if (circular == null) return null;

            CacheAvatarSprite(avatarName, circular);
            return SpriteCache[avatarName];
        }

        private static Texture2D CreateCircularTextureFromImageBytes(byte[] bytes, bool allowSourceImage = false)
        {
            if (allowSourceImage)
            {
                if (!IsSafeSourceImageBytes(bytes)) return null;
            }
            else if (!IsSafeImageBytes(bytes))
            {
                return null;
            }

            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!ImageConversion.LoadImage(source, bytes)) return null;
                if (source.width <= 0 ||
                    source.height <= 0 ||
                    source.width > MaxSourceDimension ||
                    source.height > MaxSourceDimension)
                {
                    return null;
                }

                var circular = CreateCircularTexture(source, AvatarSize);
                return circular;
            }
            catch (Exception ex)
            {
                ClientLogManager.Warning($"Load avatar failed: {ex.Message}");
                return null;
            }
            finally
            {
                UnityEngine.Object.Destroy(source);
            }
        }

        private static void CacheAvatarSprite(string avatarName, Texture2D texture)
        {
            if (texture == null) return;

            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            if (SpriteCache.TryGetValue(avatarName, out var existing))
            {
                var existingTexture = GetTexture(existing);
                if (existingTexture != null)
                {
                    if (existingTexture != texture)
                    {
                        UnityEngine.Object.Destroy(texture);
                    }

                    return;
                }

                DestroySprite(existing, texture);
            }

            SpriteCache[avatarName] = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f));
        }

        private static void DestroySprite(Sprite sprite, Texture2D replacementTexture = null)
        {
            if (sprite == null) return;

            var texture = GetTexture(sprite);
            UnityEngine.Object.Destroy(sprite);
            if (texture != null && texture != replacementTexture)
            {
                UnityEngine.Object.Destroy(texture);
            }
        }

        private static Sprite GetDefaultAvatarSprite()
        {
            if (_defaultSprite != null && GetTexture(_defaultSprite) != null)
            {
                return _defaultSprite;
            }

            var defaultBytes = TryReadDefaultAvatarBytes();
            if (defaultBytes != null && IsSafeSourceImageBytes(defaultBytes))
            {
                var sprite = CreateSpriteFromImageBytes(defaultBytes, DefaultAvatarName, true);
                if (sprite != null)
                {
                    _defaultSprite = sprite;
                    return _defaultSprite;
                }
            }

            var texture = CreateGeneratedDefaultAvatar();
            _defaultSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f));
            return _defaultSprite;
        }

        private static byte[] TryReadDefaultAvatarBytes()
        {
            foreach (var path in GetDefaultAvatarPaths())
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) continue;

                    var info = new FileInfo(path);
                    if (info.Length <= 0 || info.Length > MaxSourceBytes) continue;
                    return File.ReadAllBytes(path);
                }
                catch
                {
                }
            }

            return null;
        }

        private static IEnumerable<string> GetDefaultAvatarPaths()
        {
            var avatarDir = GetAvatarDirectory();
            yield return Path.Combine(avatarDir, "default.png");
            yield return Path.Combine(avatarDir, "head_0.png");
            yield return Path.Combine(
                MelonLoader.Utils.MelonEnvironment.UserDataDirectory,
                Constants.ModName,
                Constants.AssetsDirName,
                AvatarDirectoryName,
                "head_0.png");
        }

        private static Texture2D CreateGeneratedDefaultAvatar()
        {
            var texture = new Texture2D(AvatarSize, AvatarSize, TextureFormat.RGBA32, false);
            var pixels = new Color[AvatarSize * AvatarSize];
            var center = (AvatarSize - 1) * 0.5f;
            var radius = AvatarSize * 0.5f - 1f;

            for (var y = 0; y < AvatarSize; y++)
            {
                for (var x = 0; x < AvatarSize; x++)
                {
                    var dx = x - center;
                    var dy = y - center;
                    var distance = Mathf.Sqrt(dx * dx + dy * dy);
                    var alpha = Mathf.Clamp01((radius + 0.75f - distance) / 2f);
                    var t = Mathf.Clamp01((x + y) / (float)(AvatarSize * 2));
                    var baseColor = Color.Lerp(new Color(1f, 0.38f, 0.72f, 1f), new Color(0.20f, 0.78f, 1f, 1f), t);
                    var shine = Mathf.Clamp01(1f - distance / radius) * 0.22f;
                    pixels[y * AvatarSize + x] = new Color(
                        Mathf.Clamp01(baseColor.r + shine),
                        Mathf.Clamp01(baseColor.g + shine),
                        Mathf.Clamp01(baseColor.b + shine),
                        alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            return texture;
        }

        private static Texture2D CreateCircularTexture(Texture2D source, int size)
        {
            var output = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            var sourceSide = Mathf.Min(source.width, source.height);
            var sourceX = (source.width - sourceSide) * 0.5f;
            var sourceY = (source.height - sourceSide) * 0.5f;
            var center = (size - 1) * 0.5f;
            var radius = size * 0.5f - 1f;

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var u = (sourceX + (x + 0.5f) / size * sourceSide) / source.width;
                    var v = (sourceY + (y + 0.5f) / size * sourceSide) / source.height;
                    var color = source.GetPixelBilinear(u, v);
                    var dx = x - center;
                    var dy = y - center;
                    var distance = Mathf.Sqrt(dx * dx + dy * dy);
                    var alpha = Mathf.Clamp01((radius + 0.75f - distance) / 2f);
                    color.a *= alpha;
                    pixels[y * size + x] = color;
                }
            }

            output.SetPixels(pixels);
            output.Apply(false, false);
            output.wrapMode = TextureWrapMode.Clamp;
            output.filterMode = FilterMode.Bilinear;
            return output;
        }

        private static bool IsSafeImageBytes(byte[] bytes)
        {
            return bytes != null &&
                   bytes.Length > 0 &&
                   bytes.Length <= MaxAvatarPngBytes &&
                   TryGetImageDimensions(bytes, out var width, out var height) &&
                   width > 0 &&
                   height > 0 &&
                   width <= MaxSourceDimension &&
                   height <= MaxSourceDimension;
        }

        private static bool IsSafeSourceImageBytes(byte[] bytes)
        {
            return bytes != null &&
                   bytes.Length > 0 &&
                   bytes.Length <= MaxSourceBytes &&
                   TryGetImageDimensions(bytes, out var width, out var height) &&
                   width > 0 &&
                   height > 0 &&
                   width <= MaxSourceDimension &&
                   height <= MaxSourceDimension;
        }

        private static bool TryGetImageDimensions(byte[] bytes, out int width, out int height)
        {
            width = 0;
            height = 0;
            if (bytes == null || bytes.Length < 10) return false;
            return TryGetPngDimensions(bytes, out width, out height) ||
                   TryGetJpegDimensions(bytes, out width, out height);
        }

        private static bool TryGetPngDimensions(byte[] bytes, out int width, out int height)
        {
            width = 0;
            height = 0;
            if (bytes.Length < 24) return false;
            if (bytes[0] != 0x89 || bytes[1] != 0x50 || bytes[2] != 0x4E || bytes[3] != 0x47 ||
                bytes[4] != 0x0D || bytes[5] != 0x0A || bytes[6] != 0x1A || bytes[7] != 0x0A)
            {
                return false;
            }

            if (bytes[12] != 0x49 || bytes[13] != 0x48 || bytes[14] != 0x44 || bytes[15] != 0x52)
            {
                return false;
            }

            width = ReadBigEndianInt32(bytes, 16);
            height = ReadBigEndianInt32(bytes, 20);
            return width > 0 && height > 0;
        }

        private static bool TryGetJpegDimensions(byte[] bytes, out int width, out int height)
        {
            width = 0;
            height = 0;
            if (bytes.Length < 4 || bytes[0] != 0xFF || bytes[1] != 0xD8) return false;

            var index = 2;
            while (index + 9 < bytes.Length)
            {
                if (bytes[index] != 0xFF)
                {
                    index++;
                    continue;
                }

                while (index < bytes.Length && bytes[index] == 0xFF) index++;
                if (index >= bytes.Length) return false;

                var marker = bytes[index++];
                if (marker == 0xD8 || marker == 0xD9) continue;
                if (index + 1 >= bytes.Length) return false;

                var length = (bytes[index] << 8) + bytes[index + 1];
                if (length < 2 || index + length > bytes.Length) return false;

                if (IsJpegStartOfFrame(marker))
                {
                    height = (bytes[index + 3] << 8) + bytes[index + 4];
                    width = (bytes[index + 5] << 8) + bytes[index + 6];
                    return width > 0 && height > 0;
                }

                index += length;
            }

            return false;
        }

        private static bool IsJpegStartOfFrame(byte marker)
        {
            return (marker >= 0xC0 && marker <= 0xC3) ||
                   (marker >= 0xC5 && marker <= 0xC7) ||
                   (marker >= 0xC9 && marker <= 0xCB) ||
                   (marker >= 0xCD && marker <= 0xCF);
        }

        private static int ReadBigEndianInt32(byte[] bytes, int index)
        {
            return (bytes[index] << 24) |
                   (bytes[index + 1] << 16) |
                   (bytes[index + 2] << 8) |
                   bytes[index + 3];
        }

        private static bool AvatarNameMatchesBytes(string avatarName, byte[] bytes)
        {
            if (!IsCustomAvatarName(avatarName)) return false;
            return BuildAvatarName(bytes) == NormalizeAvatarName(avatarName);
        }

        private static string BuildAvatarName(byte[] bytes)
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(bytes);
            return CustomAvatarPrefix + BitConverter.ToString(hash, 0, 8).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static bool IsCustomAvatarName(string avatarName)
        {
            if (string.IsNullOrWhiteSpace(avatarName) ||
                avatarName.Length != CustomAvatarPrefix.Length + 16 ||
                !avatarName.StartsWith(CustomAvatarPrefix, StringComparison.Ordinal))
            {
                return false;
            }

            for (var i = CustomAvatarPrefix.Length; i < avatarName.Length; i++)
            {
                if (!Uri.IsHexDigit(avatarName[i])) return false;
            }

            return true;
        }

        private static string GetAvatarPath(string avatarName)
        {
            avatarName = NormalizeAvatarName(avatarName);
            if (!IsCustomAvatarName(avatarName)) return null;

            var directory = GetAvatarDirectory();
            var combined = Path.Combine(directory, avatarName + ".png");
            var fullDirectory = Path.GetFullPath(directory);
            var fullPath = Path.GetFullPath(combined);
            return fullPath.StartsWith(fullDirectory, StringComparison.OrdinalIgnoreCase) ? fullPath : null;
        }

        private static string GetAvatarDirectory()
        {
            return Path.Combine(MelonLoader.Utils.MelonEnvironment.UserDataDirectory, Constants.ModName, AvatarDirectoryName);
        }

        private static string NormalizeSourcePath(string sourcePath)
        {
            return string.IsNullOrWhiteSpace(sourcePath)
                ? null
                : sourcePath.Trim().Trim('"');
        }

        private static string NormalizeFolderPath(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath)) return null;

            try
            {
                return Path.GetFullPath(folderPath.Trim().Trim('"'));
            }
            catch
            {
                return null;
            }
        }

        private static IEnumerable<string> EnumerateAvatarSourceFiles(string folder)
        {
            string[] files;
            try
            {
                files = Directory.GetFiles(folder, "*.*", SearchOption.TopDirectoryOnly);
            }
            catch
            {
                yield break;
            }

            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            foreach (var file in files)
            {
                var extension = Path.GetExtension(file)?.ToLowerInvariant();
                if (extension == ".png" || extension == ".jpg" || extension == ".jpeg")
                {
                    yield return file;
                }
            }
        }

        private static Texture2D GetPreviewTexture(FileInfo info, byte[] bytes)
        {
            var key = info.FullName;
            var stamp = info.LastWriteTimeUtc.Ticks;
            if (PreviewCache.TryGetValue(key, out var cached) &&
                cached.Length == info.Length &&
                cached.LastWriteTicks == stamp &&
                cached.Texture != null)
            {
                return cached.Texture;
            }

            if (cached?.Texture != null)
            {
                UnityEngine.Object.Destroy(cached.Texture);
            }

            var texture = CreateCircularTextureFromImageBytes(bytes, true);
            if (texture == null)
            {
                PreviewCache.Remove(key);
                return null;
            }

            PreviewCache[key] = new PreviewCacheEntry(info.Length, stamp, texture);
            return texture;
        }

        private static void RemovePreviewCache(string path)
        {
            var key = NormalizeSourcePath(path);
            if (string.IsNullOrWhiteSpace(key)) return;

            if (!PreviewCache.TryGetValue(key, out var cached)) return;
            if (cached.Texture != null)
            {
                UnityEngine.Object.Destroy(cached.Texture);
            }

            PreviewCache.Remove(key);
        }

        private static void TrimPreviewCache(HashSet<string> visiblePaths)
        {
            if (PreviewCache.Count == 0) return;

            var staleKeys = new List<string>();
            foreach (var key in PreviewCache.Keys)
            {
                if (visiblePaths == null || !visiblePaths.Contains(key))
                {
                    staleKeys.Add(key);
                }
            }

            foreach (var key in staleKeys)
            {
                RemovePreviewCache(key);
            }
        }

        private static bool TryCreateLibraryItem(string path, bool loadPreviewTexture, out AvatarLibraryItem item)
        {
            item = null;

            try
            {
                var info = new FileInfo(path);
                if (!info.Exists || info.Length <= 0 || info.Length > MaxSourceBytes) return false;

                var bytes = File.ReadAllBytes(path);
                if (!IsSafeSourceImageBytes(bytes)) return false;

                Texture2D texture = null;
                if (loadPreviewTexture)
                {
                    texture = GetPreviewTexture(info, bytes);
                    if (texture == null) return false;
                }

                item = new AvatarLibraryItem(
                    info.FullName,
                    Path.GetFileNameWithoutExtension(info.Name),
                    info.FullName,
                    texture);
                return true;
            }
            catch (Exception ex)
            {
                ClientLogManager.Warning($"Skip avatar library item: {ex.Message}");
                return false;
            }
        }

        private sealed class PreviewCacheEntry
        {
            public PreviewCacheEntry(long length, long lastWriteTicks, Texture2D texture)
            {
                Length = length;
                LastWriteTicks = lastWriteTicks;
                Texture = texture;
            }

            public long Length { get; }
            public long LastWriteTicks { get; }
            public Texture2D Texture { get; }
        }

        private static void TryCreateDirectory(string path)
        {
            try
            {
                Directory.CreateDirectory(path);
            }
            catch (Exception ex)
            {
                ClientLogManager.Warning($"Create avatar library folder failed: {ex.Message}");
            }
        }

        private static Texture2D GetTexture(Sprite sprite)
        {
            if (sprite == null) return null;

            try
            {
                return sprite.texture;
            }
            catch
            {
                return null;
            }
        }
    }

    public sealed class AvatarLibraryItem
    {
        public const string DefaultId = "__default__";

        public AvatarLibraryItem(string id, string displayName, string path, Texture2D texture)
        {
            Id = id;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? "头像" : displayName;
            Path = path;
            Texture = texture;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public string Path { get; }
        public Texture2D Texture { get; }
        public bool IsDefault => Id == DefaultId;
    }
}
