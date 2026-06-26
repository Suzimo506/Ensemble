using System;
using System.Threading.Tasks;
using Il2CppAssets.Scripts.UI.Controls;
using LocalizeLib;
using MDEN.Managers;
using MDEN.Protocol.Enums;
using MDEN.UI.Core;
using MelonLoader;
using PopupLib.UI.Components;
using PopupLib.UI.Windows;
using UnityEngine;

namespace MDEN.UI.Windows
{
    public class CreateRoomWindow : MDENWindowBase
    {
        private ForumWindow _window;
        private ForumObject _btnBack;
        private ForumObject _btnName;
        private ForumObject _btnMaxPlayers;
        private ForumObject _btnPlayMode;
        private ForumObject _btnPlaylistSize;
        private ForumObject _btnGoal;
        private ForumObject _btnSettlement;
        private ForumObject _btnPassword;
        private ForumObject _btnCreate;
        private int _lastSelectedIndex = -1;
        private bool _createInProgress;

        private string _roomName = I18nManager.T("create.default_room_name");
        private ushort _maxPlayers = 4;
        private LobbyPlayMode _playMode = LobbyPlayMode.Normal;
        private ushort _playlistSize = 12;
        private LobbyGoal _goal = LobbyGoal.Accuracy;
        private bool _settlementEnabled;
        private string _password;

        public override void Show()
        {
            _window = new ForumWindow();
            _window.AutoReset = true;
            BuildList();
            _window.OnSelectionChanged += OnSelectionChanged;
            _window.OnInternalShow += OnInternalShowInjectTitle;
            _window.Show();
            _lastSelectedIndex = -1;

            RegisterEventCleanup(() =>
            {
                if (_window != null)
                {
                    _window.OnSelectionChanged -= OnSelectionChanged;
                    _window.OnInternalShow -= OnInternalShowInjectTitle;
                }

                RemoveInjectedTitle();
            });
        }

        private void BuildList()
        {
            _window.ForumObjects.Clear();
            _lastSelectedIndex = -1;

            _btnBack = new ForumObject(new LocalString(I18nManager.T("common.back.button")), new LocalString(I18nManager.T("common.back.room_list")));
            _btnBack.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("OptionsPanel.png")?.texture;
            _window.ForumObjects.Add(_btnBack);

            _btnName = new ForumObject(new LocalString(I18nManager.T("create.name.title")), new LocalString(I18nManager.Tf("create.name.desc", HighlightValue(EscapeRichText(_roomName)))));
            _btnName.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("HomePanel.png")?.texture;
            _window.ForumObjects.Add(_btnName);

            _btnMaxPlayers = new ForumObject(new LocalString(I18nManager.T("create.players.title")), new LocalString(I18nManager.Tf("create.players.desc", HighlightValue(_maxPlayers))));
            _btnMaxPlayers.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("PlayerCard.png")?.texture;
            _window.ForumObjects.Add(_btnMaxPlayers);

            _btnPlayMode = new ForumObject(
                new LocalString(I18nManager.T("create.play_mode.title")),
                new LocalString(I18nManager.Tf("create.play_mode.desc", FormatPlayMode(_playMode), FormatPlayMode(LobbyRuleTextFormatter.GetNextPlayMode((byte)_playMode)))));
            _btnPlayMode.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("OptionsPanel.png")?.texture;
            _window.ForumObjects.Add(_btnPlayMode);

            _btnPlaylistSize = new ForumObject(new LocalString(I18nManager.T("create.playlist_size.title")), new LocalString(I18nManager.Tf("create.playlist_size.desc", HighlightValue(_playlistSize))));
            _btnPlaylistSize.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("RoomList.png")?.texture;
            _window.ForumObjects.Add(_btnPlaylistSize);

            _btnGoal = new ForumObject(new LocalString(I18nManager.T("create.goal.title")), new LocalString(I18nManager.Tf("create.goal.desc", HighlightValue(GetGoalName()))));
            _btnGoal.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("SocialNetwork.png")?.texture;
            _window.ForumObjects.Add(_btnGoal);

            _btnSettlement = new ForumObject(new LocalString(I18nManager.T("create.settlement.title")), new LocalString(I18nManager.Tf("create.settlement.desc", HighlightValue(GetSettlementNamePlain()))));
            _btnSettlement.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("OptionsPanel.png")?.texture;
            _window.ForumObjects.Add(_btnSettlement);

            _btnPassword = new ForumObject(new LocalString(I18nManager.T("create.password.title")), new LocalString(I18nManager.Tf("create.password.desc", HighlightValue(string.IsNullOrWhiteSpace(_password) ? I18nManager.T("common.no") : I18nManager.T("common.set")))));
            _btnPassword.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("SocialNetwork.png")?.texture;
            _window.ForumObjects.Add(_btnPassword);

            var createTitle = _createInProgress
                ? $"<color={Constants.ColorYellow}>{I18nManager.T("create.creating.button")}</color>"
                : $"<color={Constants.ColorYellow}>{I18nManager.T("create.confirm.button")}</color>";
            var createDescription = _createInProgress
                ? I18nManager.Tf("create.creating.desc", HighlightValue(EscapeRichText(_roomName)))
                : BuildSummary();
            _btnCreate = new ForumObject(new LocalString(createTitle), new LocalString(createDescription));
            _btnCreate.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("RoomList.png")?.texture;
            _window.ForumObjects.Add(_btnCreate);
        }

