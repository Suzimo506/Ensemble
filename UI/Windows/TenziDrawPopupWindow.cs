using System;
using System.Collections;
using System.Collections.Generic;
using MDEN.Managers;
using MDEN.Protocol.Messages.Lobby;
using MDEN.UI.Core;
using MelonLoader;
using UnityEngine;

namespace MDEN.UI.Windows
{
    internal sealed class TenziDrawPopupWindow : MDENWindowBase
    {
        private const int BaseDrawSteps = 24;
        private const int HoldFrames = 90;
        private readonly TenziDrawItem[] _items;
        private readonly int _selectedIndex;
        private readonly long _drawSeed;
        private readonly List<NativeListItem> _objects = new List<NativeListItem>();
        private NativeListWindow _window;
        private bool _suppressNextCompletion;
        private int _generation;

        public TenziDrawPopupWindow(LobbySyncPush lobby)
        {
            _items = BuildItems(lobby);
            _selectedIndex = Math.Max(0, Array.FindIndex(_items, item => item.Entry == lobby?.TenziSelectedEntry));
            _drawSeed = lobby?.TenziDrawSeed ?? 0;
        }

        public override void Show()
        {
            if (_items.Length == 0)
            {
                TenziDrawController.NotifyWindowClosed(this);
                Dispose();
                return;
            }

            _window = new NativeListWindow();
            _window.AutoReset = true;
            BuildList();
            _window.Title = "天子抽曲";
            _window.OnCompletion += OnWindowCompletion;
            _window.Show();

            var generation = ++_generation;
            MelonCoroutines.Start(RunDraw(generation));

            RegisterEventCleanup(() =>
            {
                DetachWindowEvents();
            });
        }

        public override void Close()
        {
            _generation++;
            if (_window != null)
            {
                DetachWindowEvents();
                _suppressNextCompletion = true;
                _window.ForceClose();
                _window = null;
            }
        }

        private IEnumerator RunDraw(int generation)
        {
            var count = _items.Length;
            var startIndex = count == 0 ? 0 : Math.Abs((int)(_drawSeed % count));
            var offset = (_selectedIndex - startIndex + count) % count;
            var rounds = BaseDrawSteps / count + 3;
            var totalSteps = count * rounds + offset;

            for (var step = 0; step <= totalSteps; step++)
            {
                if (generation != _generation || _window == null) yield break;

                var activeIndex = (startIndex + step) % count;
                var finished = step == totalSteps;
                RefreshItems(activeIndex, finished);
                _window.RefreshItems();
                yield return new WaitForSecondsRealtime(GetStepDelay(step, totalSteps));
            }

            RefreshItems(_selectedIndex, true);
            _window.RefreshItems();
            for (var frame = 0; frame < HoldFrames; frame++)
            {
                if (generation != _generation || _window == null) yield break;
                yield return new WaitForEndOfFrame();
            }

            Close();
            TenziDrawController.NotifyWindowClosed(this);
            Dispose();
        }

        private static float GetStepDelay(int step, int totalSteps)
        {
            if (totalSteps <= 0) return 0.06f;

            var progress = step / (float)totalSteps;
            return 0.035f + progress * progress * 0.115f;
        }

        private void BuildList()
        {
            _window.Items.Clear();
            _objects.Clear();
            for (var i = 0; i < _items.Length; i++)
            {
                var obj = new NativeListItem(
                    BuildTitle(i, -1, false),
                    BuildDescription(i, -1, false));
                obj.Texture = ResourceManager.GetSprite("RoomList.png")?.texture ??
                              ResourceManager.GetRandomBannerTexture();
                _window.Items.Add(obj);
                _objects.Add(obj);
            }
        }

        private void RefreshItems(int activeIndex, bool finished)
        {
            for (var i = 0; i < _objects.Count; i++)
            {
                var obj = _objects[i];
                if (obj == null) continue;

                obj.Title = BuildTitle(i, activeIndex, finished);
                obj.Content = BuildDescription(i, activeIndex, finished);
            }
        }

        private string BuildTitle(int index, int activeIndex, bool finished)
        {
            var item = _items[index];
            var name = EscapeRichText(item.Name);
            if (finished && index == _selectedIndex)
            {
                return $"<color={Constants.ColorGreen}>★ {name}</color>";
            }

            if (!finished && index == activeIndex)
            {
                return $"<color={Constants.ColorYellow}>▶ {name}</color>";
            }

            return name;
        }

        private string BuildDescription(int index, int activeIndex, bool finished)
        {
            var item = _items[index];
            var state = "等待抽取";
            if (finished && index == _selectedIndex)
            {
                state = $"<color={Constants.ColorGreen}>已抽中</color>";
            }
            else if (!finished && index == activeIndex)
            {
                state = $"<color={Constants.ColorYellow}>抽取中</color>";
            }

            return $"状态: {state}\n谱面: {EscapeRichText(item.Name)}\n难度: {LobbyRuleTextFormatter.FormatDifficulty(item.Difficulty, false)}\n添加者: {EscapeRichText(item.OwnerName)}";
        }

        private void OnWindowCompletion(INativeBaseWindow w)
        {
            if (_suppressNextCompletion)
            {
                _suppressNextCompletion = false;
                return;
            }

            _generation++;
            DetachWindowEvents();
            _window = null;
            TenziDrawController.NotifyWindowClosed(this);
            Dispose();
        }

        private static TenziDrawItem[] BuildItems(LobbySyncPush lobby)
        {
            if (lobby?.Playlist == null || lobby.Playlist.Length == 0) return Array.Empty<TenziDrawItem>();

            var result = new List<TenziDrawItem>();
            for (var i = 0; i < lobby.Playlist.Length; i++)
            {
                var entryText = lobby.Playlist[i];
                var parsed = ChartManager.ParseEntry(entryText);
                if (parsed == null) continue;

                result.Add(new TenziDrawItem
                {
                    Entry = entryText,
                    Name = parsed.DisplayName,
                    Difficulty = parsed.Difficulty,
                    OwnerName = parsed.OwnerName
                });
            }

            return result.ToArray();
        }

        private static string EscapeRichText(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            return value
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Replace("\t", " ")
                .Replace("<", "＜")
                .Replace(">", "＞");
        }

        private void DetachWindowEvents()
        {
            if (_window == null) return;

            _window.OnCompletion -= OnWindowCompletion;
        }

        private sealed class TenziDrawItem
        {
            public string Entry { get; set; }
            public string Name { get; set; }
            public int Difficulty { get; set; }
            public string OwnerName { get; set; }
        }
    }
}
