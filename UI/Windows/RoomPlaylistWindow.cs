using Il2CppAssets.Scripts.UI.Controls;
using MDEN.Managers;
using MDEN.Protocol.Messages.Lobby;
using MDEN.Protocol.Rules;
using MDEN.UI.Core;
using MelonLoader;
using UnityEngine;

namespace MDEN.UI.Windows
{
    public class RoomPlaylistWindow : MDENWindowBase
    {
        private NativeListWindow _window;
        private PlaylistEntryViewModel[] _items = new PlaylistEntryViewModel[0];
        private int _lastSelectedIndex = -1;
        private bool _closeQueued;
        private bool _suppressNextCompletion;
        private System.IDisposable _hudSuppression;

        public override void Show()
        {
            _hudSuppression = RoomHudController.SuppressForPopupWindow("playlist window");
            _window = new NativeListWindow();
            _window.AutoReset = true;
            BuildList();
            LobbyManager.CurrentLobbyChanged += HandleCurrentLobbyChanged;
            _window.OnSelectionChanged += OnSelectionChanged;
            _window.OnCompletion += OnWindowCompletion;
            _window.Title = GetTitleText();
            _window.Show();

            RegisterEventCleanup(() =>
            {
                LobbyManager.CurrentLobbyChanged -= HandleCurrentLobbyChanged;

                if (_window != null)
                {
                    _window.OnSelectionChanged -= OnSelectionChanged;
                    _window.OnCompletion -= OnWindowCompletion;
                }

                ReleaseHudSuppression();
            });
        }

        private void OnWindowCompletion(INativeBaseWindow w)
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
            _window.Items.Clear();

            var lobby = LobbyManager.CurrentLobby;
            _items = PlaylistManager.GetPlaylistItems();
            if (lobby?.Playlist == null || _items.Length == 0)
            {
                AddButton("暂无歌曲", "在选歌界面点击 Start 可加入歌曲列表");
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
                    AddButton("无法显示的谱面", "该歌曲列表项格式异常");
                }
            }
        }

        private NativeListItem AddButton(string title, string desc)
        {
            var obj = new NativeListItem(title, desc);
            obj.Texture = ResourceManager.GetSprite("RoomList.png")?.texture;
            _window.Items.Add(obj);
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

        private void OnSelectionChanged(INativeListWindow window, int objectIndex)
        {
            if (_window == null || objectIndex < 0 || objectIndex >= _window.Items.Count) return;
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

            NativeConfirmDialog.Show("删除歌曲", $"确认从歌曲列表移除「{GetConfirmDisplayName(item)}」吗？", confirmed =>
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
                return $"谱面: {EscapeRichText(item.DisplayName)}\n类型: 自制谱\n难度: {FormatDifficulty(item.Difficulty)}\n添加者: {EscapeRichText(item.OwnerName)}\nID: {FormatCustomChartId(item.ChartKey)}";
            }

            return $"谱面: {EscapeRichText(item.DisplayName)}\n难度: {FormatDifficulty(item.Difficulty)}\n添加者: {EscapeRichText(item.OwnerName)}";
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
            if (string.IsNullOrWhiteSpace(chartKey)) return "Unknown";
            return chartKey.Length <= 8 ? chartKey : chartKey.Substring(0, 8).ToUpperInvariant();
        }

        private async System.Threading.Tasks.Task RemoveItemAsync(PlaylistEntryViewModel item)
        {
            if (item == null || IsDisposed) return;

            IDisposable uiLock = WindowStackController.LockUI("Removing playlist entry...");
            try
            {
                await PlaylistManager.RemoveAsync(item.Entry);
                if (IsDisposed) return;

                await MainThreadDispatcher.InvokeAsync(() =>
                {
                    if (IsDisposed) return;
                    ShowText.ShowInfo("成功移除歌曲列表");
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

        public override void Close()
        {
            _lastSelectedIndex = -1;
            if (_window != null)
            {
                _window.OnSelectionChanged -= OnSelectionChanged;
                _window.OnCompletion -= OnWindowCompletion;
                _suppressNextCompletion = true;
                _window.ForceClose();
                _window = null;
            }

            ReleaseHudSuppression();
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
            _window.OnCompletion -= OnWindowCompletion;
            _suppressNextCompletion = true;
            _window.ForceClose();
            _suppressNextCompletion = false;
            _window = new NativeListWindow();
            _window.AutoReset = true;
            BuildList();
            _window.Title = GetTitleText();
            _window.OnSelectionChanged += OnSelectionChanged;
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
