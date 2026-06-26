using LocalizeLib;
using Il2CppAssets.Scripts.UI.Controls;
using MDEN.Managers;
using MDEN.Protocol.Messages.Lobby;
using MDEN.Protocol.Rules;
using MDEN.UI.Core;
using MelonLoader;
using PopupLib.UI.Components;
using PopupLib.UI.Windows;
using UnityEngine;

namespace MDEN.UI.Windows
{
    public class RoomPlaylistWindow : MDENWindowBase
    {
        private ForumWindow _window;
        private PlaylistEntryViewModel[] _items = new PlaylistEntryViewModel[0];
        private int _lastSelectedIndex = -1;
        private bool _closeQueued;
        private bool _suppressNextCompletion;
        private System.IDisposable _hudSuppression;

        public override void Show()
        {
            _hudSuppression = RoomHudController.SuppressForPopupWindow("playlist window");
            _window = new ForumWindow();
            _window.AutoReset = true;
            BuildList();
            LobbyManager.CurrentLobbyChanged += HandleCurrentLobbyChanged;
            _window.OnSelectionChanged += OnSelectionChanged;
            _window.OnInternalShow += OnInternalShowInjectTitle;
            _window.OnCompletion += OnWindowCompletion;
            _window.Show();

            RegisterEventCleanup(() =>
            {
                LobbyManager.CurrentLobbyChanged -= HandleCurrentLobbyChanged;

                if (_window != null)
                {
                    _window.OnSelectionChanged -= OnSelectionChanged;
                    _window.OnInternalShow -= OnInternalShowInjectTitle;
                    _window.OnCompletion -= OnWindowCompletion;
                }

                ReleaseHudSuppression();
            });
        }

        private void OnWindowCompletion(PopupLib.UI.Windows.Abstract.BaseWindow w)
        {
            if (_suppressNextCompletion)
            {
                _suppressNextCompletion = false;
                return;
            }

            WindowStackController.NotifyWindowCompleted(this);
        }

        private void HandleCurrentLobbyChanged(LobbySyncPush lobby)
        {
            if (lobby != null || _closeQueued) return;
            _closeQueued = true;

            MainThreadDispatcher.Enqueue(() =>
            {
                _closeQueued = false;
                if (IsDisposed) return;

                Close();
                WindowStackController.OpenWindow(new RoomListWindow());
            });
        }

        private void BuildList()
        {
            _window.ForumObjects.Clear();

            var lobby = LobbyManager.CurrentLobby;
            _items = PlaylistManager.GetPlaylistItems();
            if (lobby?.Playlist == null || _items.Length == 0)
            {
                AddButton(I18nManager.T("playlist.empty.title"), I18nManager.T("playlist.empty.desc"));
                return;
            }

            for (var i = 0; i < _items.Length; i++)
            {
                try
                {
                    var item = _items[i];
                    AddButton(GetItemTitle(item), GetItemDescription(item));
                }
                catch (System.Exception ex)
                {
                    MDEN.Managers.ClientLogManager.Warning($"Build playlist item failed: {ex.Message}");
                    AddButton(I18nManager.T("playlist.invalid.title"), I18nManager.T("playlist.invalid.desc"));
                }
            }
        }

        private ForumObject AddButton(string title, string desc)
        {
            var obj = new ForumObject(new LocalString(title), new LocalString(desc));
            obj.Texture = ResourceManager.GetSprite("RoomList.png")?.texture;
            _window.ForumObjects.Add(obj);
            return obj;
        }

        private static string FormatDifficulty(int difficulty)
        {
            return LobbyRuleTextFormatter.FormatDifficulty(difficulty, false);
        }

        private static string EscapeRichText(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            var safe = value
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Replace("\t", " ")
                .Replace("<", "＜")
                .Replace(">", "＞");
            return TruncateSafe(safe, 96);
        }

        private static string TruncateSafe(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength) return value ?? string.Empty;
            var length = maxLength;
            if (length > 0 && char.IsHighSurrogate(value[length - 1])) length--;
            return value.Substring(0, length);
        }

        private void OnSelectionChanged(PopupLib.UI.Windows.Interfaces.IListWindow window, int objectIndex)
        {
            if (_window == null || objectIndex < 0 || objectIndex >= _window.ForumObjects.Count) return;
            if (_lastSelectedIndex != objectIndex)
            {
                _lastSelectedIndex = objectIndex;
                return;
            }

            if (objectIndex >= _items.Length) return;

            var item = _items[objectIndex];
            if (item == null) return;
            if (!PlaylistManager.CanRemovePlaylistEntry(item))
            {
                ShowText.ShowInfo(PlaylistManager.GetPlaylistRemoveBlockedMessage(item));
                return;
            }

            NativeConfirmDialog.Show(I18nManager.T("playlist.delete.title"), I18nManager.Tf("playlist.delete.confirm", GetConfirmDisplayName(item)), confirmed =>
            {
                if (!confirmed) return;
                _ = RemoveItemAsync(item);
            });
        }

        private static string GetItemTitle(PlaylistEntryViewModel item)
        {
            return $"{EscapeRichText(item.DisplayName)} {FormatDifficulty(item.Difficulty)}";
        }

