using System;
using System.Collections;
using System.Collections.Generic;
using Il2CppAssets.Scripts.UI.Panels.Bulletin;
using LocalizeLib;
using MDEN.Managers;
using MDEN.Protocol.Messages.Lobby;
using MDEN.UI.Core;
using MelonLoader;
using PopupLib.UI.Components;
using PopupLib.UI.Windows;
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
        private readonly List<ForumObject> _objects = new List<ForumObject>();
        private ForumWindow _window;
        private bool _nativeRefreshFailed;
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

            _window = new ForumWindow();
            _window.AutoReset = true;
            BuildList();
            _window.OnInternalShow += OnInternalShowInjectTitle;
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

            RemoveInjectedTitle();
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
                RefreshNativeWindowItems();
                yield return new WaitForSecondsRealtime(GetStepDelay(step, totalSteps));
            }

            RefreshItems(_selectedIndex, true);
            RefreshNativeWindowItems();
            for (var frame = 0; frame < HoldFrames; frame++)
            {
                if (generation != _generation || _window == null) yield break;
                yield return new WaitForEndOfFrame();
            }

            Close();
            TenziDrawController.NotifyDrawCompleted(this);
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
            _window.ForumObjects.Clear();
            _objects.Clear();
            for (var i = 0; i < _items.Length; i++)
            {
                var obj = new ForumObject(
                    new LocalString(BuildTitle(i, -1, false)),
                    new LocalString(BuildDescription(i, -1, false)));
                obj.Texture = ResourceManager.GetRandomBannerTexture() ??
                              ResourceManager.GetSprite("RoomList.png")?.texture;
                _window.ForumObjects.Add(obj);
                _objects.Add(obj);
            }
        }

        private void RefreshItems(int activeIndex, bool finished)
        {
            for (var i = 0; i < _objects.Count; i++)
            {
                var obj = _objects[i];
                if (obj == null) continue;

                obj.Titles = new LocalString(BuildTitle(i, activeIndex, finished));
                obj.Contents = new LocalString(BuildDescription(i, activeIndex, finished));
            }
        }

        private void RefreshNativeWindowItems()
        {
            if (_nativeRefreshFailed || _window == null || !_window.Activated) return;

            try
            {
                var panel = GameObject.Find("UI/Forward/Tips/PnlBulletinNew");
                var controller = panel?.GetComponent<PnlStageBulletinController>();
                if (controller == null) return;

                controller.m_BulletinDataModels = BuildBulletinModels();
                controller.m_BulletinView.languageChangDirty = true;
                controller.RefreshUI();
            }
            catch (Exception ex)
            {
                _nativeRefreshFailed = true;
                ClientLogManager.Warning($"Tenzi draw window refresh failed: {ex.Message}");
            }
        }

        private Il2CppSystem.Collections.Generic.Dictionary<string, Il2CppSystem.Collections.Generic.List<PnlStageBulletinDataModel>> BuildBulletinModels()
        {
            var models = new Il2CppSystem.Collections.Generic.Dictionary<string, Il2CppSystem.Collections.Generic.List<PnlStageBulletinDataModel>>();
            for (var i = 0; i < _objects.Count; i++)
            {
                var obj = _objects[i];
                if (obj == null) continue;

                foreach (var content in LocalString.GetContents(obj.Titles, obj.Contents))
                {
                    var language = content[0] ?? string.Empty;
                    if (!models.ContainsKey(language))
                    {
                        models[language] = new Il2CppSystem.Collections.Generic.List<PnlStageBulletinDataModel>();
                    }

                    models[language].Add(new PnlStageBulletinDataModel
                    {
                        title = content[1] ?? string.Empty,
                        content = content[2] ?? string.Empty,
                        imageUrl = obj.TextureURL ?? $"PopupLib://{i}",
                        uid = i.ToString(),
                        force = true,
                        isNew = obj.IsNew
                    });
                }
            }

            return models;
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
            var state = I18nManager.T("tenzi.state.waiting");
            if (finished && index == _selectedIndex)
            {
                state = $"<color={Constants.ColorGreen}>{I18nManager.T("tenzi.state.selected")}</color>";
            }
            else if (!finished && index == activeIndex)
            {
                state = $"<color={Constants.ColorYellow}>{I18nManager.T("tenzi.state.drawing")}</color>";
            }

            return I18nManager.Tf(
                "tenzi.desc",
                state,
                EscapeRichText(item.Name),
                LobbyRuleTextFormatter.FormatDifficulty(item.Difficulty, false),
                EscapeRichText(item.OwnerName));
        }

        private void OnWindowCompletion(PopupLib.UI.Windows.Abstract.BaseWindow w)
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

        private void OnInternalShowInjectTitle(PopupLib.UI.Windows.Abstract.BaseWindow w)
        {
            var uiForward = GameObject.Find("UI/Forward");
            var pnlBulletin = uiForward?.transform.Find("Tips/PnlBulletinNew");
            var imgBase = pnlBulletin?.Find("ImgBase");
            var txtTitleObj = pnlBulletin?.Find("TxtTittle");
            if (imgBase == null || txtTitleObj == null) return;

            RemoveInjectedTitle(imgBase);

            var newTitle = GameObject.Instantiate(txtTitleObj.gameObject, imgBase);
            newTitle.name = "MDENTenziDrawTitle";
            newTitle.SetActive(true);

            var loc = newTitle.GetComponent<Il2CppAssets.Scripts.PeroTools.GeneralLocalization.Localization>();
            if (loc != null) UnityEngine.Object.Destroy(loc);

            var text = newTitle.GetComponent<UnityEngine.UI.Text>();
            if (text != null)
            {
                text.text = I18nManager.T("tenzi.title");
                text.alignment = TextAnchor.MiddleCenter;
            }
        }

        private static void RemoveInjectedTitle()
        {
            var panel = GameObject.Find("UI/Forward/Tips/PnlBulletinNew");
            var imgBase = panel?.transform.Find("ImgBase");
            if (imgBase == null) return;

            RemoveInjectedTitle(imgBase);
        }

        private static void RemoveInjectedTitle(Transform imgBase)
        {
            RemoveTitle(imgBase, "MDENTenziDrawTitle");
            RemoveTitle(imgBase, "MDENTitle");
            var scrollView = imgBase.Find("ScrollView");
            if (scrollView == null) return;

            RemoveTitle(scrollView, "MDENTenziDrawTitle");
            RemoveTitle(scrollView, "MDENTitle");
        }

        private static void RemoveTitle(Transform parent, string name)
        {
            var title = parent?.Find(name);
            if (title != null) UnityEngine.Object.Destroy(title.gameObject);
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

            _window.OnInternalShow -= OnInternalShowInjectTitle;
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