        private async void OnSelectionChanged(PopupLib.UI.Windows.Interfaces.IListWindow window, int objectIndex)
        {
            if (_window == null || objectIndex < 0 || objectIndex >= _window.ForumObjects.Count) return;

            if (_createInProgress)
            {
                ShowText.ShowInfo(I18nManager.T("create.in_progress"));
                return;
            }

            if (_lastSelectedIndex != objectIndex)
            {
                _lastSelectedIndex = objectIndex;
                return;
            }

            var button = _window.ForumObjects[objectIndex];
            if (button == _btnBack)
            {
                Close();
                WindowStackController.OpenWindow(new RoomListWindow());
            }
            else if (button == _btnName)
            {
                ShowNameInput();
            }
            else if (button == _btnMaxPlayers)
            {
                ShowMaxPlayersInput();
            }
            else if (button == _btnPlayMode)
            {
                _playMode = LobbyRuleTextFormatter.GetNextPlayMode((byte)_playMode);
                RebuildWindow();
            }
            else if (button == _btnPlaylistSize)
            {
                ShowPlaylistSizeInput();
            }
            else if (button == _btnGoal)
            {
                _goal = _goal == LobbyGoal.Accuracy ? LobbyGoal.Score : LobbyGoal.Accuracy;
                RebuildWindow();
            }
            else if (button == _btnSettlement)
            {
                _settlementEnabled = !_settlementEnabled;
                RebuildWindow();
            }
            else if (button == _btnPassword)
            {
                ShowPasswordInput();
            }
            else if (button == _btnCreate)
            {
                await CreateLobbyAsync();
            }
        }

        private void ShowNameInput()
        {
            if (_window != null)
            {
                _window.ForceClose();
            }

            var input = new InputWindow();
            input.OnCompletion += (w) =>
            {
                var value = input.Result?.Trim();
                if (!string.IsNullOrWhiteSpace(value) && value.Length <= 24)
                {
                    _roomName = value;
                }
                else if (!string.IsNullOrEmpty(value))
                {
                    MDEN.Managers.ClientLogManager.Warning("Room name is too long. Max length is 24.");
                }

                RebuildWindow();
            };
            input.Show();
        }

