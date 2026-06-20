using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Il2CppAssets.Scripts.Database;
using Il2CppPeroPeroGames.GlobalDefines;
using MDEN.Managers;
using UnityEngine;

namespace MDEN.UI.Core
{
    internal enum UiSound
    {
        Yes
    }

    internal static class UiSoundManager
    {
        private const float SampleGain = 2.2f;
        private static readonly Dictionary<UiSound, AudioClip> Clips = new();
        private static readonly HashSet<UiSound> MissingLogged = new();
        private static AudioSource _source;

        public static void Play(UiSound sound, float volumeScale = 1f)
        {
            var clip = GetClip(sound);
            if (clip == null) return;

            var source = GetSource();
            if (source == null) return;

            source.PlayOneShot(clip, volumeScale * GetGameSfxVolume());
        }

        private static AudioClip GetClip(UiSound sound)
        {
            if (Clips.TryGetValue(sound, out var clip)) return clip;

            clip = LoadClip(sound);
            if (clip != null)
            {
                Clips[sound] = clip;
            }
            else if (MissingLogged.Add(sound))
            {
                ClientLogManager.Warning($"Missing UI sound: {sound}");
            }

            return clip;
        }

        private static AudioClip LoadClip(UiSound sound)
        {
            var baseName = sound.ToString();
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = assembly
                .GetManifestResourceNames()
                .FirstOrDefault(name => name.EndsWith(baseName + ".wav", StringComparison.OrdinalIgnoreCase));
            if (resourceName == null) return null;

            try
            {
                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    return stream == null ? null : LoadWav(stream, baseName);
                }
            }
            catch (Exception ex)
            {
                ClientLogManager.Warning($"Failed to load UI sound {baseName}: {ex.Message}");
                return null;
            }
        }

        private static AudioClip LoadWav(Stream stream, string name)
        {
            using (var reader = new BinaryReader(stream))
            {
                if (new string(reader.ReadChars(4)) != "RIFF") throw new InvalidDataException("Invalid WAV RIFF header.");
                reader.ReadInt32();
                if (new string(reader.ReadChars(4)) != "WAVE") throw new InvalidDataException("Invalid WAV WAVE header.");

                short channels = 0;
                var sampleRate = 0;
                short bitsPerSample = 0;
                byte[] data = null;

                while (stream.Position < stream.Length)
                {
                    var chunkId = new string(reader.ReadChars(4));
                    var chunkSize = reader.ReadInt32();
                    var nextChunk = stream.Position + chunkSize + chunkSize % 2;

                    if (chunkId == "fmt ")
                    {
                        var format = reader.ReadInt16();
                        channels = reader.ReadInt16();
                        sampleRate = reader.ReadInt32();
                        reader.ReadInt32();
                        reader.ReadInt16();
                        bitsPerSample = reader.ReadInt16();
                        if (format != 1) throw new NotSupportedException("Only PCM WAV is supported.");
                    }
                    else if (chunkId == "data")
                    {
                        data = reader.ReadBytes(chunkSize);
                    }

                    stream.Position = Math.Min(nextChunk, stream.Length);
                }

                if (data == null || channels <= 0 || sampleRate <= 0 || bitsPerSample != 16)
                {
                    throw new InvalidDataException("Unsupported WAV data.");
                }

                var samples = new float[data.Length / 2];
                for (var i = 0; i < samples.Length; i++)
                {
                    var value = BitConverter.ToInt16(data, i * 2) / 32768f;
                    samples[i] = Mathf.Clamp(value * SampleGain, -1f, 1f);
                }

                var clip = AudioClip.Create("MDEN_" + name, samples.Length / channels, channels, sampleRate, false);
                clip.SetData(samples, 0);
                return clip;
            }
        }

        private static AudioSource GetSource()
        {
            if (_source != null) return _source;

            var obj = new GameObject("MDENUiAudio");
            UnityEngine.Object.DontDestroyOnLoad(obj);
            _source = obj.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = false;
            _source.spatialBlend = 0f;
            _source.volume = 1f;
            return _source;
        }

        private static float GetGameSfxVolume()
        {
            try
            {
                return Mathf.Clamp01(DataHelper.GetVolume(PeroAudioType.Sfx));
            }
            catch (Exception ex)
            {
                ClientLogManager.Warning($"Failed to read game SFX volume: {ex.Message}");
                return 1f;
            }
        }
    }
}
