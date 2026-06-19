using System;
using System.Linq;
using System.Threading.Tasks;
using Il2CppAssets.Scripts.UI.Panels;
using MDEN.Managers;
using MDEN.Protocol.Models;
using MDEN.UI.Displays;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;

namespace MDEN.UI.Core
{
    internal static class BattleResultBannerDisplay
    {
        private const string ResultEntryName = "MDENBattleResultEntry";
        private static readonly TimeSpan CellDelay = TimeSpan.FromMilliseconds(300);
        private const int PnlMessageRetryCount = 20;
        private const int PnlMessageRetryDelayMs = 150;
        private static PnlMessage _pnlMessage;

        public static bool ShowingResults { get; private set; }

        public static async Task ShowAsync(BattlePlayerEntry[] players)
        {
            if (!LobbyManager.IsInLobby) return;

            var orderedPlayers = BattleLobbyDisplay
                .OrderPlayers(players ?? Array.Empty<BattlePlayerEntry>())
                .ToArray();
            if (orderedPlayers.Length == 0)
            {
                MDEN.Managers.ClientLogManager.Warning("Battle result skipped: no player snapshot.");
                return;
            }

            if (!await WaitForPnlMessageAsync())
            {
                MDEN.Managers.ClientLogManager.Warning("Battle result skipped: PnlMessage is not ready.");
                return;
            }

            ShowingResults = true;
            MainThreadDispatcher.Enqueue(() => Enable(true));

            try
            {
                for (var i = 0; i < orderedPlayers.Length; i++)
                {
                    var player = orderedPlayers[i];
                    var text = BattleLobbyDisplay.FormatResultEntry(player, i + 1);
                    await AddOneAsync(text);
                }

                await CleanupNativeEntriesAsync();
            }
            finally
            {
                ShowingResults = false;
            }
        }

        public static void ClearAll()
        {
            var pnlMessage = GetPnlMessage();
            if (pnlMessage == null || pnlMessage.layout == null) return;

            for (var i = pnlMessage.layout.childCount - 1; i >= 0; i--)
            {
                var entry = pnlMessage.layout.GetChild(i);
                if (entry != null)
                {
                    DestroyTransformObject(entry);
                }
            }
        }

        private static async Task AddOneAsync(string text)
        {
            MainThreadDispatcher.Enqueue(() => AddEntry(text));
            await Task.Delay(CellDelay);
        }

        private static async Task<bool> WaitForPnlMessageAsync()
        {
            for (var i = 0; i < PnlMessageRetryCount; i++)
            {
                if (GetPnlMessage() != null) return true;
                await Task.Delay(PnlMessageRetryDelayMs);
            }

            return false;
        }

        private static void AddEntry(string text)
        {
            var pnlMessage = GetPnlMessage();
            if (pnlMessage == null || pnlMessage.achievement == null || pnlMessage.layout == null) return;

            ClearNativeEntries();

            var entry = UnityEngine.Object.Instantiate(pnlMessage.achievement);
            entry.name = ResultEntryName;
            entry.SetActive(true);
            entry.transform.SetParent(pnlMessage.layout, false);

            var description = entry.transform.Find("TxtDescription")?.GetComponent<Text>();
            if (description != null)
            {
                description.supportRichText = true;
                description.text = text;
            }

            var checkMark = entry.transform.Find("ImgCherkMark");
            if (checkMark != null) checkMark.gameObject.SetActive(false);

            var trophy = entry.transform.Find("Icon/ImgTrophy")?.GetComponent<Image>();
            if (trophy != null)
            {
                var animator = trophy.GetComponent<Animator>();
                if (animator != null) UnityEngine.Object.Destroy(animator);
                trophy.gameObject.SetActive(false);
            }
        }

        private static async Task CleanupNativeEntriesAsync()
        {
            await Task.Delay(500);
            MainThreadDispatcher.Enqueue(ClearNativeEntries);
            await Task.Delay(1500);
            MainThreadDispatcher.Enqueue(ClearNativeEntries);
        }

        private static void ClearNativeEntries()
        {
            var pnlMessage = GetPnlMessage();
            if (pnlMessage == null || pnlMessage.layout == null) return;

            for (var i = pnlMessage.layout.childCount - 1; i >= 0; i--)
            {
                var child = pnlMessage.layout.GetChild(i);
                if (child == null || child.name == ResultEntryName) continue;
                DestroyTransformObject(child);
            }
        }

        private static void DestroyTransformObject(Transform transform)
        {
            if (transform == null) return;
            UnityEngine.Object.Destroy(transform.gameObject);
        }

        private static void Enable(bool clearVanillaMessages)
        {
            var pnlMessage = GetPnlMessage();
            if (pnlMessage == null) return;

            if (clearVanillaMessages)
            {
                ClearAll();
            }

            pnlMessage.gameObject.SetActive(true);
            if (pnlMessage.btnDone != null)
            {
                pnlMessage.btnDone.gameObject.SetActive(true);
            }
        }

        private static PnlMessage GetPnlMessage()
        {
            if (_pnlMessage != null) return _pnlMessage;

            var obj = GameObject.Find("CommonManagers/MessagesManager/UI/PnlMessage");
            if (obj == null) return null;

            _pnlMessage = obj.GetComponent<PnlMessage>();
            return _pnlMessage;
        }
    }
}