        private void ShowPasswordInput()
        {
            if (_window != null)
            {
                _window.ForceClose();
            }

            var input = new InputWindow();
            input.OnCompletion += (w) =>
            {
                var value = input.Result?.Trim();
                if (value != null && value.Length <= 16)
                {
                    _password = string.IsNullOrWhiteSpace(value) ? null : value;
                }
                else if (!string.IsNullOrEmpty(value))
                {
                    MDEN.Managers.ClientLogManager.Warning("Room password is too long. Max length is 16.");
                }

                RebuildWindow();
            };
            input.Show();
        }

        private void ShowMaxPlayersInput()
        {
            ShowNumberInput(I18nManager.T("create.players.title"), 2, 10, value => _maxPlayers = value);
        }

        private void ShowPlaylistSizeInput()
        {
            ShowNumberInput(I18nManager.T("create.playlist_size.title"), 2, 32, value => _playlistSize = value);
        }

        private void ShowNumberInput(string fieldName, int min, int max, Action<ushort> applyValue)
        {
            if (_window != null)
            {
                _window.ForceClose();
            }

            var input = new InputWindow();
            input.OnCompletion += (w) =>
            {
                var value = input.Result?.Trim();
                if (TryParseNumberInRange(value, min, max, out var parsed))
                {
                    applyValue((ushort)parsed);
                }
                else
                {
                    ShowText.ShowInfo(I18nManager.Tf("create.number_range", fieldName, min, max));
                }

                RebuildWindow();
            };
            input.Show();
        }

        private async System.Threading.Tasks.Task CreateLobbyAsync()
        {
            if (_createInProgress)
            {
                ShowText.ShowInfo(I18nManager.T("create.in_progress"));
                return;
            }

            if (!ConnectionManager.CanSendRequests)
            {
                var message = ConnectionManager.IsReconnecting
                    ? I18nManager.T("create.reconnecting")
                    : I18nManager.T("create.not_connected");
                MDEN.Managers.ClientLogManager.Warning($"Create lobby skipped: {message}");
                ShowText.ShowInfo(message);
                if (!ConnectionManager.IsReconnecting)
                {
                    Close();
                    WindowStackController.OpenWindow(new ServerSelectionWindow());
                }
                return;
            }

            _createInProgress = true;
            ShowText.ShowInfo(I18nManager.T("create.creating.toast"));
            RebuildWindow();

            var keepPending = false;
            IDisposable uiLock = WindowStackController.LockUI(I18nManager.T("create.creating.toast"));

            try
            {
                var lobbyId = await LobbyManager.CreateLobbyAsync(
                    _roomName,
                    _maxPlayers,
                    (byte)_playMode,
                    _goal,
                    _playlistSize,
                    _settlementEnabled,
                    _password);
                if (IsDisposed) return;

                keepPending = true;

                MDEN.Managers.ClientLogManager.Msg($"Created lobby: {lobbyId}");
                await MainThreadDispatcher.InvokeAsync(() =>
                {
                    if (IsDisposed) return;
                    Close();
                    NavigationButton.RefreshRoomButton();
                    WindowStackController.OpenWindow(new MyRoomWindow());
                });
            }
            catch (Exception ex)
            {
                MDEN.Managers.ClientLogManager.Warning($"Create lobby failed: {ex.Message}");
                MainThreadDispatcher.Enqueue(() => ShowText.ShowInfo(I18nManager.Tf("create.failed", ex.Message)));
            }
            finally
            {
                if (!keepPending && !IsDisposed)
                {
                    _createInProgress = false;
                    MainThreadDispatcher.Enqueue(RebuildWindow);
                }

                await MainThreadDispatcher.InvokeAsync(() => uiLock?.Dispose());
            }
        }

        private static bool TryParseNumberInRange(string value, int min, int max, out int parsed)
        {
            parsed = 0;
            return !string.IsNullOrWhiteSpace(value) &&
                   int.TryParse(value, out parsed) &&
                   parsed >= min &&
                   parsed <= max;
        }

