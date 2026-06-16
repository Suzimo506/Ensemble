using System;
using System.Linq;
using System.Threading.Tasks;
using Il2CppAssets.Scripts.UI.Panels;
using MDEN.Managers;
using MDEN.Protocol.Models;
using MDEN.UI.Displays;
using UnityEngine;
using UnityEngine.UI;

namespace MDEN.UI.Core
{
    internal static class BattleResultBannerDisplay
    {
        private const string ResultEntryName = "MDENBattleResultEntry";
        private static readonly TimeSpan CellDelay = TimeSpan.FromMilliseconds(300);
        private static PnlMessage _pnlMessage;

        public static bool ShowingResults { get; private set; }

        public static async Task ShowAsync(BattlePlayerEntry[] players)
        {
            if (!LobbyManager.IsInLobby) return;

            var orderedPlayers = BattleLobbyDisplay
                .OrderPlayers(BattleLobbyDisplay.WithLobbyDefaults(players ?? Array.Empty<BattlePlayerEntry>()))
                .ToArray();
            if (orderedPlayers.Length == 0) return;

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
                    UnityEngine.Object.Destroy(entry.gameObject);
                }
            }
        }

        private static async Task AddOneAsync(string text)
        {
            MainThreadDispatcher.Enqueue(() => AddEntry(text));
            await Task.Delay(CellDelay);
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
                UnityEngine.Object.Destroy(child.gameObject);
            }
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
