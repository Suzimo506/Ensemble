using LocalizeLib;
using Il2CppAssets.Scripts.UI.Controls;
using MDEN.Managers;
using MDEN.Protocol.Messages.Lobby;
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
        private readonly object _refreshLock = new object();
        private bool _refreshQueued;
        private int _lastLobbyId = -1;
        private string _lastPlaylistSnapshot = string.Empty;

        public override void Show()
        {
            _window = new ForumWindow();
            _window.AutoReset = true;
            BuildList();
            CapturePlaylistSnapshot(LobbyManager.CurrentLobby);
            LobbyManager.CurrentLobbyChanged += HandleCurrentLobbyChanged;
            _window.OnSelectionChanged += OnSelectionChanged;
            _window.OnInternalShow += OnInternalShowInjectTitle;
            _window.Show();

            RegisterEventCleanup(() =>
            {
                LobbyManager.CurrentLobbyChanged -= HandleCurrentLobbyChanged;

                if (_window != null)
                {
                    _window.OnSelectionChanged -= OnSelectionChanged;
                    _window.OnInternalShow -= OnInternalShowInjectTitle;
                }
            });
        }

        private void HandleCurrentLobbyChanged(LobbySyncPush lobby)
        {
            lock (_refreshLock)
            {
                if (!HasPlaylistStateChanged(lobby) || _refreshQueued) return;
                _refreshQueued = true;
            }

            MainThreadDispatcher.Enqueue(() =>
            {
                lock (_refreshLock)
                {
                    _refreshQueued = false;
                }

                if (IsDisposed) return;

                if (lobby == null)
                {
                    Close();
                    WindowStackController.OpenWindow(new RoomListWindow());
                    return;
                }

                CapturePlaylistSnapshot(lobby);
                RebuildWindow();
            });
        }

        private void BuildList()
        {
            _window.ForumObjects.Clear();

            var lobby = LobbyManager.CurrentLobby;
            _items = PlaylistManager.GetPlaylistItems();
            if (lobby?.Playlist == null || _items.Length == 0)
            {
                AddButton("暂无歌曲", "在选歌界面点击 Start 可加入歌曲列表");
                return;
            }

            for (var i = 0; i < _items.Length; i++)
            {
                var item = _items[i];
                var title = $"{EscapeRichText(item.DisplayName)} {FormatDifficulty(item.Difficulty)}";
                var desc = $"谱面: {EscapeRichText(item.DisplayName)}\n难度: {FormatDifficulty(item.Difficulty)}\n添加者: {EscapeRichText(item.OwnerName)}";
                AddButton(title, desc);
            }
        }

        private bool HasPlaylistStateChanged(LobbySyncPush lobby)
        {
            var lobbyId = lobby?.Id ?? -1;
            var snapshot = BuildPlaylistSnapshot(lobby);
            return lobbyId != _lastLobbyId || snapshot != _lastPlaylistSnapshot;
        }

        private void CapturePlaylistSnapshot(LobbySyncPush lobby)
        {
            _lastLobbyId = lobby?.Id ?? -1;
            _lastPlaylistSnapshot = BuildPlaylistSnapshot(lobby);
        }

        private static string BuildPlaylistSnapshot(LobbySyncPush lobby)
        {
            if (lobby == null) return string.Empty;

            var playlist = lobby.Playlist == null || lobby.Playlist.Length == 0
                ? string.Empty
                : string.Join("\u001F", lobby.Playlist);

            return $"{lobby.PlaylistSize}|{lobby.Locked}|{lobby.IsPlaying}|{playlist}";
        }

        private ForumObject AddButton(string title, string desc)
        {
            var obj = new ForumObject(new LocalString(title), new LocalString(desc));
            obj.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("RoomList.png")?.texture;
            _window.ForumObjects.Add(obj);
            return obj;
        }

        private static string FormatDifficulty(int difficulty)
        {
            return difficulty switch
            {
                1 => "<color=00d45aff>萌新</color>",
                2 => $"<color={Constants.ColorBlue}>高手</color>",
                3 => "<color=9b55ffff>大触</color>",
                4 => "<color=ff5555ff>隐藏</color>",
                _ => difficulty.ToString()
            };
        }

        private static string EscapeRichText(string value)
        {
            return value?.Replace("<", "＜").Replace(">", "＞") ?? string.Empty;
        }

        private void OnSelectionChanged(PopupLib.UI.Windows.Interfaces.IListWindow window, int objectIndex)
        {
            if (_window == null || objectIndex < 0 || objectIndex >= _window.ForumObjects.Count) return;
            if (_lastSelectedIndex != objectIndex)
            {
                _lastSelectedIndex = objectIndex;
                return;
            }

            if (!PlaylistManager.CanChangePlaylist || objectIndex >= _items.Length) return;

            var item = _items[objectIndex];
            NativeConfirmDialog.Show("删除歌曲", $"确认从歌曲列表移除「{item.DisplayName}」吗？", confirmed =>
            {
                if (!confirmed) return;
                _ = RemoveItemAsync(item);
            });
        }

        private async System.Threading.Tasks.Task RemoveItemAsync(PlaylistEntryViewModel item)
        {
            if (item == null || IsDisposed) return;

            using var _ = WindowStackController.LockUI("Removing playlist entry...");
            try
            {
                await PlaylistManager.RemoveAsync(item.Entry);
                if (IsDisposed) return;

                MainThreadDispatcher.Enqueue(() =>
                {
                    if (IsDisposed) return;
                    ShowText.ShowInfo("成功移除歌曲列表");
                    RebuildWindow();
                });
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"Remove playlist entry failed: {ex.Message}");
                MainThreadDispatcher.Enqueue(() => ShowText.ShowInfo(ex.Message));
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
                _window.ForceClose();
                _window = null;
            }
        }

        private string GetTitleText()
        {
            var lobby = LobbyManager.CurrentLobby;
            var currentCount = lobby?.Playlist?.Length ?? _items?.Length ?? 0;
            var maxCount = lobby?.PlaylistSize ?? 0;
            return $"歌曲列表 {currentCount}/{maxCount}";
        }
        private void RebuildWindow()
        {
            if (_window == null) return;
            _window.OnSelectionChanged -= OnSelectionChanged;
            _window.OnInternalShow -= OnInternalShowInjectTitle;
            _window.ForceClose();
            _window = new ForumWindow();
            _window.AutoReset = true;
            BuildList();
            _window.OnSelectionChanged += OnSelectionChanged;
            _window.OnInternalShow += OnInternalShowInjectTitle;
            _window.Show();
            _lastSelectedIndex = -1;
        }
    }
}