        private static string GetItemDescription(PlaylistEntryViewModel item)
        {
            if (IsCustomEntry(item))
            {
                return I18nManager.Tf(
                    "playlist.item.custom.desc",
                    EscapeRichText(item.DisplayName),
                    FormatDifficulty(item.Difficulty),
                    EscapeRichText(item.OwnerName),
                    FormatCustomChartId(item.ChartKey));
            }

            return I18nManager.Tf(
                "playlist.item.desc",
                EscapeRichText(item.DisplayName),
                FormatDifficulty(item.Difficulty),
                EscapeRichText(item.OwnerName));
        }

        private static string GetConfirmDisplayName(PlaylistEntryViewModel item)
        {
            return IsCustomEntry(item)
                ? $"{EscapeRichText(item.DisplayName)} ({FormatCustomChartId(item.ChartKey)})"
                : EscapeRichText(item.DisplayName);
        }

        private static bool IsCustomEntry(PlaylistEntryViewModel item)
        {
            return ChartSelectionRules.IsCustomChartKey(item?.ChartKey);
        }

        private static string FormatCustomChartId(string chartKey)
        {
            if (string.IsNullOrWhiteSpace(chartKey)) return I18nManager.T("common.unknown");
            return chartKey.Length <= 8 ? chartKey : chartKey.Substring(0, 8).ToUpperInvariant();
        }

        private async System.Threading.Tasks.Task RemoveItemAsync(PlaylistEntryViewModel item)
        {
            if (item == null || IsDisposed) return;

            IDisposable uiLock = WindowStackController.LockUI(I18nManager.T("common.removing"));
            try
            {
                await PlaylistManager.RemoveAsync(item.Entry);
                if (IsDisposed) return;

                await MainThreadDispatcher.InvokeAsync(() =>
                {
                    if (IsDisposed) return;
                    ShowText.ShowInfo(I18nManager.T("playlist.removed"));
                    RebuildWindow();
                });
            }
            catch (System.Exception ex)
            {
                MDEN.Managers.ClientLogManager.Warning($"Remove playlist entry failed: {ex.Message}");
                MainThreadDispatcher.Enqueue(() => ShowText.ShowInfo(ex.Message));
            }
            finally
            {
                await MainThreadDispatcher.InvokeAsync(() => uiLock?.Dispose());
            }
        }

        private void OnInternalShowInjectTitle(PopupLib.UI.Windows.Abstract.BaseWindow w)
        {
            var uiForward = GameObject.Find("UI/Forward");
            var pnlBulletin = uiForward?.transform.Find("Tips/PnlBulletinNew");
            var imgBase = pnlBulletin?.Find("ImgBase");
            var txtTitleObj = pnlBulletin?.Find("TxtTittle");
            if (imgBase == null || txtTitleObj == null) return;

            var oldTitle = imgBase.Find("MDENTitle");
            if (oldTitle != null) UnityEngine.Object.Destroy(oldTitle.gameObject);
            var oldTenziTitle = imgBase.Find("MDENTenziDrawTitle");
            if (oldTenziTitle != null) UnityEngine.Object.Destroy(oldTenziTitle.gameObject);

            var newTitle = GameObject.Instantiate(txtTitleObj.gameObject, imgBase);
            newTitle.name = "MDENTitle";
            newTitle.SetActive(true);

            var loc = newTitle.GetComponent<Il2CppAssets.Scripts.PeroTools.GeneralLocalization.Localization>();
            if (loc != null) UnityEngine.Object.Destroy(loc);

            var text = newTitle.GetComponent<UnityEngine.UI.Text>();
            if (text != null)
            {
                text.text = GetTitleText();
                text.alignment = TextAnchor.MiddleCenter;
            }
        }

        public override void Close()
        {
            _lastSelectedIndex = -1;
            if (_window != null)
            {
                _window.OnSelectionChanged -= OnSelectionChanged;
                _window.OnInternalShow -= OnInternalShowInjectTitle;
                _window.OnCompletion -= OnWindowCompletion;
                _suppressNextCompletion = true;
                _window.ForceClose();
                _window = null;
            }

            ReleaseHudSuppression();
            RemoveInjectedTitle();
        }

        private string GetTitleText()
        {
            var lobby = LobbyManager.CurrentLobby;
            var currentCount = lobby?.Playlist?.Length ?? _items?.Length ?? 0;
            var maxCount = lobby?.PlaylistSize ?? 0;
            return I18nManager.Tf("playlist.title", currentCount, maxCount);
        }

        private static void RemoveInjectedTitle()
        {
            var panel = GameObject.Find("UI/Forward/Tips/PnlBulletinNew");
            var imgBase = panel?.transform.Find("ImgBase");
            if (imgBase == null) return;

            var title = imgBase.Find("MDENTitle");
            if (title != null) UnityEngine.Object.Destroy(title.gameObject);
        }

        private void RebuildWindow()
        {
            if (_window == null) return;
            _window.OnSelectionChanged -= OnSelectionChanged;
            _window.OnInternalShow -= OnInternalShowInjectTitle;
            _window.OnCompletion -= OnWindowCompletion;
            _suppressNextCompletion = true;
            _window.ForceClose();
            _suppressNextCompletion = false;
            _window = new ForumWindow();
            _window.AutoReset = true;
            BuildList();
            _window.OnSelectionChanged += OnSelectionChanged;
            _window.OnInternalShow += OnInternalShowInjectTitle;
            _window.OnCompletion += OnWindowCompletion;
            _window.Show();
            _lastSelectedIndex = -1;
        }

        private void ReleaseHudSuppression()
        {
            var suppression = _hudSuppression;
            if (suppression == null) return;

            _hudSuppression = null;
            suppression.Dispose();
        }
    }
}
