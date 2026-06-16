using System;
using System.Threading.Tasks;
using LocalizeLib;
using MDEN.Managers;
using MDEN.Protocol.Enums;
using MDEN.Protocol.Messages.Lobby;
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
        private ForumObject _btnPlaylistSize;
        private ForumObject _btnGoal;
        private ForumObject _btnSettlement;
        private ForumObject _btnCreate;
        private int _lastSelectedIndex = -1;

        private string _roomName = "联机房间";
        private ushort _maxPlayers = 4;
        private ushort _playlistSize = 12;
        private LobbyGoal _goal = LobbyGoal.Accuracy;
        private bool _settlementEnabled;

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

            _btnBack = new ForumObject(new LocalString("- 返回 -"), new LocalString("回到房间列表"));
            _btnBack.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("OptionsPanel.png")?.texture;
            _window.ForumObjects.Add(_btnBack);

            _btnName = new ForumObject(new LocalString("房间名称"), new LocalString($"当前: {_roomName}\n点击后输入房间名称，24字上限"));
            _btnName.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("HomePanel.png")?.texture;
            _window.ForumObjects.Add(_btnName);

            _btnMaxPlayers = new ForumObject(new LocalString("人数"), new LocalString($"当前: {_maxPlayers}\n点击在 2/4/6/8/10 间切换"));
            _btnMaxPlayers.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("PlayerCard.png")?.texture;
            _window.ForumObjects.Add(_btnMaxPlayers);

            _btnPlaylistSize = new ForumObject(new LocalString("歌曲列表长度"), new LocalString($"当前: {_playlistSize}\n点击在 8/12/16/24/32 间切换"));
            _btnPlaylistSize.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("RoomList.png")?.texture;
            _window.ForumObjects.Add(_btnPlaylistSize);

            _btnGoal = new ForumObject(new LocalString("获胜方式"), new LocalString($"当前: {GetGoalName()}\n点击在准确率和分数间切换"));
            _btnGoal.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("SocialNetwork.png")?.texture;
            _window.ForumObjects.Add(_btnGoal);

            _btnSettlement = new ForumObject(new LocalString("结算功能"), new LocalString($"当前: {GetSettlementName()}\n开启后每五首歌弹出一次结算"));
            _btnSettlement.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("OptionsPanel.png")?.texture;
            _window.ForumObjects.Add(_btnSettlement);

            _btnCreate = new ForumObject(new LocalString($"<color={Constants.ColorYellow}>- 确认创建 -</color>"), new LocalString(BuildSummary()));
            _btnCreate.Texture = ResourceManager.GetRandomBannerTexture() ?? ResourceManager.GetSprite("RoomList.png")?.texture;
            _window.ForumObjects.Add(_btnCreate);
        }

        private async void OnSelectionChanged(PopupLib.UI.Windows.Interfaces.IListWindow window, int objectIndex)
        {
            if (_window == null || objectIndex < 0 || objectIndex >= _window.ForumObjects.Count) return;

            if (_lastSelectedIndex != objectIndex)
            {
                _lastSelectedIndex = objectIndex;
                return;
            }

            var button = _window.ForumObjects[objectIndex];
            if (button == _btnBack)
            {
                Close();
                UIManager.OpenWindow(new RoomListWindow());
            }
            else if (button == _btnName)
            {
                ShowNameInput();
            }
            else if (button == _btnMaxPlayers)
            {
                CycleMaxPlayers();
                RebuildWindow();
            }
            else if (button == _btnPlaylistSize)
            {
                CyclePlaylistSize();
                RebuildWindow();
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
                    MelonLogger.Warning("Room name is too long. Max length is 24.");
                }

                RebuildWindow();
            };
            input.Show();
        }

        private async Task CreateLobbyAsync()
        {
            if (!ConnectionManager.IsLoggedIn)
            {
                MelonLogger.Warning("Create lobby skipped: not connected to server. Please choose a server again.");
                Close();
                UIManager.OpenWindow(new ServerSelectionWindow());
                return;
            }

            using var _ = UIManager.LockUI("Creating lobby...");

            try
            {
                var request = new CreateLobbyRequest
                {
                    Name = _roomName,
                    MaxPlayers = _maxPlayers,
                    PlayType = (byte)LobbyPlayType.All,
                    ChartSelection = (byte)LobbyChartSelection.HostPlaylist,
                    Goal = (byte)_goal,
                    PlaylistSize = _playlistSize,
                    SettlementEnabled = _settlementEnabled
                };

                var lobbyId = await LobbyManager.CreateLobbyAsync(request);
                if (IsDisposed) return;

                LobbyManager.MarkLobbyEntered(lobbyId, request);

                MelonLogger.Msg($"Created lobby: {lobbyId}");
                MainThreadDispatcher.Enqueue(() =>
                {
                    if (IsDisposed) return;
                    Close();
                    NavigationButton.RefreshRoomButton();
                    UIManager.OpenWindow(new MyRoomWindow());
                });
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Create lobby failed: {ex.Message}");
            }
        }

        private void CycleMaxPlayers()
        {
            _maxPlayers = _maxPlayers switch
            {
                2 => 4,
                4 => 6,
                6 => 8,
                8 => 10,
                _ => 2
            };
        }

        private void CyclePlaylistSize()
        {
            _playlistSize = _playlistSize switch
            {
                8 => 12,
                12 => 16,
                16 => 24,
                24 => 32,
                _ => 8
            };
        }

        private string BuildSummary()
        {
            return $"名称: {_roomName}\n人数: <color={Constants.ColorYellow}>{_maxPlayers}</color>\n歌曲列表长度: {_playlistSize}\n获胜方式: {GetGoalName()}\n结算功能: {GetSettlementName()}";
        }

        private string GetGoalName()
        {
            return _goal == LobbyGoal.Score ? "分数" : "准确率";
        }

        private string GetSettlementName()
        {
            return _settlementEnabled
                ? $"<color={Constants.ColorYellow}>开启</color>"
                : "关闭";
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
                txt.text = "创建房间";
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
            if (_window != null)
            {
                _window.ForceClose();
                _window = null;
            }
        }
    }
}
