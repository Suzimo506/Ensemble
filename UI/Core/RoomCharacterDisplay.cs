using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppAssets.Scripts.Database;
using Il2CppAssets.Scripts.UI;
using Il2CppAssets.Scripts.UI.Panels.PnlRole;
using MDEN.Managers;
using MDEN.Protocol.Messages.Lobby;
using MDEN.Protocol.Models;
using MelonLoader;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MDEN.UI.Core
{
    public static class RoomCharacterDisplay
    {
        private const int PirateRinGirlIndex = 32;
        private const float PageButtonGap = 8.7f;

        private static GameObject _originalMuseShow;
        private static GameObject _originalElfinShow;
        private static GameObject _sourceButton;
        private static PnlRole _rolePanel;

        private static GameObject _localSlot;
        private static GameObject _rightSlotA;
        private static GameObject _rightSlotB;
        private static GameObject _leftButton;
        private static GameObject _rightButton;
        private static int _localGirlIndex = -1;
        private static int _rightGirlIndexA = -1;
        private static int _rightGirlIndexB = -1;

        private static Vector3 _originalPosition;
        private static Vector3 _originalScale;
        private static int _pageIndex;

        private static readonly Vector3 LocalPosition = new Vector3(-3.8f, -0.85f, 100f);
        private static readonly Vector3 RightPositionA = new Vector3(2.6f, -1.18f, 100f);
        private static readonly Vector3 RightPositionB = new Vector3(6.9f, -1.18f, 100f);
        private static readonly Vector3 LocalScale = new Vector3(0.82f, 0.82f, 0.82f);
        private static readonly Vector3 OtherScale = new Vector3(0.58f, 0.58f, 0.58f);

        public static void Refresh(LobbySyncPush lobby)
        {
            if (lobby == null)
            {
                Destroy();
                return;
            }

            if (!EnsureCreated())
            {
                MelonLogger.Warning("Room character display is not ready.");
                return;
            }

            var localPlayer = GetLocalPlayer(lobby);
            var others = GetOtherPlayers(lobby).ToList();
            var pageCount = Math.Max(1, (int)Math.Ceiling(others.Count / 2f));
            if (_pageIndex >= pageCount) _pageIndex = 0;
            if (_pageIndex < 0) _pageIndex = pageCount - 1;

            ApplySlot(_localSlot, localPlayer, true);
            ApplySlot(_rightSlotA, others.Skip(_pageIndex * 2).FirstOrDefault(), false);
            ApplySlot(_rightSlotB, others.Skip(_pageIndex * 2 + 1).FirstOrDefault(), false);
            SetPageButtonsVisible(pageCount > 1);
        }

        public static void Destroy()
        {
            RestoreOriginal();
            DestroyObject(_rightSlotA);
            DestroyObject(_rightSlotB);
            DestroyObject(_leftButton);
            DestroyObject(_rightButton);

            _originalMuseShow = null;
            _originalElfinShow = null;
            _sourceButton = null;
            _rolePanel = null;
            _localSlot = null;
            _rightSlotA = null;
            _rightSlotB = null;
            _leftButton = null;
            _rightButton = null;
            _localGirlIndex = -1;
            _rightGirlIndexA = -1;
            _rightGirlIndexB = -1;
            _pageIndex = 0;
        }

        private static bool EnsureCreated()
        {
            if (_localSlot != null) return true;

            _originalMuseShow = GameObject.Find("UI/Standerd/PnlHome/MuseShow");
            _originalElfinShow = GameObject.Find("UI/Standerd/PnlHome/ElfinShow");
            _sourceButton = GameObject.Find("UI/Standerd/PnlMenu/Panels/PnlRole/MainShow/FancyScrollView/BtnPrevious");
            _rolePanel = GameObject.Find("UI/Standerd/PnlMenu/Panels/PnlRole")?.GetComponent<PnlRole>();
            if (_originalMuseShow == null || _sourceButton == null || _rolePanel == null)
            {
                MelonLogger.Warning("Room character display source objects are missing.");
                return false;
            }

            if (!_rolePanel.m_IsInit)
            {
                _rolePanel.Init();
            }

            if (_rolePanel.fancyPanel == null)
            {
                MelonLogger.Warning("Role fancy panel is missing.");
                return false;
            }

            InitializeRoleCells();

            _originalPosition = _originalMuseShow.transform.position;
            _originalScale = _originalMuseShow.transform.localScale;

            _localSlot = _originalMuseShow;
            _rightSlotA = UnityEngine.Object.Instantiate(_originalMuseShow, _originalMuseShow.transform.parent);
            _rightSlotB = UnityEngine.Object.Instantiate(_originalMuseShow, _originalMuseShow.transform.parent);
            _rightSlotA.name = "MDENRoomCharacterRightA";
            _rightSlotB.name = "MDENRoomCharacterRightB";

            PrepareMuseShow(_localSlot);
            PrepareMuseShow(_rightSlotA);
            PrepareMuseShow(_rightSlotB);
            CreatePageButtons();
            if (_originalElfinShow != null) _originalElfinShow.SetActive(false);
            return true;
        }

        private static void PrepareMuseShow(GameObject museShow)
        {
            museShow.SetActive(true);
            var interaction = museShow.transform.Find("BtnInteraction");
            if (interaction != null) interaction.gameObject.SetActive(false);

            var bubble = museShow.transform.Find("FirstTwnTalkBubble");
            if (bubble != null) bubble.gameObject.SetActive(false);
        }

        private static void ApplySlot(GameObject slot, RoomCharacterPlayer player, bool local)
        {
            if (slot == null) return;
            slot.SetActive(player != null);
            if (player == null)
            {
                SetCachedGirl(slot, -1);
                return;
            }

            slot.transform.position = local ? LocalPosition : (slot == _rightSlotA ? RightPositionA : RightPositionB);
            slot.transform.localScale = local ? LocalScale : OtherScale;

            var girlIndex = local
                ? GameAccountManager.GetCurrentSelection().GirlIndex
                : player.GirlIndex;
            if (girlIndex < 0) girlIndex = 0;

            if (IsSameGirl(slot, girlIndex)) return;
            ReplaceGirl(slot, girlIndex);
            SetCachedGirl(slot, girlIndex);
        }

        private static bool IsSameGirl(GameObject slot, int girlIndex)
        {
            if (slot == _localSlot) return _localGirlIndex == girlIndex;
            if (slot == _rightSlotA) return _rightGirlIndexA == girlIndex;
            if (slot == _rightSlotB) return _rightGirlIndexB == girlIndex;
            return false;
        }

        private static void SetCachedGirl(GameObject slot, int girlIndex)
        {
            if (slot == _localSlot) _localGirlIndex = girlIndex;
            else if (slot == _rightSlotA) _rightGirlIndexA = girlIndex;
            else if (slot == _rightSlotB) _rightGirlIndexB = girlIndex;
        }

        private static void ReplaceGirl(GameObject museShow, int girlIndex)
        {
            if (_rolePanel?.fancyPanel == null || girlIndex < 0 || museShow == null)
            {
                MelonLogger.Warning($"Skip replacing character. girlIndex={girlIndex}");
                return;
            }

            var prefabTransform = museShow.transform.Find("ShowLocalization/SpinePerfab_other");
            if (prefabTransform == null)
            {
                MelonLogger.Warning("Character prefab transform is missing.");
                return;
            }

            var charInfo = _rolePanel.m_ConfigCharacter?.GetCharacterInfoByIndex(girlIndex);
            if (charInfo == null)
            {
                MelonLogger.Warning($"Character info not found. girlIndex={girlIndex}");
                return;
            }

            var subControl = _rolePanel.fancyPanel.GetCellComponent<PnlRoleSubControl>(charInfo.order - 1);
            if (subControl == null)
            {
                MelonLogger.Warning($"Character cell not ready. order={charInfo.order}");
                return;
            }
            if (!subControl.m_Init) subControl.Init();

            var charApply = subControl.characterApply;
            if (charApply == null)
            {
                MelonLogger.Warning($"Character apply is missing. girlIndex={girlIndex}");
                return;
            }

            for (var i = 0; i < prefabTransform.childCount; i++)
            {
                UnityEngine.Object.Destroy(prefabTransform.GetChild(i).gameObject);
            }

            var newShow = UnityEngine.Object.Instantiate(charApply.gameObject, prefabTransform);
            NormalizeCharacterVisibility(newShow, girlIndex);
            RemoveSpecialCharacterExtras(newShow, girlIndex);

            var museComponent = prefabTransform.gameObject.GetComponent<MuseShow>();
            if (museComponent != null) museComponent.m_MuseShow = newShow;

            MelonLogger.Msg($"Room character replaced: girlIndex={girlIndex}, name={charInfo.characterName}, cos={charInfo.cosName}");
        }

        private static void NormalizeCharacterVisibility(GameObject root, int girlIndex)
        {
            if (root == null) return;

            root.SetActive(true);
            if (girlIndex == PirateRinGirlIndex)
            {
                ActivateHierarchy(root.transform);
            }

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                MelonLogger.Warning($"Character has no renderer after clone. girlIndex={girlIndex}");
            }
            else
            {
                foreach (var renderer in renderers)
                {
                    if (renderer == null) continue;
                    renderer.enabled = true;
                    renderer.sortingOrder = 0;
                    renderer.gameObject.SetActive(true);
                }
            }

            var canvasGroups = root.GetComponentsInChildren<CanvasGroup>(true);
            if (canvasGroups != null)
            {
                foreach (var canvasGroup in canvasGroups)
                {
                    if (canvasGroup == null) continue;
                    canvasGroup.alpha = 1f;
                    canvasGroup.gameObject.SetActive(true);
                }
            }

            if (girlIndex == PirateRinGirlIndex)
            {
                MelonLogger.Msg("Applied pirate Rin visibility normalization.");
            }
        }

        private static void ActivateHierarchy(Transform transform)
        {
            if (transform == null) return;

            transform.gameObject.SetActive(true);
            for (var i = 0; i < transform.childCount; i++)
            {
                ActivateHierarchy(transform.GetChild(i));
            }
        }

        private static void RemoveSpecialCharacterExtras(GameObject newShow, int girlIndex)
        {
            if (newShow == null) return;

            switch (girlIndex)
            {
                case 31:
                    var bloodheirHandler = newShow.GetComponent<Il2CppAssets.Scripts.UI.Specials.BloodheirTransformGenerator>();
                    if (bloodheirHandler != null && bloodheirHandler.m_BloodheirTransformObj != null)
                    {
                        UnityEngine.Object.Destroy(bloodheirHandler.m_BloodheirTransformObj);
                    }
                    break;
                case 33:
                    var diverHandler = newShow.GetComponent<Il2CppAssets.Scripts.UI.Specials.DiverBuroHandler>();
                    if (diverHandler != null && diverHandler.m_PnlDaveFishViewObj != null)
                    {
                        UnityEngine.Object.Destroy(diverHandler.m_PnlDaveFishViewObj);
                    }
                    break;
            }
        }

        private static void CreatePageButtons()
        {
            _leftButton = UnityEngine.Object.Instantiate(_sourceButton, _originalMuseShow.transform.parent);
            _rightButton = UnityEngine.Object.Instantiate(_sourceButton, _originalMuseShow.transform.parent);
            _leftButton.name = "MDENRoomCharacterPrev";
            _rightButton.name = "MDENRoomCharacterNext";

            _leftButton.transform.position = new Vector3(-PageButtonGap, 0f, 100f);
            _rightButton.transform.position = new Vector3(PageButtonGap, 0f, 100f);
            _leftButton.transform.localScale = new Vector3(-0.56f, 0.56f, 0.56f);
            _rightButton.transform.localScale = new Vector3(0.56f, 0.56f, 0.56f);

            SetupPageButton(_leftButton, () =>
            {
                _pageIndex--;
                Refresh(LobbyManager.CurrentLobby);
            });
            SetupPageButton(_rightButton, () =>
            {
                _pageIndex++;
                Refresh(LobbyManager.CurrentLobby);
            });
        }

        private static void SetupPageButton(GameObject target, Action action)
        {
            var image = target.GetComponent<Image>();
            if (image != null)
            {
                image.color = new Color(0.8f, 0.2f, 0.8f, 1f);
            }

            var button = target.GetComponent<Button>();
            if (button == null) button = target.AddComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener((UnityAction)(() => action.Invoke()));

            var eventTrigger = target.GetComponent<UnityEngine.EventSystems.EventTrigger>();
            if (eventTrigger != null) UnityEngine.Object.Destroy(eventTrigger);
        }

        private static void SetPageButtonsVisible(bool visible)
        {
            if (_leftButton != null) _leftButton.SetActive(visible);
            if (_rightButton != null) _rightButton.SetActive(visible);
        }

        private static RoomCharacterPlayer GetLocalPlayer(LobbySyncPush lobby)
        {
            var uid = PlayerManager.CurrentUid;
            return GetPlayers(lobby).FirstOrDefault(p => p.Uid == uid)
                ?? new RoomCharacterPlayer
                {
                    Uid = uid,
                    Name = PlayerManager.CurrentProfile?.Name ?? uid,
                    GirlIndex = GameAccountManager.GetCurrentSelection().GirlIndex,
                    ElfinIndex = GameAccountManager.GetCurrentSelection().ElfinIndex
                };
        }

        private static IEnumerable<RoomCharacterPlayer> GetOtherPlayers(LobbySyncPush lobby)
        {
            var currentUid = PlayerManager.CurrentUid;
            foreach (var player in GetPlayers(lobby))
            {
                if (player.Uid == currentUid) continue;
                yield return player;
            }
        }

        private static IEnumerable<RoomCharacterPlayer> GetPlayers(LobbySyncPush lobby)
        {
            var characterMap = lobby.PlayerCharacters?
                .Where(p => !string.IsNullOrEmpty(p?.Uid))
                .ToDictionary(p => p.Uid, p => p);

            if (lobby.PlayerDetails != null && lobby.PlayerDetails.Length > 0)
            {
                foreach (var player in lobby.PlayerDetails)
                {
                    if (string.IsNullOrEmpty(player?.Uid)) continue;
                    var character = GetCharacter(characterMap, player.Uid);
                    yield return new RoomCharacterPlayer
                    {
                        Uid = player.Uid,
                        Name = string.IsNullOrEmpty(player.Name) ? player.Uid : player.Name,
                        GirlIndex = GetGirlIndex(player.Uid, character),
                        ElfinIndex = GetElfinIndex(player.Uid, character)
                    };
                }

                yield break;
            }

            if (lobby.Players == null) yield break;
            foreach (var uid in lobby.Players)
            {
                if (string.IsNullOrEmpty(uid)) continue;
                var character = GetCharacter(characterMap, uid);
                yield return new RoomCharacterPlayer
                {
                    Uid = uid,
                    Name = uid,
                    GirlIndex = GetGirlIndex(uid, character),
                    ElfinIndex = GetElfinIndex(uid, character)
                };
            }
        }

        private static void InitializeRoleCells()
        {
            var fancyPanel = _rolePanel.fancyPanel;
            var scrollView = fancyPanel?.m_FancyScrollView;
            if (scrollView == null) return;

            for (var i = 0; i < scrollView.itemCount; i++)
            {
                scrollView.ScrollToDataIndex(i, 0, true);
            }

            var localSelection = GameAccountManager.GetCurrentSelection();
            var localInfo = _rolePanel.m_ConfigCharacter?.GetCharacterInfoByIndex(localSelection.GirlIndex);
            if (localInfo != null)
            {
                scrollView.ScrollToDataIndex(localInfo.order - 1, 0, true);
            }
        }

        private static int GetGirlIndex(string uid, LobbyPlayerCharacterEntry character)
        {
            if (uid == PlayerManager.CurrentUid)
            {
                return GameAccountManager.GetCurrentSelection().GirlIndex;
            }

            return character?.GirlIndex ?? 0;
        }

        private static int GetElfinIndex(string uid, LobbyPlayerCharacterEntry character)
        {
            if (uid == PlayerManager.CurrentUid)
            {
                return GameAccountManager.GetCurrentSelection().ElfinIndex;
            }

            return character?.ElfinIndex ?? -1;
        }

        private static LobbyPlayerCharacterEntry GetCharacter(
            Dictionary<string, LobbyPlayerCharacterEntry> characterMap,
            string uid)
        {
            if (characterMap == null || string.IsNullOrEmpty(uid)) return null;
            characterMap.TryGetValue(uid, out var character);
            return character;
        }

        private static void RestoreOriginal()
        {
            if (_originalMuseShow != null)
            {
                _originalMuseShow.transform.position = _originalPosition;
                _originalMuseShow.transform.localScale = _originalScale;
                var interaction = _originalMuseShow.transform.Find("BtnInteraction");
                if (interaction != null) interaction.gameObject.SetActive(true);
            }

            if (_originalElfinShow != null)
            {
                _originalElfinShow.SetActive(true);
            }
        }

        private static void DestroyObject(GameObject obj)
        {
            if (obj != null)
            {
                UnityEngine.Object.Destroy(obj);
            }
        }

        private sealed class RoomCharacterPlayer
        {
            public string Uid { get; set; }
            public string Name { get; set; }
            public int GirlIndex { get; set; }
            public int ElfinIndex { get; set; }
        }
    }
}