        private string BuildSummary()
        {
            return I18nManager.Tf(
                "create.summary",
                HighlightValue(EscapeRichText(_roomName)),
                HighlightValue(_maxPlayers),
                FormatPlayMode(_playMode),
                HighlightValue(_playlistSize),
                HighlightValue(GetGoalName()),
                HighlightValue(GetSettlementNamePlain()),
                HighlightValue(string.IsNullOrWhiteSpace(_password) ? I18nManager.T("common.no") : I18nManager.T("common.set")));
        }

        private string GetGoalName()
        {
            return _goal == LobbyGoal.Score ? I18nManager.T("lobby.goal.score") : I18nManager.T("lobby.goal.accuracy");
        }

        private string GetSettlementName()
        {
            return _settlementEnabled
                ? $"<color={Constants.ColorYellow}>{I18nManager.T("common.enabled")}</color>"
                : I18nManager.T("common.disabled");
        }

        private string GetSettlementNamePlain()
        {
            return _settlementEnabled ? I18nManager.T("common.enabled") : I18nManager.T("common.disabled");
        }

        private static string HighlightValue(object value)
        {
            return $"<color={Constants.ColorYellow}>{value}</color>";
        }

        private static string FormatPlayMode(LobbyPlayMode mode)
        {
            return LobbyRuleTextFormatter.FormatPlayMode((byte)mode, false);
        }

        private static string EscapeRichText(string value)
        {
            return string.IsNullOrEmpty(value)
                ? string.Empty
                : value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
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

        private void OnInternalShowInjectTitle(PopupLib.UI.Windows.Abstract.BaseWindow w)
        {
            var uiForward = GameObject.Find("UI/Forward");
            if (uiForward == null) return;

            var pnlBulletin = uiForward.transform.Find("Tips/PnlBulletinNew");
            if (pnlBulletin == null) return;

            var imgBase = pnlBulletin.Find("ImgBase");
            if (imgBase == null) return;

            var oldTitle = imgBase.Find("MDENTitle");
            if (oldTitle != null) UnityEngine.Object.Destroy(oldTitle.gameObject);
            var oldTitleInScroll = imgBase.Find("ScrollView/MDENTitle");
            if (oldTitleInScroll != null) UnityEngine.Object.Destroy(oldTitleInScroll.gameObject);

            var txtTittleObj = pnlBulletin.Find("TxtTittle");
            if (txtTittleObj == null) return;

            var newTitle = GameObject.Instantiate(txtTittleObj.gameObject, imgBase);
            newTitle.name = "MDENTitle";
            newTitle.SetActive(true);

            var loc = newTitle.GetComponent<Il2CppAssets.Scripts.PeroTools.GeneralLocalization.Localization>();
            if (loc != null) UnityEngine.Object.Destroy(loc);

            var txt = newTitle.GetComponent<UnityEngine.UI.Text>();
            if (txt != null)
            {
                txt.text = I18nManager.T("create.title");
                txt.alignment = TextAnchor.MiddleCenter;
            }

            var titleRect = newTitle.GetComponent<RectTransform>();
            if (titleRect != null)
            {
                titleRect.anchorMin = new Vector2(0.5f, 1f);
                titleRect.anchorMax = new Vector2(0.5f, 1f);
                titleRect.pivot = new Vector2(0.5f, 0.5f);
                titleRect.anchoredPosition = new Vector2(0f, 12f);
            }
        }

        private void RemoveInjectedTitle()
        {
            var panel = GameObject.Find("UI/Forward/Tips/PnlBulletinNew");
            if (panel == null) return;

            var titleTrans = panel.transform.Find("ImgBase/ScrollView/MDENTitle");
            if (titleTrans == null) titleTrans = panel.transform.Find("ImgBase/MDENTitle");
            if (titleTrans != null) UnityEngine.Object.Destroy(titleTrans.gameObject);
        }

        public override void Close()
        {
            _lastSelectedIndex = -1;
            _createInProgress = false;
            if (_window != null)
            {
                _window.ForceClose();
                _window = null;
            }
        }
    }
}
