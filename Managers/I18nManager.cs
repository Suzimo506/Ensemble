using System;
using System.Collections.Generic;
using Il2CppAssets.Scripts.Database;
using MelonLoader;

namespace MDEN.Managers
{
    public static class I18nManager
    {
        private const string FallbackLanguage = "zh-CN";
        private static readonly Dictionary<string, Dictionary<string, string>> Strings = new Dictionary<string, Dictionary<string, string>>();
        private static readonly Dictionary<int, string> GameLanguageMap = new Dictionary<int, string>
        {
            { 0, "zh-CN" },
            { 1, "en" },
            { 2, "zh-CN" },
            { 3, "zh-TW" },
            { 4, "ja" },
            { 5, "ko" }
        };

        private static string _currentLanguage = FallbackLanguage;
        private static bool _initialized;

        public static string CurrentLanguage
        {
            get
            {
                RefreshLanguage();
                return _currentLanguage;
            }
        }

        public static void Initialize()
        {
            if (_initialized) return;

            RegisterAll();
            RefreshLanguage();
            _initialized = true;
            ClientLogManager.Msg($"I18n initialized. Language={_currentLanguage}");
        }

        public static string T(string key)
        {
            if (!_initialized)
            {
                Initialize();
            }

            RefreshLanguage();
            if (Strings.TryGetValue(key, out var languages))
            {
                if (languages.TryGetValue(_currentLanguage, out var localized))
                {
                    return localized;
                }

                if (languages.TryGetValue(FallbackLanguage, out var fallback))
                {
                    return fallback;
                }

                if (languages.TryGetValue("en", out var english))
                {
                    return english;
                }
            }

            return key;
        }

        public static string Tf(string key, params object[] args)
        {
            var template = T(key);
            try
            {
                return string.Format(template, args);
            }
            catch (FormatException)
            {
                return template;
            }
        }

        private static void RefreshLanguage()
        {
            try
            {
                var gameId = (int)GlobalDataBase.s_DbUi.curLanguageIndex;
                _currentLanguage = GameLanguageMap.TryGetValue(gameId, out var code)
                    ? code
                    : "en";
            }
            catch
            {
                _currentLanguage = FallbackLanguage;
            }
        }

        private static void Register(string key, string zhCn, string en, string zhTw, string ja, string ko)
        {
            Strings[key] = new Dictionary<string, string>
            {
                { "zh-CN", zhCn },
                { "en", en },
                { "zh-TW", zhTw },
                { "ja", ja },
                { "ko", ko }
            };
        }

        private static void RegisterAll()
        {
            Register("common.back.button", "- 返回 -", "- Back -", "- 返回 -", "- 戻る -", "- 뒤로 -");
            Register("common.back.main_menu", "回到主菜单", "Return to the main menu", "返回主選單", "メインメニューに戻る", "메인 메뉴로 돌아가기");
            Register("common.back.previous_window", "回到上一个窗口", "Return to the previous window", "返回上一個視窗", "前のウィンドウに戻る", "이전 창으로 돌아가기");
            Register("common.back.room_list", "回到房间列表", "Return to the room list", "返回房間列表", "ルーム一覧に戻る", "방 목록으로 돌아가기");
            Register("common.back.server_list", "回到节点列表", "Return to the server list", "返回節點列表", "ノード一覧に戻る", "서버 목록으로 돌아가기");
            Register("common.back.my_room", "回到我的房间", "Return to my room", "返回我的房間", "自分のルームに戻る", "내 방으로 돌아가기");
            Register("common.confirm", "确认", "Confirm", "確認", "確認", "확인");
            Register("common.none", "暂无", "None", "暫無", "なし", "없음");
            Register("common.unknown", "未知", "Unknown", "未知", "不明", "알 수 없음");
            Register("common.loading", "查询中...", "Loading...", "查詢中...", "確認中...", "조회 중...");
            Register("common.processing", "处理中...", "Processing...", "處理中...", "処理中...", "처리 중...");
            Register("common.current_setting", "当前设置：<color={0}>{1}</color>", "Current setting: <color={0}>{1}</color>", "目前設定：<color={0}>{1}</color>", "現在の設定：<color={0}>{1}</color>", "현재 설정: <color={0}>{1}</color>");
            Register("common.enabled.popup", "<color=00ff00ff>开启</color>", "<color=00ff00ff>ON</color>", "<color=00ff00ff>開啟</color>", "<color=00ff00ff>オン</color>", "<color=00ff00ff>켜짐</color>");
            Register("common.disabled.popup", "<color=ff4444ff>关闭</color>", "<color=ff4444ff>OFF</color>", "<color=ff4444ff>關閉</color>", "<color=ff4444ff>オフ</color>", "<color=ff4444ff>꺼짐</color>");
            Register("common.enabled", "开启", "On", "開啟", "オン", "켜짐");
            Register("common.disabled", "关闭", "Off", "關閉", "オフ", "꺼짐");
            Register("common.open", "开放", "Open", "開放", "開放中", "열림");
            Register("common.locked", "已上锁", "Locked", "已上鎖", "ロック中", "잠김");
            Register("common.no", "无", "None", "無", "なし", "없음");
            Register("common.set", "已设置", "Set", "已設定", "設定済み", "설정됨");
            Register("common.required", "需要", "Required", "需要", "必要", "필요");
            Register("common.unknown_error", "未知错误", "Unknown error", "未知錯誤", "不明なエラー", "알 수 없는 오류");

            Register("main.title", "一起合奏吧", "Let's Ensemble", "一起合奏吧", "一緒に合奏しよう", "함께 연주해요");
            Register("main.about", "关于", "About", "關於", "情報", "정보");
            Register("main.profile", "个人信息", "Profile", "個人資訊", "プロフィール", "프로필");
            Register("main.friends", "好友列表", "Friends", "好友列表", "フレンド", "친구 목록");
            Register("main.lobbies", "联机大厅", "Multiplayer Lobby", "聯機大廳", "マルチプレイロビー", "멀티플레이 로비");
            Register("main.settings", "设置", "Settings", "設定", "設定", "설정");
            Register("main.support", "支持我们", "Support Us", "支持我們", "支援する", "후원하기");
            Register("main.profile.desc", "更改自己的名字、个人简介、以及个性化修改", "Edit your name, bio, and personal settings", "更改自己的名字、個人簡介以及個人化設定", "名前、自己紹介、個人設定を変更します", "이름, 소개, 개인 설정을 변경합니다");
            Register("main.friends.desc", "查看自己的好友，与好友一起玩吧！", "View your friends and play together!", "查看自己的好友，與好友一起玩吧！", "フレンドを確認して一緒に遊ぼう！", "친구를 보고 함께 플레이하세요!");
            Register("main.lobbies.desc", "加入服务器，与服务器的其他人一起愉快的组队吧！", "Join a server and team up with other players!", "加入伺服器，和其他玩家一起組隊吧！", "サーバーに参加してほかのプレイヤーと遊ぼう！", "서버에 참가해 다른 플레이어와 함께 즐겨요!");
            Register("main.settings.desc", "更改游戏的各种设置喵", "Change Ensemble settings", "更改 Ensemble 的各種設定", "Ensemble の設定を変更します", "Ensemble 설정을 변경합니다");
            Register("main.support.desc", "前往爱发电支持我们，让服务器更长久！OVO", "Support us on Afdian and help keep the servers running!", "前往愛發電支持我們，讓伺服器更長久！OVO", "Afdianで支援して、サーバー運営を応援してください！", "Afdian에서 후원해 서버 운영을 도와주세요!");

            Register("settings.title", "设置", "Settings", "設定", "設定", "설정");
            Register("settings.favgirl.desc", "将我的 FavGirl 角色和精灵显示给其他玩家", "Show my FavGirl character and elfin to other players", "向其他玩家顯示我的 FavGirl 角色與精靈", "FavGirl のキャラとエルフィンを他のプレイヤーに表示します", "내 FavGirl 캐릭터와 엘핀을 다른 플레이어에게 표시합니다");
            Register("settings.hide_health.title", "隐藏血量", "Hide HP", "隱藏血量", "HPを隠す", "체력 숨기기");
            Register("settings.hide_health.desc", "隐藏游戏内血量条和 Fever 条", "Hide the in-game HP and Fever bars", "隱藏遊戲內血量條與 Fever 條", "ゲーム内のHPバーとFeverバーを隠します", "게임 내 체력 바와 Fever 바를 숨깁니다");
            Register("settings.debug_logs.title", "Debug日志", "Debug Logs", "Debug 日誌", "デバッグログ", "디버그 로그");
            Register("settings.debug_logs.desc", "显示客户端调试日志和警告日志", "Show client debug and warning logs", "顯示客戶端除錯與警告日誌", "クライアントのデバッグログと警告ログを表示します", "클라이언트 디버그 및 경고 로그를 표시합니다");

            Register("server.title", "节点列表", "Server List", "節點列表", "ノード一覧", "서버 목록");
            Register("server.refresh.button", "- 刷新 -", "- Refresh -", "- 重新整理 -", "- 更新 -", "- 새로고침 -");
            Register("server.refresh.desc", "重新获取最新的服务器节点", "Fetch the latest server nodes", "重新取得最新伺服器節點", "最新のサーバーノードを取得します", "최신 서버 노드를 다시 가져옵니다");
            Register("server.add.button", "- 添加服务器 -", "- Add Server -", "- 新增伺服器 -", "- サーバー追加 -", "- 서버 추가 -");
            Register("server.add.desc", "添加私人服务器长期到列表", "Save a private server to the list", "將私人伺服器長期加入列表", "プライベートサーバーを一覧に保存します", "개인 서버를 목록에 저장합니다");
            Register("server.join.button", "- 加入服务器 -", "- Join Server -", "- 加入伺服器 -", "- サーバー参加 -", "- 서버 참가 -");
            Register("server.join.desc", "临时加入私人服务器", "Temporarily join a private server", "暫時加入私人伺服器", "プライベートサーバーに一時参加します", "개인 서버에 임시로 참가합니다");
            Register("server.fetching", "正在获取节点...", "Fetching servers...", "正在取得節點...", "ノード取得中...", "서버 목록 가져오는 중...");
            Register("server.offline", "获取信息失败或服务器离线", "Failed to fetch info or server is offline", "取得資訊失敗或伺服器離線", "情報取得に失敗したかサーバーがオフラインです", "정보를 가져오지 못했거나 서버가 오프라인입니다");
            Register("server.stats", "版本: <color={0}>{1}</color>\n当前在线: <color={2}>{3}</color> 人\n房间数量: <color={4}>{5}</color> 个", "Version: <color={0}>{1}</color>\nOnline: <color={2}>{3}</color>\nRooms: <color={4}>{5}</color>", "版本: <color={0}>{1}</color>\n目前在線: <color={2}>{3}</color> 人\n房間數量: <color={4}>{5}</color> 個", "バージョン: <color={0}>{1}</color>\nオンライン: <color={2}>{3}</color>人\nルーム数: <color={4}>{5}</color>", "버전: <color={0}>{1}</color>\n현재 접속: <color={2}>{3}</color>명\n방 수: <color={4}>{5}</color>개");
            Register("server.missing", "节点不存在", "Server does not exist", "節點不存在", "ノードが存在しません", "서버가 없습니다");
            Register("server.custom.default_status", "当前在线: 未刷新\n房间数量: 未刷新", "Online: not refreshed\nRooms: not refreshed", "目前在線: 未重新整理\n房間數量: 未重新整理", "オンライン: 未更新\nルーム数: 未更新", "현재 접속: 새로고침 안 됨\n방 수: 새로고침 안 됨");
            Register("server.custom.desc", "IP地址: {0}\n{1}\n点击后管理该服务器", "IP address: {0}\n{1}\nClick to manage this server", "IP 位址: {0}\n{1}\n點擊後管理此伺服器", "IPアドレス: {0}\n{1}\nクリックしてこのサーバーを管理", "IP 주소: {0}\n{1}\n클릭하면 이 서버를 관리합니다");
            Register("server.node.shanghai", "上海", "Shanghai", "上海", "上海", "상하이");
            Register("server.node.shandong", "山东", "Shandong", "山東", "山東", "산둥");
            Register("server.node.hubei", "湖北", "Hubei", "湖北", "湖北", "후베이");
            Register("server.node.hongkong", "香港", "Hong Kong", "香港", "香港", "홍콩");
            Register("server.node.us", "美国", "United States", "美國", "アメリカ", "미국");
            Register("server.node.test", "内测节点", "Test Server", "內測節點", "テストノード", "테스트 서버");

            Register("profile.title", "个人信息", "Profile", "個人資訊", "プロフィール", "프로필");
            Register("profile.local.title", "本地资料", "Local Profile", "本機資料", "ローカルプロフィール", "로컬 프로필");
            Register("profile.local.saved", "个人信息会保存到本地 Ensemble.json", "Profile info is saved locally to Ensemble.json", "個人資訊會儲存到本機 Ensemble.json", "プロフィール情報はローカルの Ensemble.json に保存されます", "프로필 정보는 로컬 Ensemble.json에 저장됩니다");
            Register("profile.summary", "名字: {0}\n颜色: {1}\n介绍: {2}\n入场提示: {3}\n头衔: {4}", "Name: {0}\nColor: {1}\nBio: {2}\nEntrance message: {3}\nTitle: {4}", "名字: {0}\n顏色: {1}\n介紹: {2}\n入場提示: {3}\n頭銜: {4}", "名前: {0}\n色: {1}\n自己紹介: {2}\n入室メッセージ: {3}\n称号: {4}", "이름: {0}\n색상: {1}\n소개: {2}\n입장 메시지: {3}\n칭호: {4}");
            Register("profile.avatar.edit", "修改头像", "Change Avatar", "修改頭像", "アバター変更", "아바타 변경");
            Register("profile.avatar.back", "回到个人信息", "Return to profile", "返回個人資訊", "プロフィールに戻る", "프로필로 돌아가기");
            Register("profile.name.edit", "修改名字", "Change Name", "修改名字", "名前変更", "이름 변경");
            Register("profile.color.edit", "修改名字颜色", "Change Name Color", "修改名字顏色", "名前色変更", "이름 색상 변경");
            Register("profile.bio.edit", "修改个人介绍", "Change Bio", "修改個人介紹", "自己紹介変更", "소개 변경");
            Register("profile.entrance.edit", "修改入场提示语", "Change Entrance Message", "修改入場提示語", "入室メッセージ変更", "입장 메시지 변경");
            Register("profile.title.edit", "修改头衔", "Change Title", "修改頭銜", "称号変更", "칭호 변경");
            Register("profile.name.desc", "修改自己的名字，16字上限", "Change your name. 16 characters max.", "修改自己的名字，最多 16 字", "名前を変更します。最大16文字。", "이름을 변경합니다. 최대 16자.");
            Register("profile.color.desc", "输入十六进制颜色，不要带#，例如 ff00ff", "Enter a hex color without #, for example ff00ff", "輸入十六進位顏色，不要帶 #，例如 ff00ff", "# なしの16進色を入力します。例: ff00ff", "# 없이 16진수 색상을 입력하세요. 예: ff00ff");
            Register("profile.bio.desc", "别人在房间点击你的卡片时显示的介绍，30字上限", "Shown when others click your card in a room. 30 characters max.", "別人在房間點擊你的卡片時顯示的介紹，最多 30 字", "ルームで他の人があなたのカードをクリックした時に表示されます。最大30文字。", "방에서 다른 사람이 내 카드를 클릭하면 표시됩니다. 최대 30자.");
            Register("profile.entrance.desc", "进入房间时显示的提示语，12字上限", "Shown when entering a room. 12 characters max.", "進入房間時顯示的提示語，最多 12 字", "ルーム入室時に表示されます。最大12文字。", "방에 입장할 때 표시됩니다. 최대 12자.");
            Register("profile.title.desc", "显示在个人信息中的头衔，12字上限", "Shown in your profile. 12 characters max.", "顯示在個人資訊中的頭銜，最多 12 字", "プロフィールに表示される称号です。最大12文字。", "프로필에 표시되는 칭호입니다. 최대 12자.");
            Register("profile.unset", "未设置", "Not set", "未設定", "未設定", "설정 안 됨");
            Register("profile.avatar.guide", "<color={0}>添加头像教程：\n1. 将 png、jpg 或 jpeg 图片放入头像文件夹\n2. 再次点击左侧“修改头像”进入头像列表\n3. 选择图片后会自动裁成圆形并长期保存\n当前头像文件夹：{1}</color>", "<color={0}>Avatar guide:\n1. Put png, jpg, or jpeg images into the avatar folder\n2. Click Change Avatar again to open the avatar list\n3. The selected image will be cropped into a circle and saved\nAvatar folder: {1}</color>", "<color={0}>新增頭像教學：\n1. 將 png、jpg 或 jpeg 圖片放入頭像資料夾\n2. 再次點擊左側「修改頭像」進入頭像列表\n3. 選擇圖片後會自動裁成圓形並長期保存\n目前頭像資料夾：{1}</color>", "<color={0}>アバター追加ガイド：\n1. png、jpg、jpeg 画像をアバターフォルダに入れる\n2. もう一度「アバター変更」を押して一覧を開く\n3. 選択した画像は円形に切り抜かれて保存されます\nアバターフォルダ：{1}</color>", "<color={0}>아바타 추가 안내:\n1. png, jpg, jpeg 이미지를 아바타 폴더에 넣으세요\n2. 왼쪽의 아바타 변경을 다시 눌러 목록을 여세요\n3. 선택한 이미지는 원형으로 잘려 저장됩니다\n아바타 폴더: {1}</color>");
            Register("profile.validation.name", "名字不能为空且最多16字", "Name cannot be empty and must be 16 characters or fewer", "名字不能為空且最多 16 字", "名前は空にできず、最大16文字です", "이름은 비워둘 수 없으며 최대 16자입니다");
            Register("profile.validation.color", "名字颜色必须是十六进制颜色，不要带#", "Name color must be a hex color without #", "名字顏色必須是十六進位顏色，不要帶 #", "名前色は # なしの16進色で入力してください", "이름 색상은 # 없는 16진수여야 합니다");
            Register("profile.validation.bio", "个人介绍最多30字", "Bio must be 30 characters or fewer", "個人介紹最多 30 字", "自己紹介は最大30文字です", "소개는 최대 30자입니다");
            Register("profile.validation.entrance", "入场提示语最多12字", "Entrance message must be 12 characters or fewer", "入場提示語最多 12 字", "入室メッセージは最大12文字です", "입장 메시지는 최대 12자입니다");
            Register("profile.validation.title", "头衔最多12字", "Title must be 12 characters or fewer", "頭銜最多 12 字", "称号は最大12文字です", "칭호는 최대 12자입니다");

            Register("lobby.title", "选择房间", "Select Room", "選擇房間", "ルーム選択", "방 선택");
            Register("lobby.refresh.desc", "重新获取当前服务器的房间列表", "Refresh the room list from this server", "重新取得目前伺服器的房間列表", "このサーバーのルーム一覧を更新します", "현재 서버의 방 목록을 새로고침합니다");
            Register("lobby.create.button", "- 创建房间 -", "- Create Room -", "- 建立房間 -", "- ルーム作成 -", "- 방 만들기 -");
            Register("lobby.create.desc", "创建新的联机房间", "Create a new multiplayer room", "建立新的聯機房間", "新しいマルチルームを作成します", "새 멀티플레이 방을 만듭니다");
            Register("lobby.empty.title", "暂无房间", "No Rooms", "暫無房間", "ルームなし", "방 없음");
            Register("lobby.empty.desc", "当前服务器没有公开房间，可以刷新或创建房间", "There are no public rooms on this server. Refresh or create one.", "目前伺服器沒有公開房間，可以重新整理或建立房間", "このサーバーに公開ルームはありません。更新するか作成してください。", "현재 서버에 공개 방이 없습니다. 새로고침하거나 방을 만들어 보세요.");
            Register("lobby.private_suffix", "（私密）", " (Private)", "（私密）", "（非公開）", " (비공개)");
            Register("lobby.joining_suffix", " - 加入中...", " - Joining...", " - 加入中...", " - 参加中...", " - 참가 중...");
            Register("lobby.joining.desc", "请求已提交，正在等待服务器回应\n{0}", "Request sent. Waiting for server response...\n{0}", "請求已送出，正在等待伺服器回應\n{0}", "リクエスト送信済み。サーバー応答待ち...\n{0}", "요청을 보냈습니다. 서버 응답 대기 중...\n{0}");
            Register("lobby.desc", "房主: <color={0}>{1}</color>\n人数: <color={2}>{3}/{4}</color>\n游玩模式: {5}\n歌曲列表: <color={6}>{7}/{8}</color>\n玩法: <color={9}>{10}</color>\n选谱: <color={11}>{12}</color>\n获胜方式: <color={13}>{14}</color>\n结算功能: <color={15}>{16}</color>\n加入限制: {17}\n密码: {18}\n状态: {19}", "Host: <color={0}>{1}</color>\nPlayers: <color={2}>{3}/{4}</color>\nMode: {5}\nPlaylist: <color={6}>{7}/{8}</color>\nCharts: <color={9}>{10}</color>\nSelection: <color={11}>{12}</color>\nGoal: <color={13}>{14}</color>\nSettlement: <color={15}>{16}</color>\nJoin: {17}\nPassword: {18}\nStatus: {19}", "房主: <color={0}>{1}</color>\n人數: <color={2}>{3}/{4}</color>\n遊玩模式: {5}\n歌曲列表: <color={6}>{7}/{8}</color>\n玩法: <color={9}>{10}</color>\n選譜: <color={11}>{12}</color>\n勝利方式: <color={13}>{14}</color>\n結算功能: <color={15}>{16}</color>\n加入限制: {17}\n密碼: {18}\n狀態: {19}", "ホスト: <color={0}>{1}</color>\n人数: <color={2}>{3}/{4}</color>\nモード: {5}\nプレイリスト: <color={6}>{7}/{8}</color>\n譜面種別: <color={9}>{10}</color>\n選曲: <color={11}>{12}</color>\n勝利条件: <color={13}>{14}</color>\nリザルト: <color={15}>{16}</color>\n参加制限: {17}\nパスワード: {18}\n状態: {19}", "방장: <color={0}>{1}</color>\n인원: <color={2}>{3}/{4}</color>\n모드: {5}\n플레이리스트: <color={6}>{7}/{8}</color>\n곡 종류: <color={9}>{10}</color>\n선곡: <color={11}>{12}</color>\n승리 조건: <color={13}>{14}</color>\n정산 기능: <color={15}>{16}</color>\n입장 제한: {17}\n비밀번호: {18}\n상태: {19}");
            Register("lobby.desc.tenzi", "房主: <color={0}>{1}</color>\n人数: <color={2}>{3}/{4}</color>\n游玩模式: {5}\n歌曲列表: <color={6}>{7}/{8}</color>\n玩法: <color={9}>{10}</color>\n选谱: <color={11}>{12}</color>\n获胜方式: <color={13}>{14}</color>\n结算功能: <color={15}>{16}</color>\n加入限制: {17}\n密码: {18}\n状态: {19}\n每人点歌数: <color={20}>{21}</color>", "Host: <color={0}>{1}</color>\nPlayers: <color={2}>{3}/{4}</color>\nMode: {5}\nPlaylist: <color={6}>{7}/{8}</color>\nCharts: <color={9}>{10}</color>\nSelection: <color={11}>{12}</color>\nGoal: <color={13}>{14}</color>\nSettlement: <color={15}>{16}</color>\nJoin: {17}\nPassword: {18}\nStatus: {19}\nSongs per player: <color={20}>{21}</color>", "房主: <color={0}>{1}</color>\n人數: <color={2}>{3}/{4}</color>\n遊玩模式: {5}\n歌曲列表: <color={6}>{7}/{8}</color>\n玩法: <color={9}>{10}</color>\n選譜: <color={11}>{12}</color>\n勝利方式: <color={13}>{14}</color>\n結算功能: <color={15}>{16}</color>\n加入限制: {17}\n密碼: {18}\n狀態: {19}\n每人點歌數: <color={20}>{21}</color>", "ホスト: <color={0}>{1}</color>\n人数: <color={2}>{3}/{4}</color>\nモード: {5}\nプレイリスト: <color={6}>{7}/{8}</color>\n譜面種別: <color={9}>{10}</color>\n選曲: <color={11}>{12}</color>\n勝利条件: <color={13}>{14}</color>\nリザルト: <color={15}>{16}</color>\n参加制限: {17}\nパスワード: {18}\n状態: {19}\n1人あたりの曲数: <color={20}>{21}</color>", "방장: <color={0}>{1}</color>\n인원: <color={2}>{3}/{4}</color>\n모드: {5}\n플레이리스트: <color={6}>{7}/{8}</color>\n곡 종류: <color={9}>{10}</color>\n선곡: <color={11}>{12}</color>\n승리 조건: <color={13}>{14}</color>\n정산 기능: <color={15}>{16}</color>\n입장 제한: {17}\n비밀번호: {18}\n상태: {19}\n1인당 선곡 수: <color={20}>{21}</color>");
            Register("lobby.goal.score", "分数", "Score", "分數", "スコア", "점수");
            Register("lobby.goal.accuracy", "准确率", "Accuracy", "準確率", "精度", "정확도");
            Register("lobby.goal.custom", "自定义", "Custom", "自訂", "カスタム", "사용자 지정");
            Register("lobby.play_type.all", "全部", "All", "全部", "すべて", "전체");
            Register("lobby.play_type.vanilla", "仅官方谱", "Official only", "僅官方譜", "公式のみ", "공식 곡만");
            Register("lobby.play_type.custom", "仅自定义谱", "Custom only", "僅自訂譜", "カスタムのみ", "커스텀 곡만");
            Register("lobby.chart_selection.host", "房主歌单", "Host playlist", "房主歌單", "ホストのリスト", "방장 목록");
            Register("lobby.chart_selection.playlist", "列表轮换", "Playlist rotation", "列表輪換", "リストローテーション", "목록 순환");
            Register("lobby.chart_selection.random", "随机", "Random", "隨機", "ランダム", "랜덤");
            Register("lobby.status.playing", "游戏中", "Playing", "遊戲中", "プレイ中", "게임 중");
            Register("lobby.status.locked", "已锁定", "Locked", "已鎖定", "ロック中", "잠김");
            Register("lobby.status.waiting", "等待中", "Waiting", "等待中", "待機中", "대기 중");
            Register("lobby.join_in_progress", "正在加入房间，请稍候", "Joining room. Please wait.", "正在加入房間，請稍候", "ルームに参加中です。お待ちください。", "방에 참가 중입니다. 잠시만 기다려 주세요.");
            Register("lobby.room_locked", "房间已上锁", "Room is locked", "房間已上鎖", "ルームはロックされています", "방이 잠겨 있습니다");
            Register("lobby.switch.title", "切换房间", "Switch Room", "切換房間", "ルーム切り替え", "방 전환");
            Register("lobby.switch.confirm", "确认离开当前房间并加入「{0}」吗？", "Leave the current room and join \"{0}\"?", "確認離開目前房間並加入「{0}」嗎？", "現在のルームを退出して「{0}」に参加しますか？", "현재 방을 나가고 \"{0}\"에 참가할까요?");
            Register("lobby.fetching", "Fetching lobby list...", "Fetching lobby list...", "正在取得房間列表...", "ルーム一覧取得中...", "방 목록 가져오는 중...");
            Register("lobby.joining", "正在加入房间...", "Joining room...", "正在加入房間...", "ルーム参加中...", "방 참가 중...");
            Register("lobby.join_failed", "加入失败：{0}", "Join failed: {0}", "加入失敗：{0}", "参加失敗：{0}", "참가 실패: {0}");

            Register("create.title", "创建房间", "Create Room", "建立房間", "ルーム作成", "방 만들기");
            Register("create.default_room_name", "联机房间", "Multiplayer Room", "聯機房間", "マルチルーム", "멀티플레이 방");
            Register("create.name.title", "房间名称", "Room Name", "房間名稱", "ルーム名", "방 이름");
            Register("create.name.desc", "房间名称: {0}\n点击后输入房间名称，24字上限", "Room name: {0}\nClick to enter a room name. 24 characters max.", "房間名稱: {0}\n點擊後輸入房間名稱，最多 24 字", "ルーム名: {0}\nクリックしてルーム名を入力します。最大24文字。", "방 이름: {0}\n클릭해서 방 이름을 입력하세요. 최대 24자.");
            Register("create.players.title", "人数", "Players", "人數", "人数", "인원");
            Register("create.players.desc", "最多人数: {0}\n点击后输入人数，范围 2-10", "Max players: {0}\nClick to enter a value from 2 to 10", "最多人數: {0}\n點擊後輸入人數，範圍 2-10", "最大人数: {0}\nクリックして 2-10 の範囲で入力します", "최대 인원: {0}\n클릭해서 2-10 범위로 입력하세요");
            Register("create.play_mode.title", "游玩模式", "Play Mode", "遊玩模式", "プレイモード", "플레이 모드");
            Register("create.play_mode.desc", "游玩模式: {0}\n点击切换为{1}", "Play mode: {0}\nClick to switch to {1}", "遊玩模式: {0}\n點擊切換為{1}", "プレイモード: {0}\nクリックで {1} に切り替え", "플레이 모드: {0}\n클릭하면 {1}(으)로 전환");
            Register("create.playlist_size.title", "歌曲列表长度", "Playlist Size", "歌曲列表長度", "プレイリスト長", "플레이리스트 길이");
            Register("create.playlist_size.desc", "列表长度: {0}\n点击后输入歌曲列表长度，范围 2-32", "Playlist size: {0}\nClick to enter a value from 2 to 32", "列表長度: {0}\n點擊後輸入歌曲列表長度，範圍 2-32", "リスト長: {0}\nクリックして 2-32 の範囲で入力します", "목록 길이: {0}\n클릭해서 2-32 범위로 입력하세요");
            Register("tenzi.songs_per_player.title", "每人点歌数", "Songs Per Player", "每人點歌數", "1人あたりの曲数", "1인당 선곡 수");
            Register("tenzi.songs_per_player.desc", "每人点歌数: {0}\n点击后输入数量，范围 1-{1}", "Songs per player: {0}\nClick to enter a value from 1 to {1}", "每人點歌數: {0}\n點擊後輸入數量，範圍 1-{1}", "1人あたりの曲数: {0}\nクリックして 1-{1} の範囲で入力します", "1인당 선곡 수: {0}\n클릭해서 1-{1} 범위로 입력하세요");
            Register("tenzi.songs_per_player.room_desc", "当前: {0}\n点击后输入数量，范围 1-{1}", "Current: {0}\nClick to enter a value from 1 to {1}", "目前: {0}\n點擊後輸入數量，範圍 1-{1}", "現在: {0}\nクリックして 1-{1} の範囲で入力します", "현재: {0}\n클릭해서 1-{1} 범위로 입력하세요");
            Register("tenzi.songs_per_player.existing_limit", "已有玩家点歌数达到 {0}，请先移除多余谱面", "A player already has {0} songs. Remove extra charts first.", "已有玩家點歌數達到 {0}，請先移除多餘譜面", "既に {0} 曲選んでいるプレイヤーがいます。先に余分な譜面を削除してください。", "이미 {0}곡을 선택한 플레이어가 있습니다. 먼저 초과 채보를 제거하세요");
            Register("tenzi.songs_per_player.existing_limit_server", "已有玩家点歌数超过新上限，请先移除多余谱面", "A player already exceeds the new limit. Remove extra charts first.", "已有玩家點歌數超過新上限，請先移除多餘譜面", "新しい上限を超えているプレイヤーがいます。先に余分な譜面を削除してください。", "새 제한을 초과한 플레이어가 있습니다. 먼저 초과 채보를 제거하세요");
            Register("create.goal.title", "获胜方式", "Goal", "勝利方式", "勝利条件", "승리 조건");
            Register("create.goal.desc", "获胜方式: {0}\n点击在准确率和分数间切换", "Goal: {0}\nClick to switch between accuracy and score", "勝利方式: {0}\n點擊在準確率與分數間切換", "勝利条件: {0}\nクリックで精度とスコアを切り替え", "승리 조건: {0}\n클릭해서 정확도와 점수를 전환");
            Register("create.settlement.title", "结算功能", "Settlement", "結算功能", "リザルト機能", "정산 기능");
            Register("create.settlement.desc", "结算功能: {0}\n开启后每五首歌弹出一次结算", "Settlement: {0}\nWhen enabled, a settlement appears every five songs", "結算功能: {0}\n開啟後每五首歌彈出一次結算", "リザルト機能: {0}\n有効時は5曲ごとにリザルトを表示します", "정산 기능: {0}\n켜면 5곡마다 정산이 표시됩니다");
            Register("create.password.title", "房间密码", "Room Password", "房間密碼", "ルームパスワード", "방 비밀번호");
            Register("create.password.desc", "密码: {0}\n输入空内容可清除密码，16字上限", "Password: {0}\nEnter empty text to clear it. 16 characters max.", "密碼: {0}\n輸入空內容可清除密碼，最多 16 字", "パスワード: {0}\n空欄入力で解除。最大16文字。", "비밀번호: {0}\n빈 값으로 입력하면 제거됩니다. 최대 16자.");
            Register("create.confirm.button", "- 确认创建 -", "- Create -", "- 確認建立 -", "- 作成 -", "- 만들기 -");
            Register("create.creating.button", "- 创建中... -", "- Creating... -", "- 建立中... -", "- 作成中... -", "- 만드는 중... -");
            Register("create.creating.desc", "请求已提交，正在等待服务器回应\n名称: {0}", "Request sent. Waiting for server response...\nName: {0}", "請求已送出，正在等待伺服器回應\n名稱: {0}", "リクエスト送信済み。サーバー応答待ち...\n名前: {0}", "요청을 보냈습니다. 서버 응답 대기 중...\n이름: {0}");
            Register("create.in_progress", "正在创建房间，请稍候", "Creating room. Please wait.", "正在建立房間，請稍候", "ルーム作成中です。お待ちください。", "방을 만드는 중입니다. 잠시만 기다려 주세요.");
            Register("create.number_range", "{0}必须在 {1}-{2} 之间", "{0} must be between {1} and {2}", "{0}必須在 {1}-{2} 之間", "{0} は {1}-{2} の範囲で入力してください", "{0}은(는) {1}-{2} 사이여야 합니다");
            Register("create.reconnecting", "正在重连服务器，请稍候", "Reconnecting to server. Please wait.", "正在重新連線伺服器，請稍候", "サーバーに再接続中です。お待ちください。", "서버에 재연결 중입니다. 잠시만 기다려 주세요.");
            Register("create.not_connected", "未连接服务器，请重新选择节点", "Not connected. Please choose a server again.", "未連線伺服器，請重新選擇節點", "未接続です。サーバーを選び直してください。", "서버에 연결되지 않았습니다. 서버를 다시 선택하세요.");
            Register("create.creating.toast", "正在创建房间...", "Creating room...", "正在建立房間...", "ルーム作成中...", "방 만드는 중...");
            Register("create.failed", "创建失败：{0}", "Create failed: {0}", "建立失敗：{0}", "作成失敗：{0}", "생성 실패: {0}");
            Register("create.summary", "名称: {0}\n人数: {1}\n游玩模式: {2}\n歌曲列表长度: {3}\n获胜方式: {4}\n结算功能: {5}\n密码: {6}", "Name: {0}\nPlayers: {1}\nPlay mode: {2}\nPlaylist size: {3}\nGoal: {4}\nSettlement: {5}\nPassword: {6}", "名稱: {0}\n人數: {1}\n遊玩模式: {2}\n歌曲列表長度: {3}\n勝利方式: {4}\n結算功能: {5}\n密碼: {6}", "名前: {0}\n人数: {1}\nモード: {2}\nプレイリスト長: {3}\n勝利条件: {4}\nリザルト: {5}\nパスワード: {6}", "이름: {0}\n인원: {1}\n모드: {2}\n목록 길이: {3}\n승리 조건: {4}\n정산 기능: {5}\n비밀번호: {6}");

            Register("create.summary.tenzi", "名称: {0}\n人数: {1}\n游玩模式: {2}\n歌曲列表长度: {3}\n每人点歌数: {4}\n获胜方式: {5}\n结算功能: {6}\n密码: {7}", "Name: {0}\nPlayers: {1}\nPlay mode: {2}\nPlaylist size: {3}\nSongs per player: {4}\nGoal: {5}\nSettlement: {6}\nPassword: {7}", "名稱: {0}\n人數: {1}\n遊玩模式: {2}\n歌曲列表長度: {3}\n每人點歌數: {4}\n勝利方式: {5}\n結算功能: {6}\n密碼: {7}", "名前: {0}\n人数: {1}\nモード: {2}\nプレイリスト長: {3}\n1人あたりの曲数: {4}\n勝利条件: {5}\nリザルト: {6}\nパスワード: {7}", "이름: {0}\n인원: {1}\n모드: {2}\n목록 길이: {3}\n1인당 선곡 수: {4}\n승리 조건: {5}\n정산 기능: {6}\n비밀번호: {7}");
            Register("room.title", "我的房间", "My Room", "我的房間", "マイルーム", "내 방");
            Register("room.leave.button", "- 退出房间 -", "- Leave Room -", "- 退出房間 -", "- ルーム退出 -", "- 방 나가기 -");
            Register("room.leave.desc", "离开当前联机房间", "Leave the current multiplayer room", "離開目前聯機房間", "現在のマルチルームを退出します", "현재 멀티플레이 방에서 나갑니다");
            Register("room.not_in_lobby", "Not in lobby.", "Not in lobby.", "不在房間中。", "ルームにいません。", "방에 있지 않습니다.");
            Register("room.setting.desc", "当前: {0}\n点击切换为{1}", "Current: {0}\nClick to switch to {1}", "目前: {0}\n點擊切換為{1}", "現在: {0}\nクリックで {1} に切り替え", "현재: {0}\n클릭하면 {1}(으)로 전환");
            Register("room.setting_failed", "设置失败：{0}", "Setting failed: {0}", "設定失敗：{0}", "設定に失敗しました：{0}", "설정 실패: {0}");
            Register("room.settlement.desc", "当前: {0}\n点击{1}每五首结算", "Current: {0}\nClick to {1} settlement every five songs", "目前: {0}\n點擊{1}每五首結算", "現在: {0}\nクリックで5曲ごとのリザルトを{1}", "현재: {0}\n클릭하면 5곡마다 정산을 {1}");
            Register("room.unlock.button", "- 手动解锁 -", "- Unlock -", "- 手動解鎖 -", "- 手動解除 -", "- 수동 잠금 해제 -");
            Register("room.lock.button", "- 手动上锁 -", "- Lock -", "- 手動上鎖 -", "- 手動ロック -", "- 수동 잠금 -");
            Register("room.unlock.desc", "解锁后其他玩家可以加入房间", "Other players can join after unlocking", "解鎖後其他玩家可以加入房間", "解除すると他のプレイヤーが参加できます", "잠금을 해제하면 다른 플레이어가 참가할 수 있습니다");
            Register("room.lock.desc", "上锁后其他玩家不能加入房间", "Other players cannot join after locking", "上鎖後其他玩家不能加入房間", "ロックすると他のプレイヤーは参加できません", "잠그면 다른 플레이어가 참가할 수 없습니다");
            Register("room.password.change.button", "- 修改/清除密码 -", "- Change/Clear Password -", "- 修改/清除密碼 -", "- パスワード変更/解除 -", "- 비밀번호 변경/제거 -");
            Register("room.password.set.button", "- 设置密码 -", "- Set Password -", "- 設定密碼 -", "- パスワード設定 -", "- 비밀번호 설정 -");
            Register("room.password.change.desc", "当前房间需要密码加入，输入空内容可清除密码", "This room requires a password. Enter empty text to clear it.", "目前房間需要密碼加入，輸入空內容可清除密碼", "このルームはパスワードが必要です。空欄入力で解除できます。", "현재 방은 비밀번호가 필요합니다. 빈 값으로 입력하면 제거됩니다.");
            Register("room.password.set.desc", "设置后房间列表会显示（私密），加入时需要输入密码", "After setting it, the room appears as private and requires a password to join", "設定後房間列表會顯示（私密），加入時需要輸入密碼", "設定するとルーム一覧に非公開として表示され、参加時にパスワードが必要です", "설정하면 방 목록에 비공개로 표시되고 참가 시 비밀번호가 필요합니다");
            Register("room.summary", "房主: {0}\n人数: {1}\n游玩模式: {2}\n歌曲列表: {3}\n获胜方式: {4}\n结算功能: {5}\n加入限制: {6}\n密码: {7}\n状态: {8}", "Host: {0}\nPlayers: {1}\nMode: {2}\nPlaylist: {3}\nGoal: {4}\nSettlement: {5}\nJoin: {6}\nPassword: {7}\nStatus: {8}", "房主: {0}\n人數: {1}\n遊玩模式: {2}\n歌曲列表: {3}\n勝利方式: {4}\n結算功能: {5}\n加入限制: {6}\n密碼: {7}\n狀態: {8}", "ホスト: {0}\n人数: {1}\nモード: {2}\nプレイリスト: {3}\n勝利条件: {4}\nリザルト: {5}\n参加制限: {6}\nパスワード: {7}\n状態: {8}", "방장: {0}\n인원: {1}\n모드: {2}\n플레이리스트: {3}\n승리 조건: {4}\n정산 기능: {5}\n입장 제한: {6}\n비밀번호: {7}\n상태: {8}");
            Register("room.playlist.tenzi", "{0}，每人 {1} 首", "{0}, {1} each", "{0}，每人 {1} 首", "{0}、1人 {1} 曲", "{0}, 1인당 {1}곡");
            Register("room.leave.confirm", "是否退出房间？", "Leave the room?", "是否退出房間？", "ルームを退出しますか？", "방에서 나갈까요?");
            Register("room.host_only", "只有房主能进行该操作", "Only the host can do this", "只有房主能進行該操作", "ホストのみ操作できます", "방장만 이 작업을 할 수 있습니다");
            Register("room.rules_locked", "游戏准备或进行中，不能修改房间规则", "Room rules cannot be changed during preparation or play", "遊戲準備或進行中，不能修改房間規則", "準備中またはプレイ中はルールを変更できません", "준비 중이거나 게임 중에는 방 규칙을 변경할 수 없습니다");
            Register("room.stop_first", "请先停止游戏", "Please stop the game first", "請先停止遊戲", "先にゲームを停止してください", "먼저 게임을 중지하세요");
            Register("room.leaving", "Leaving lobby...", "Leaving lobby...", "正在離開房間...", "ルーム退出中...", "방 나가는 중...");

            Register("player.info.title", "玩家信息", "Player Info", "玩家資訊", "プレイヤー情報", "플레이어 정보");
            Register("player.add_friend", "添加好友", "Add Friend", "新增好友", "フレンド追加", "친구 추가");
            Register("player.add_friend.desc", "向 {0} 发送好友请求", "Send a friend request to {0}", "向 {0} 發送好友請求", "{0} にフレンド申請を送ります", "{0}에게 친구 요청을 보냅니다");
            Register("player.kick", "踢出", "Kick", "踢出", "キック", "강퇴");
            Register("player.kick.desc", "将该玩家踢出房间", "Kick this player from the room", "將該玩家踢出房間", "このプレイヤーをルームからキックします", "이 플레이어를 방에서 내보냅니다");
            Register("player.transfer_host", "移交房主", "Transfer Host", "移交房主", "ホスト委任", "방장 넘기기");
            Register("player.transfer_host.desc", "将房主权限移交给该玩家", "Transfer host privileges to this player", "將房主權限移交給該玩家", "ホスト権限をこのプレイヤーに委任します", "방장 권한을 이 플레이어에게 넘깁니다");
            Register("player.ban_chart", "禁止选谱", "Ban Chart Selection", "禁止選譜", "選曲禁止", "선곡 금지");
            Register("player.unban_chart", "解除选谱限制", "Allow Chart Selection", "解除選譜限制", "選曲制限解除", "선곡 제한 해제");
            Register("player.ban_chart.desc", "禁止该玩家选择谱面", "Prevent this player from selecting charts", "禁止該玩家選擇譜面", "このプレイヤーの選曲を禁止します", "이 플레이어가 곡을 선택하지 못하게 합니다");
            Register("player.unban_chart.desc", "允许该玩家选择谱面", "Allow this player to select charts", "允許該玩家選擇譜面", "このプレイヤーの選曲を許可します", "이 플레이어가 곡을 선택할 수 있게 합니다");
            Register("player.mute", "禁言", "Mute", "禁言", "ミュート", "채팅 금지");
            Register("player.unmute", "解除禁言", "Unmute", "解除禁言", "ミュート解除", "채팅 금지 해제");
            Register("player.mute.desc", "禁止该玩家发送聊天消息", "Prevent this player from sending chat messages", "禁止該玩家發送聊天訊息", "このプレイヤーのチャット送信を禁止します", "이 플레이어가 채팅 메시지를 보내지 못하게 합니다");
            Register("player.unmute.desc", "允许该玩家发送聊天消息", "Allow this player to send chat messages", "允許該玩家發送聊天訊息", "このプレイヤーのチャット送信を許可します", "이 플레이어가 채팅 메시지를 보낼 수 있게 합니다");
            Register("player.invalid", "目标玩家无效", "Invalid target player", "目標玩家無效", "対象プレイヤーが無効です", "대상 플레이어가 올바르지 않습니다");
            Register("player.add_friend.confirm", "确认向 {0} 发送好友请求吗？", "Send a friend request to {0}?", "確認向 {0} 發送好友請求嗎？", "{0} にフレンド申請を送りますか？", "{0}에게 친구 요청을 보낼까요?");
            Register("player.kick.title", "踢出玩家", "Kick Player", "踢出玩家", "プレイヤーをキック", "플레이어 강퇴");
            Register("player.kick.confirm", "确认将 {0} 踢出房间吗？", "Kick {0} from the room?", "確認將 {0} 踢出房間嗎？", "{0} をルームからキックしますか？", "{0}님을 방에서 내보낼까요?");
            Register("player.transfer.confirm", "确认将房主移交给 {0} 吗？", "Transfer host to {0}?", "確認將房主移交給 {0} 嗎？", "ホストを {0} に委任しますか？", "방장을 {0}님에게 넘길까요?");
            Register("player.ban_chart.confirm", "确认禁止 {0} 选谱吗？", "Ban {0} from selecting charts?", "確認禁止 {0} 選譜嗎？", "{0} の選曲を禁止しますか？", "{0}님의 선곡을 금지할까요?");
            Register("player.unban_chart.confirm", "确认允许 {0} 选谱吗？", "Allow {0} to select charts?", "確認允許 {0} 選譜嗎？", "{0} の選曲を許可しますか？", "{0}님의 선곡을 허용할까요?");
            Register("player.mute.confirm", "确认禁言 {0} 吗？", "Mute {0}?", "確認禁言 {0} 嗎？", "{0} をミュートしますか？", "{0}님을 채팅 금지할까요?");
            Register("player.unmute.confirm", "确认解除 {0} 的禁言吗？", "Unmute {0}?", "確認解除 {0} 的禁言嗎？", "{0} のミュートを解除しますか？", "{0}님의 채팅 금지를 해제할까요?");
            Register("player.not_in_room", "当前不在房间中", "You are not in a room", "目前不在房間中", "現在ルームにいません", "현재 방에 있지 않습니다");
            Register("player.host_only", "只有房主可以使用该操作", "Only the host can use this action", "只有房主可以使用該操作", "ホストのみこの操作を使えます", "방장만 이 작업을 사용할 수 있습니다");
            Register("player.no_self_action", "不能对自己使用该操作", "You cannot use this action on yourself", "不能對自己使用該操作", "自分にはこの操作を使えません", "자기 자신에게는 이 작업을 사용할 수 없습니다");
            Register("player.action_locked", "游戏准备或进行中，不能使用该操作", "This action cannot be used during preparation or play", "遊戲準備或進行中，不能使用該操作", "準備中またはプレイ中はこの操作を使えません", "준비 중이거나 게임 중에는 이 작업을 사용할 수 없습니다");
            Register("friend.request_sent", "好友请求已发送", "Friend request sent", "好友請求已發送", "フレンド申請を送信しました", "친구 요청을 보냈습니다");
            Register("friend.added", "已添加好友", "Friend added", "已新增好友", "フレンドに追加しました", "친구를 추가했습니다");
            Register("friend.removed", "已删除好友", "Friend removed", "已刪除好友", "フレンドを削除しました", "친구를 삭제했습니다");
            Register("friend.request_cancelled", "已取消好友请求", "Friend request cancelled", "已取消好友請求", "フレンド申請を取り消しました", "친구 요청을 취소했습니다");
            Register("friend.request_declined", "已拒绝好友请求", "Friend request declined", "已拒絕好友請求", "フレンド申請を拒否しました", "친구 요청을 거절했습니다");
            Register("friend.unchanged", "好友状态未变化", "Friend status unchanged", "好友狀態未變化", "フレンド状態は変わっていません", "친구 상태가 변경되지 않았습니다");
            Register("friend.updated", "好友状态已更新", "Friend status updated", "好友狀態已更新", "フレンド状態を更新しました", "친구 상태가 업데이트되었습니다");

            Register("common.saving", "保存中...", "Saving...", "儲存中...", "保存中...", "저장 중...");
            Register("common.connecting", "正在连接服务器...", "Connecting to server...", "正在連線伺服器...", "サーバーに接続中...", "서버에 연결 중...");
            Register("common.removing", "移除中...", "Removing...", "移除中...", "削除中...", "제거 중...");
            Register("common.starting", "启动中...", "Starting...", "啟動中...", "開始中...", "시작 중...");
            Register("common.applying", "应用中...", "Applying...", "套用中...", "適用中...", "적용 중...");
            Register("common.stopping", "停止中...", "Stopping...", "停止中...", "停止中...", "중지 중...");

            Register("main.credits.text",
                "当前Ensemble版本：<color={0}>0.4.3</color>\n加入喵斯兔交流群，反馈问题，与其他人一起合奏！\n群号：<color={1}>331568783</color>\n[ 开发者 ]\n<color={2}>Suzimo506</color> - MDEN项目发起与维护人\n<color={2}>XMJjs</color> - Mod与服务器后端开发者\n<color={2}>Cookie</color> - Mod与服务器后端开发者\n[ 测试团队 ]\nMSC☆言叶. AP的是给 浅夏 莼畹ChunWan_ Tvv 佩奇 冬瓜糖 飞翔冥王星 晓屿云叙 GreenHub 纯鹿人 苏达 nuxwalker 萧千岁 fish 姫坂乃愛\n[ 特别感谢 ]\n<color={3}>莼畹ChunWan_</color> - 宣传大使\n<color={3}>浅夏ぬ浅离</color> - 横幅选择与剪辑\n<color={3}>XMJjs</color> - mden.top网站维护人\n<color={3}>Cookie</color> - mden.top网站维护人\n\n",
                "Current Ensemble version: <color={0}>0.4.3</color>\nJoin the Museto community to report issues and play together!\nGroup: <color={1}>331568783</color>\n[ Developers ]\n<color={2}>Suzimo506</color> - MDEN project founder and maintainer\n<color={2}>XMJjs</color> - Mod and server backend developer\n<color={2}>Cookie</color> - Mod and server backend developer\n[ Test Team ]\nMSC☆言叶. AP的是给 浅夏 莼畹ChunWan_ Tvv 佩奇 冬瓜糖 飞翔冥王星 晓屿云叙 GreenHub 纯鹿人 苏达 nuxwalker 萧千岁 fish 姫坂乃愛\n[ Special Thanks ]\n<color={3}>莼畹ChunWan_</color> - Promotion ambassador\n<color={3}>浅夏ぬ浅离</color> - Banner selection and editing\n<color={3}>XMJjs</color> - mden.top website maintainer\n<color={3}>Cookie</color> - mden.top website maintainer\n\n",
                "目前 Ensemble 版本：<color={0}>0.4.3</color>\n加入喵斯兔交流群，回報問題，與其他人一起合奏！\n群號：<color={1}>331568783</color>\n[ 開發者 ]\n<color={2}>Suzimo506</color> - MDEN 專案發起與維護人\n<color={2}>XMJjs</color> - Mod 與伺服器後端開發者\n<color={2}>Cookie</color> - Mod 與伺服器後端開發者\n[ 測試團隊 ]\nMSC☆言叶. AP的是给 浅夏 莼畹ChunWan_ Tvv 佩奇 冬瓜糖 飞翔冥王星 晓屿云叙 GreenHub 纯鹿人 苏达 nuxwalker 萧千岁 fish 姫坂乃愛\n[ 特別感謝 ]\n<color={3}>莼畹ChunWan_</color> - 宣傳大使\n<color={3}>浅夏ぬ浅离</color> - 橫幅選擇與剪輯\n<color={3}>XMJjs</color> - mden.top 網站維護人\n<color={3}>Cookie</color> - mden.top 網站維護人\n\n",
                "現在の Ensemble バージョン：<color={0}>0.4.3</color>\nMuseto 交流グループに参加して、不具合報告や合奏を楽しみましょう！\nグループ番号：<color={1}>331568783</color>\n[ 開発者 ]\n<color={2}>Suzimo506</color> - MDEN プロジェクト発起人・メンテナー\n<color={2}>XMJjs</color> - Mod・サーバーバックエンド開発\n<color={2}>Cookie</color> - Mod・サーバーバックエンド開発\n[ テストチーム ]\nMSC☆言叶. AP的是给 浅夏 莼畹ChunWan_ Tvv 佩奇 冬瓜糖 飞翔冥王星 晓屿云叙 GreenHub 纯鹿人 苏达 nuxwalker 萧千岁 fish 姫坂乃愛\n[ Special Thanks ]\n<color={3}>莼畹ChunWan_</color> - 宣伝大使\n<color={3}>浅夏ぬ浅离</color> - バナー選定・編集\n<color={3}>XMJjs</color> - mden.top サイト管理\n<color={3}>Cookie</color> - mden.top サイト管理\n\n",
                "현재 Ensemble 버전: <color={0}>0.4.3</color>\nMuseto 교류 그룹에 참여해 문제를 제보하고 함께 연주하세요!\n그룹 번호: <color={1}>331568783</color>\n[ 개발자 ]\n<color={2}>Suzimo506</color> - MDEN 프로젝트 시작 및 유지보수\n<color={2}>XMJjs</color> - Mod 및 서버 백엔드 개발자\n<color={2}>Cookie</color> - Mod 및 서버 백엔드 개발자\n[ 테스트 팀 ]\nMSC☆言叶. AP的是给 浅夏 莼畹ChunWan_ Tvv 佩奇 冬瓜糖 飞翔冥王星 晓屿云叙 GreenHub 纯鹿人 苏达 nuxwalker 萧千岁 fish 姫坂乃愛\n[ 특별 감사 ]\n<color={3}>莼畹ChunWan_</color> - 홍보 대사\n<color={3}>浅夏ぬ浅离</color> - 배너 선정 및 편집\n<color={3}>XMJjs</color> - mden.top 사이트 유지보수\n<color={3}>Cookie</color> - mden.top 사이트 유지보수\n\n");

            Register("server.custom.management.title", "管理自定义节点", "Manage Custom Server", "管理自訂節點", "カスタムノード管理", "사용자 지정 서버 관리");
            Register("server.custom.default_name", "自定义节点", "Custom Server", "自訂節點", "カスタムノード", "사용자 지정 서버");
            Register("server.custom.join.button", "- 加入 -", "- Join -", "- 加入 -", "- 参加 -", "- 참가 -");
            Register("server.custom.rename.button", "- 重命名 -", "- Rename -", "- 重新命名 -", "- 名前変更 -", "- 이름 변경 -");
            Register("server.custom.delete.button", "- 删除 -", "- Delete -", "- 刪除 -", "- 削除 -", "- 삭제 -");
            Register("server.custom.join.desc", "加入节点: {0}", "Join server: {0}", "加入節點: {0}", "ノードに参加: {0}", "서버 참가: {0}");
            Register("server.custom.rename.desc", "重命名节点: {0}", "Rename server: {0}", "重新命名節點: {0}", "ノード名を変更: {0}", "서버 이름 변경: {0}");
            Register("server.custom.delete.desc", "从列表中删除节点: {0}", "Delete server from the list: {0}", "從列表刪除節點: {0}", "一覧からノードを削除: {0}", "목록에서 서버 삭제: {0}");

            Register("avatar.title", "选择头像", "Select Avatar", "選擇頭像", "アバター選択", "아바타 선택");
            Register("avatar.default.name", "默认头像", "Default Avatar", "預設頭像", "デフォルトアバター", "기본 아바타");
            Register("avatar.generic_name", "头像", "Avatar", "頭像", "アバター", "아바타");
            Register("avatar.default.desc", "使用 Ensemble 默认头像", "Use the default Ensemble avatar", "使用 Ensemble 預設頭像", "Ensemble のデフォルトアバターを使います", "Ensemble 기본 아바타 사용");
            Register("avatar.use.desc", "使用这个头像\n{0}", "Use this avatar\n{0}", "使用這個頭像\n{0}", "このアバターを使う\n{0}", "이 아바타 사용\n{0}");
            Register("avatar.empty.title", "暂无头像文件", "No Avatar Files", "暫無頭像檔案", "アバターなし", "아바타 파일 없음");
            Register("avatar.empty.guide", "<color={0}>把 png、jpg 或 jpeg 图片放入头像文件夹后，重新进入这个页面即可选择。\n当前头像文件夹：{1}</color>", "<color={0}>Put png, jpg, or jpeg images into the avatar folder, then reopen this page to select one.\nAvatar folder: {1}</color>", "<color={0}>將 png、jpg 或 jpeg 圖片放入頭像資料夾後，重新進入此頁即可選擇。\n目前頭像資料夾：{1}</color>", "<color={0}>png、jpg、jpeg 画像をアバターフォルダに入れてから、このページを開き直すと選択できます。\nアバターフォルダ：{1}</color>", "<color={0}>png, jpg, jpeg 이미지를 아바타 폴더에 넣은 뒤 이 페이지를 다시 열면 선택할 수 있습니다.\n아바타 폴더: {1}</color>");
            Register("avatar.updated", "头像已更新", "Avatar updated", "頭像已更新", "アバターを更新しました", "아바타가 업데이트되었습니다");
            Register("avatar.file_missing", "头像文件不存在", "Avatar file does not exist", "頭像檔案不存在", "アバターファイルが存在しません", "아바타 파일이 없습니다");
            Register("avatar.unsupported_type", "头像只支持 png、jpg、jpeg", "Avatar only supports png, jpg, and jpeg", "頭像僅支援 png、jpg、jpeg", "アバターは png、jpg、jpeg のみ対応しています", "아바타는 png, jpg, jpeg만 지원합니다");
            Register("avatar.file_too_large", "头像文件过大，最大支持 4MB", "Avatar file is too large. Maximum is 4MB.", "頭像檔案過大，最大支援 4MB", "アバターファイルが大きすぎます。最大4MBです。", "아바타 파일이 너무 큽니다. 최대 4MB입니다");
            Register("avatar.invalid_size", "头像尺寸无效，最大支持 2048x2048", "Avatar dimensions are invalid. Maximum is 2048x2048.", "頭像尺寸無效，最大支援 2048x2048", "アバターサイズが無効です。最大2048x2048です。", "아바타 크기가 올바르지 않습니다. 최대 2048x2048입니다");
            Register("avatar.read_failed", "头像图片读取失败", "Failed to read avatar image", "頭像圖片讀取失敗", "アバター画像の読み込みに失敗しました", "아바타 이미지를 읽지 못했습니다");
            Register("avatar.compress_too_large", "头像压缩后仍太大，请换一张尺寸更小或细节更少的图片", "Avatar is still too large after compression. Please choose a smaller or simpler image.", "頭像壓縮後仍太大，請更換尺寸更小或細節更少的圖片", "圧縮後もアバターが大きすぎます。小さい、または細部の少ない画像を選んでください。", "압축 후에도 아바타가 너무 큽니다. 더 작거나 단순한 이미지를 선택하세요");
            Register("avatar.save_path_invalid", "头像保存路径无效", "Avatar save path is invalid", "頭像儲存路徑無效", "アバター保存先が無効です", "아바타 저장 경로가 올바르지 않습니다");
            Register("avatar.folder_empty", "头像文件夹路径不能为空", "Avatar folder path cannot be empty", "頭像資料夾路徑不能為空", "アバターフォルダのパスは空にできません", "아바타 폴더 경로는 비워둘 수 없습니다");
            Register("avatar.folder_missing", "头像文件夹不存在", "Avatar folder does not exist", "頭像資料夾不存在", "アバターフォルダが存在しません", "아바타 폴더가 없습니다");

            Register("playlist.title", "歌曲列表 {0}/{1}", "Playlist {0}/{1}", "歌曲列表 {0}/{1}", "プレイリスト {0}/{1}", "플레이리스트 {0}/{1}");
            Register("playlist.empty.title", "暂无歌曲", "No Songs", "暫無歌曲", "曲なし", "곡 없음");
            Register("playlist.empty.desc", "在选歌界面点击 Start 可加入歌曲列表", "Press Start on the song select screen to add a song", "在選歌介面點擊 Start 可加入歌曲列表", "選曲画面で Start を押すと曲を追加できます", "곡 선택 화면에서 Start를 눌러 곡을 추가하세요");
            Register("playlist.invalid.title", "无法显示的谱面", "Unreadable Chart", "無法顯示的譜面", "表示できない譜面", "표시할 수 없는 채보");
            Register("playlist.invalid.desc", "该歌曲列表项格式异常", "This playlist item has an invalid format", "此歌曲列表項目格式異常", "このプレイリスト項目の形式が不正です", "이 플레이리스트 항목 형식이 올바르지 않습니다");
            Register("playlist.delete.title", "删除歌曲", "Remove Song", "刪除歌曲", "曲を削除", "곡 제거");
            Register("playlist.delete.confirm", "确认从歌曲列表移除「{0}」吗？", "Remove \"{0}\" from the playlist?", "確認從歌曲列表移除「{0}」嗎？", "「{0}」をプレイリストから削除しますか？", "\"{0}\"을(를) 플레이리스트에서 제거할까요?");
            Register("playlist.item.desc", "谱面: {0}\n难度: {1}\n添加者: {2}", "Chart: {0}\nDifficulty: {1}\nAdded by: {2}", "譜面: {0}\n難度: {1}\n添加者: {2}", "譜面: {0}\n難易度: {1}\n追加者: {2}", "채보: {0}\n난이도: {1}\n추가한 사람: {2}");
            Register("playlist.item.custom.desc", "谱面: {0}\n类型: 自制谱\n难度: {1}\n添加者: {2}\nID: {3}", "Chart: {0}\nType: Custom chart\nDifficulty: {1}\nAdded by: {2}\nID: {3}", "譜面: {0}\n類型: 自訂譜\n難度: {1}\n添加者: {2}\nID: {3}", "譜面: {0}\nタイプ: カスタム譜面\n難易度: {1}\n追加者: {2}\nID: {3}", "채보: {0}\n유형: 커스텀 채보\n난이도: {1}\n추가한 사람: {2}\nID: {3}");
            Register("playlist.removed", "成功移除歌曲列表", "Removed from playlist", "已成功從歌曲列表移除", "プレイリストから削除しました", "플레이리스트에서 제거했습니다");
            Register("playlist.added", "成功加入歌曲列表", "Added to playlist", "已成功加入歌曲列表", "プレイリストに追加しました", "플레이리스트에 추가했습니다");
            Register("playlist.ready", "已准备", "Ready", "已準備", "準備完了", "준비 완료");
            Register("playlist.select_and_ready", "选择并准备", "Select and Ready", "選擇並準備", "選択して準備", "선택하고 준비");
            Register("playlist.variant_locked", "本局已锁定加入列表时的表/里谱，请切换回对应谱面", "This round is locked to the chart variant added to the playlist. Switch back to that variant.", "本局已鎖定加入列表時的表/裏譜，請切換回對應譜面", "このラウンドは追加時の表/裏譜面に固定されています。対応する譜面に戻してください", "이번 라운드는 목록에 추가한 표/리 채보로 고정됩니다. 해당 채보로 다시 전환하세요");
            Register("playlist.wait_ready", "等待准备", "Waiting for ready", "等待準備", "準備待ち", "준비 대기");
            Register("playlist.wait_host", "等待房主选歌", "Waiting for host selection", "等待房主選歌", "ホストの選曲待ち", "방장 선곡 대기");
            Register("playlist.no_chart", "未选择谱面", "No chart selected", "未選擇譜面", "譜面未選択", "채보가 선택되지 않음");
            Register("playlist.remove_own_only", "只能移除自己的谱面", "You can only remove your own chart", "只能移除自己的譜面", "自分の譜面のみ削除できます", "자신의 채보만 제거할 수 있습니다");
            Register("playlist.remove", "移除歌曲列表", "Remove from Playlist", "移除歌曲列表", "プレイリストから削除", "플레이리스트에서 제거");
            Register("playlist.remove_own_first", "先移除自己的谱面", "Remove your own chart first", "先移除自己的譜面", "先に自分の譜面を削除してください", "먼저 자신의 채보를 제거하세요");
            Register("playlist.round_closed", "本轮已封盘", "This round is closed", "本輪已封盤", "このラウンドは締め切られました", "이번 라운드는 마감되었습니다");
            Register("playlist.full", "歌曲列表已满", "Playlist is full", "歌曲列表已滿", "プレイリストが満杯です", "플레이리스트가 가득 찼습니다");
            Register("playlist.add", "加入歌曲列表", "Add to Playlist", "加入歌曲列表", "プレイリストに追加", "플레이리스트에 추가");
            Register("playlist.already_added", "歌曲已在列表中", "Song is already in the playlist", "歌曲已在列表中", "曲は既にプレイリストにあります", "곡이 이미 목록에 있습니다");
            Register("playlist.not_found", "歌曲不在列表中", "Song is not in the playlist", "歌曲不在列表中", "曲はプレイリストにありません", "곡이 목록에 없습니다");
            Register("playlist.chart_selection_banned", "你已被禁止选谱", "You are not allowed to select charts", "你已被禁止選譜", "選曲が禁止されています", "선곡이 금지되었습니다");
            Register("playlist.unsupported", "该谱面暂不支持联机", "This chart does not support multiplayer yet", "該譜面暫不支援聯機", "この譜面はまだマルチプレイに対応していません", "이 채보는 아직 멀티플레이를 지원하지 않습니다");
            Register("playlist.rookie_touhou_spell", "新手模式暂不支持东方特殊谱面，请切换表谱或更换模式", "Rookie mode does not support Touhou Special charts. Choose the normal chart or another mode.", "新手模式暫不支援東方特殊譜面，請切換表譜或更換模式", "ルーキーモードでは東方の特殊譜面は使えません。表譜か別のモードを選んでください", "초보 모드는 동방 특수 채보를 지원하지 않습니다. 일반 채보나 다른 모드를 선택하세요");
            Register("playlist.fearless_difficulty", "无畏模式只能选择大触或隐藏难度", "Fearless mode only allows Master or Hidden difficulty", "無畏模式只能選擇大觸或隱藏難度", "無畏モードでは大触または隠し難易度のみ選択できます", "무외 모드는 마스터 또는 히든 난이도만 선택할 수 있습니다");
            Register("playlist.hidden_by_someone", "有人隐藏了该谱面", "Someone has hidden this chart", "有人隱藏了該譜面", "誰かがこの譜面を非表示にしています", "누군가 이 채보를 숨겼습니다");
            Register("playlist.missing_by_someone", "有人未下载该谱面", "Someone has not downloaded this chart", "有人未下載該譜面", "誰かがこの譜面を未ダウンロードです", "누군가 이 채보를 다운로드하지 않았습니다");
            Register("playlist.add_failed", "添加歌曲失败，错误码 {0}。", "Failed to add song. Error code {0}.", "新增歌曲失敗，錯誤碼 {0}。", "曲の追加に失敗しました。エラーコード {0}。", "곡 추가 실패, 오류 코드 {0}.");
            Register("playlist.lock_unsupported", "歌曲列表包含暂不支持联机的谱面", "Playlist contains charts that do not support multiplayer yet", "歌曲列表包含暫不支援聯機的譜面", "プレイリストに未対応の譜面が含まれています", "플레이리스트에 아직 멀티플레이를 지원하지 않는 채보가 있습니다");
            Register("playlist.start_failed", "开始准备失败，错误码 {0}。", "Failed to start preparation. Error code {0}.", "開始準備失敗，錯誤碼 {0}。", "準備開始に失敗しました。エラーコード {0}。", "준비 시작 실패, 오류 코드 {0}.");
            Register("playlist.playing_blocked", "游戏进行中，不能修改歌曲列表", "Cannot change the playlist while the game is in progress", "遊戲進行中，不能修改歌曲列表", "ゲーム中はプレイリストを変更できません", "게임 진행 중에는 플레이리스트를 수정할 수 없습니다");
            Register("playlist.locked_blocked", "游戏准备或进行中，不能修改歌曲列表", "Cannot change the playlist during preparation or play", "遊戲準備或進行中，不能修改歌曲列表", "準備中またはプレイ中はプレイリストを変更できません", "준비 중이거나 게임 중에는 플레이리스트를 수정할 수 없습니다");
            Register("playlist.tenzi_limit", "天子模式每人最多选择 {0} 首谱面", "Tenzi mode allows up to {0} charts per player", "天子模式每人最多選擇 {0} 首譜面", "天子モードでは1人 {0} 譜面まで選べます", "텐지 모드는 1인당 최대 {0}개의 채보를 선택할 수 있습니다");
            Register("playlist.tenzi_remove_first", "天子模式请先移除自己选择的谱面", "In Tenzi mode, remove your selected chart first", "天子模式請先移除自己選擇的譜面", "天子モードでは先に自分の譜面を削除してください", "텐지 모드에서는 먼저 자신이 선택한 채보를 제거하세요");
            Register("playlist.tenzi_round_remove", "天子模式本轮已封盘，请先移除本轮谱面", "This Tenzi round is closed. Remove this round's chart first.", "天子模式本輪已封盤，請先移除本輪譜面", "天子モードのこのラウンドは締め切られました。先に今回の譜面を削除してください。", "텐지 모드 이번 라운드는 마감되었습니다. 먼저 이번 라운드 채보를 제거하세요");

            Register("mode.normal", "正常模式", "Normal Mode", "正常模式", "通常モード", "일반 모드");
            Register("mode.rookie", "新手模式", "Rookie Mode", "新手模式", "ルーキーモード", "초보 모드");
            Register("mode.fearless", "无畏模式", "Fearless Mode", "無畏模式", "無畏モード", "무외 모드");
            Register("mode.tenzi", "天子模式", "Tenzi Mode", "天子模式", "天子モード", "텐지 모드");
            Register("difficulty.easy", "萌新", "Easy", "萌新", "萌新", "쉬움");
            Register("difficulty.hard", "高手", "Hard", "高手", "高手", "어려움");
            Register("difficulty.master", "大触", "Master", "大觸", "大触", "마스터");
            Register("difficulty.hidden", "隐藏", "Hidden", "隱藏", "隠し", "히든");
            Register("difficulty.spell", "特殊", "Special", "特殊", "特殊", "특수");
            Register("difficulty.unknown", "难度{0}", "Difficulty {0}", "難度{0}", "難易度{0}", "난이도 {0}");

            Register("ready.stop", "停止游戏", "Stop Game", "停止遊戲", "ゲーム停止", "게임 중지");
            Register("ready.equip", "使用推荐", "Use Recommended", "使用推薦", "おすすめ使用", "추천 사용");
            Register("ready.equipped", "已选择", "Selected", "已選擇", "選択済み", "선택됨");
            Register("ready.waiting_chart", "等待歌曲", "Waiting for song", "等待歌曲", "曲待ち", "곡 대기");
            Register("ready.cancel", "取消准备", "Cancel Ready", "取消準備", "準備解除", "준비 취소");
            Register("ready.rookie_jump", "跳转选择难度", "Choose Difficulty", "跳轉選擇難度", "難易度選択へ", "난이도 선택으로");
            Register("ready.button", "准备", "Ready", "準備", "準備", "준비");
            Register("ready.custom_closed", "已关闭自制谱窗口，请重新准备", "Closed the custom chart window. Please ready again.", "已關閉自訂譜視窗，請重新準備", "カスタム譜面ウィンドウを閉じました。もう一度準備してください。", "커스텀 채보 창을 닫았습니다. 다시 준비해 주세요");
            Register("ready.failed", "准备失败：{0}", "Ready failed: {0}", "準備失敗：{0}", "準備失敗：{0}", "준비 실패: {0}");
            Register("ready.custom_closed_jump", "已关闭自制谱窗口，请重新点击跳转", "Closed the custom chart window. Please click jump again.", "已關閉自訂譜視窗，請重新點擊跳轉", "カスタム譜面ウィンドウを閉じました。もう一度ジャンプしてください。", "커스텀 채보 창을 닫았습니다. 다시 이동을 눌러 주세요");
            Register("ready.choose_difficulty", "请选择难度后点击 Play 准备", "Choose a difficulty, then press Play to ready", "請選擇難度後點擊 Play 準備", "難易度を選んでから Play で準備してください", "난이도를 선택한 뒤 Play를 눌러 준비하세요");
            Register("ready.chart_not_found", "未找到本局谱面", "Could not find this round's chart", "未找到本局譜面", "今回の譜面が見つかりません", "이번 라운드 채보를 찾지 못했습니다");
            Register("ready.next", "Next:", "Next:", "下一首:", "Next:", "다음:");
            Register("ready.recommended_config", "推荐配置:", "Recommended:", "推薦配置:", "おすすめ設定:", "추천 설정:");
            Register("ready.owner", "选谱人:", "Selected by:", "選譜人:", "選曲者:", "선곡자:");
            Register("ready.no_recommendation", "暂无推荐", "No recommendation", "暫無推薦", "おすすめなし", "추천 없음");
            Register("ready.girl_fallback", "角色 {0}", "Character {0}", "角色 {0}", "キャラ {0}", "캐릭터 {0}");
            Register("ready.elfin_fallback", "精灵 {0}", "Elfin {0}", "精靈 {0}", "エルフィン {0}", "엘핀 {0}");

            Register("room.watchers", "观众：", "Spectators: ", "觀眾：", "観戦：", "관전자: ");
            Register("room.no_players", "暂无玩家", "No players", "暫無玩家", "プレイヤーなし", "플레이어 없음");
            Register("room.hidden_players_hint", "还有 {0} 位玩家未显示，滚动查看", "{0} more players hidden. Scroll to view.", "還有 {0} 位玩家未顯示，滾動查看", "未表示のプレイヤーがあと {0} 人います。スクロールで表示。", "{0}명의 플레이어가 더 있습니다. 스크롤해서 보세요.");
            Register("room.host.meta", "房主：", "Host: ", "房主：", "ホスト：", "방장: ");
            Register("room.host.badge", "[房主]", "[Host]", "[房主]", "[ホスト]", "[방장]");
            Register("room.players.meta", "人数：", "Players: ", "人數：", "人数：", "인원: ");
            Register("room.player.ready", "已准备", "Ready", "已準備", "準備完了", "준비 완료");
            Register("room.player.not_ready", "未准备", "Not ready", "未準備", "未準備", "준비 안 됨");
            Register("room.player.selecting", "选歌中", "Selecting", "選歌中", "選曲中", "선곡 중");
            Register("room.battle_owner", "选谱人: ", "Selected by: ", "選譜人: ", "選曲者: ", "선곡자: ");
            Register("room.reconnecting_quality", "网络质量差，尝试重连中...", "Poor network quality. Reconnecting...", "網路品質不佳，正在嘗試重新連線...", "通信品質が悪いため再接続中...", "네트워크 상태가 좋지 않아 재연결 중...");

            Register("chat.placeholder", "按\"/\"或点击输入框输入消息", "Press \"/\" or click the input box to chat", "按「/」或點擊輸入框輸入訊息", "「/」または入力欄クリックでメッセージ入力", "\"/\"를 누르거나 입력칸을 클릭해 메시지를 입력하세요");
            Register("chat.mdt.reply_usage", "请输入 /mdt yes 或 /mdt no", "Please enter /mdt yes or /mdt no", "請輸入 /mdt yes 或 /mdt no", "/mdt yes または /mdt no を入力してください", "/mdt yes 또는 /mdt no를 입력하세요");
            Register("chat.mdt.reply_success", "回应成功", "Reply sent", "回應成功", "返信しました", "응답했습니다");
            Register("chat.mdt.host_reply_tip", "房主可以输入/mdt yes/no 来回应同意或拒绝", "The host can type /mdt yes/no to approve or refuse", "房主可以輸入 /mdt yes/no 回應同意或拒絕", "ホストは /mdt yes/no で承認または拒否できます", "방장은 /mdt yes/no로 동의 또는 거절할 수 있습니다");
            Register("chat.player_missing_chart", "未下载该谱面: {0}", "has not downloaded this chart: {0}", "未下載該譜面: {0}", "この譜面を未ダウンロード: {0}", "이 채보를 다운로드하지 않음: {0}");
            Register("chat.playlist_added", "添加了", "added", "新增了", "追加しました", "추가함");
            Register("chat.playlist_removed", "移除了", "removed", "移除了", "削除しました", "제거함");
            Register("chat.player_joined", "加入了房间", "joined the room", "加入了房間", "ルームに参加しました", "방에 참가했습니다");
            Register("chat.player_left", "离开了房间", "left the room", "離開了房間", "ルームを退出しました", "방을 나갔습니다");
            Register("chat.host_started", "房主已开始游戏，请准备", "The host started the game. Please ready up.", "房主已開始遊戲，請準備", "ホストがゲームを開始しました。準備してください。", "방장이 게임을 시작했습니다. 준비해 주세요.");
            Register("chat.all_ready", "所有玩家已准备，开始游戏", "All players are ready. Starting game.", "所有玩家已準備，開始遊戲", "全員準備完了。ゲームを開始します。", "모든 플레이어가 준비했습니다. 게임을 시작합니다.");
            Register("chat.host_stopped", "房主已停止游戏", "The host stopped the game", "房主已停止遊戲", "ホストがゲームを停止しました", "방장이 게임을 중지했습니다");
            Register("chat.finished", "已完成", "finished", "已完成", "完了", "완료");
            Register("chat.cancel_ready", "取消准备", "cancelled ready", "取消準備", "準備解除", "준비 취소");
            Register("chat.chart_not_in_playlist", "未在歌曲列表找到该谱面", "Chart not found in the playlist", "未在歌曲列表找到該譜面", "プレイリストにこの譜面が見つかりません", "플레이리스트에서 이 채보를 찾지 못했습니다");
            Register("chat.system_prefix", "[系统]", "[System]", "[系統]", "[システム]", "[시스템]");

            Register("connection.version_old", "服务器版本过旧，请更换节点或等待服务器更新。", "Server version is too old. Choose another node or wait for an update.", "伺服器版本過舊，請更換節點或等待伺服器更新。", "サーバーバージョンが古すぎます。別のノードを選ぶか更新をお待ちください。", "서버 버전이 너무 오래되었습니다. 다른 서버를 선택하거나 업데이트를 기다려 주세요.");
            Register("connection.reconnecting", "正在重连服务器，请稍候。", "Reconnecting to server. Please wait.", "正在重新連線伺服器，請稍候。", "サーバーに再接続中です。お待ちください。", "서버에 재연결 중입니다. 잠시만 기다려 주세요.");
            Register("connection.not_connected", "未连接服务器。", "Not connected to server.", "未連線伺服器。", "サーバーに接続していません。", "서버에 연결되지 않았습니다.");
            Register("connection.not_logged_in", "尚未登录服务器。", "Not logged in to server.", "尚未登入伺服器。", "サーバーにログインしていません。", "아직 서버에 로그인하지 않았습니다.");
            Register("connection.name_too_long", "请先前往个人信息将名字改短", "Please go to Profile and shorten your name first", "請先前往個人資訊將名字改短", "先にプロフィールで名前を短くしてください", "먼저 프로필에서 이름을 짧게 바꿔 주세요");
            Register("connection.connect_failed_reason", "无法连接到服务器", "Failed to connect to server", "無法連線到伺服器", "サーバーに接続できません", "서버에 연결할 수 없습니다");
            Register("connection.join_failed", "进入服务器失败：{0}", "Failed to enter server: {0}", "進入伺服器失敗：{0}", "サーバー参加失敗：{0}", "서버 참가 실패: {0}");
            Register("connection.interrupted", "连接中断，正在重连...", "Connection interrupted. Reconnecting...", "連線中斷，正在重新連線...", "接続が切れました。再接続中...", "연결이 끊어져 재연결 중...");
            Register("connection.reconnected", "已重新连接服务器", "Reconnected to server", "已重新連線伺服器", "サーバーに再接続しました", "서버에 다시 연결되었습니다");
            Register("connection.reconnect_failed", "重连失败，请重新选择节点", "Reconnect failed. Please choose a server again.", "重新連線失敗，請重新選擇節點", "再接続に失敗しました。サーバーを選び直してください。", "재연결 실패. 서버를 다시 선택해 주세요.");
            Register("connection.reconnecting.label", "{0} 重连中...", "{0} reconnecting...", "{0} 重新連線中...", "{0} 再接続中...", "{0} 재연결 중...");

            Register("navigation.playlist", "歌曲列表", "Playlist", "歌曲列表", "プレイリスト", "플레이리스트");
            Register("navigation.start_game", "开始游戏", "Start Game", "開始遊戲", "ゲーム開始", "게임 시작");
            Register("navigation.host_only_start", "只有房主可以开始游戏哦", "Only the host can start the game", "只有房主可以開始遊戲喔", "ゲーム開始はホストのみ可能です", "방장만 게임을 시작할 수 있습니다");
            Register("navigation.start_confirm", "确认开始多人准备吗？", "Start multiplayer preparation?", "確認開始多人準備嗎？", "マルチプレイの準備を開始しますか？", "멀티플레이 준비를 시작할까요?");
            Register("navigation.custom_closed_start", "已关闭自制谱窗口，请重新开始准备", "Closed the custom chart window. Please start preparation again.", "已關閉自訂譜視窗，請重新開始準備", "カスタム譜面ウィンドウを閉じました。もう一度準備を開始してください。", "커스텀 채보 창을 닫았습니다. 다시 준비를 시작해 주세요");
            Register("navigation.start_failed", "开始失败：{0}", "Start failed: {0}", "開始失敗：{0}", "開始失敗：{0}", "시작 실패: {0}");
            Register("navigation.start_requesting", "正在开始准备，请稍候", "Starting preparation. Please wait.", "正在開始準備，請稍候", "準備開始中です。少しお待ちください", "준비를 시작하는 중입니다. 잠시만 기다려 주세요");
            Register("navigation.start_locked", "已经进入准备阶段，不能重复开始", "Preparation has already started.", "已經進入準備階段，不能重複開始", "すでに準備段階です", "이미 준비 단계입니다");
            Register("navigation.start_playing", "游戏进行中，不能开始准备", "Game is already in progress.", "遊戲進行中，不能開始準備", "ゲーム中は準備を開始できません", "게임 진행 중에는 준비를 시작할 수 없습니다");
            Register("navigation.start_blocked", "当前不能开始准备", "Cannot start preparation right now.", "目前不能開始準備", "現在は準備を開始できません", "지금은 준비를 시작할 수 없습니다");

            Register("patch.store_disabled", "多人房间中不能打开商店！", "Cannot open the shop in a multiplayer room!", "多人房間中不能打開商店！", "マルチルーム中はショップを開けません！", "멀티플레이 방에서는 상점을 열 수 없습니다!");
            Register("patch.song_hide_disabled", "不能在游戏房间中隐藏或解除隐藏谱面！", "Cannot hide or unhide charts in a game room!", "不能在遊戲房間中隱藏或解除隱藏譜面！", "ゲームルーム中は譜面の表示/非表示を変更できません！", "게임 방에서는 채보 숨김 상태를 변경할 수 없습니다!");
            Register("battle.wait_others", "等待其他人完成游戏中...", "Waiting for others to finish...", "正在等待其他人完成遊戲...", "他のプレイヤーの完了待ち...", "다른 플레이어의 완료를 기다리는 중...");
            Register("battle.continue", "继续", "Continue", "繼續", "続行", "계속");
            Register("battle.waiting", "等待中", "Waiting", "等待中", "待機中", "대기 중");
            Register("battle.leaderboard", "排行榜", "Leaderboard", "排行榜", "ランキング", "순위표");
            Register("battle.lost_fc", "失去FC!", "Lost FC!", "失去 FC！", "FCロスト！", "FC 실패!");
            Register("battle.lost_ap", "失去AP!", "Lost AP!", "失去 AP！", "APロスト！", "AP 실패!");
            Register("battle.down", "倒下", "Down", "倒下", "ダウン", "다운");
            Register("battle.missed", "Missed!", "Missed!", "Missed!", "Missed!", "Missed!");
            Register("cloud.syncing", "同步中...", "Syncing...", "同步中...", "同期中...", "동기화 중...");

            Register("missing_chart.local", "谱面已在本地: {0}", "Chart is already local: {0}", "譜面已在本機: {0}", "譜面は既にローカルにあります: {0}", "채보가 이미 로컬에 있습니다: {0}");
            Register("missing_chart.imported", "已从候选区导入谱面: {0}", "Imported chart from candidates: {0}", "已從候選區匯入譜面: {0}", "候補から譜面をインポートしました: {0}", "후보에서 채보를 가져왔습니다: {0}");
            Register("missing_chart.multiple_search", "候选区有多个可能结果，已打开搜索并复制: {0}", "Multiple candidates found. Opened search and copied: {0}", "候選區有多個可能結果，已開啟搜尋並複製: {0}", "候補が複数あります。検索を開いてコピーしました: {0}", "후보가 여러 개 있습니다. 검색을 열고 복사했습니다: {0}");
            Register("missing_chart.multiple_copy", "候选区有多个可能结果，已复制谱面名: {0}", "Multiple candidates found. Copied chart name: {0}", "候選區有多個可能結果，已複製譜面名: {0}", "候補が複数あります。譜面名をコピーしました: {0}", "후보가 여러 개 있습니다. 채보 이름을 복사했습니다: {0}");
            Register("missing_chart.import_failed_search", "自动导入失败，已打开搜索并复制: {0}", "Auto import failed. Opened search and copied: {0}", "自動匯入失敗，已開啟搜尋並複製: {0}", "自動インポートに失敗しました。検索を開いてコピーしました: {0}", "자동 가져오기 실패. 검색을 열고 복사했습니다: {0}");
            Register("missing_chart.import_failed_copy", "自动导入失败，已复制谱面名: {0}", "Auto import failed. Copied chart name: {0}", "自動匯入失敗，已複製譜面名: {0}", "自動インポートに失敗しました。譜面名をコピーしました: {0}", "자동 가져오기 실패. 채보 이름을 복사했습니다: {0}");
            Register("missing_chart.not_found_search", "候选区未找到，已打开搜索并复制: {0}", "No candidate found. Opened search and copied: {0}", "候選區未找到，已開啟搜尋並複製: {0}", "候補が見つかりません。検索を開いてコピーしました: {0}", "후보를 찾지 못했습니다. 검색을 열고 복사했습니다: {0}");
            Register("missing_chart.not_found_copy", "候选区未找到，已复制谱面名: {0}", "No candidate found. Copied chart name: {0}", "候選區未找到，已複製譜面名: {0}", "候補が見つかりません。譜面名をコピーしました: {0}", "후보를 찾지 못했습니다. 채보 이름을 복사했습니다: {0}");
            Register("missing_chart.musedashtool_opened", "已在喵斯兔打开全局搜索，请切屏查看结果", "Opened MuseDashTOOL global search. Please switch to it for results.", "已在喵斯兔開啟全域搜尋，請切換視窗查看結果", "MuseDashTOOL の全体検索を開きました。結果を確認してください", "MuseDashTOOL 전역 검색을 열었습니다. 창을 전환해 결과를 확인하세요.");
            Register("missing_chart.musedashtool_not_running", "请先打开喵斯兔，再点击缺谱播报", "Please open MuseDashTOOL first, then click the missing-chart notice again.", "請先開啟喵斯兔，再點擊缺譜播報", "先に MuseDashTOOL を開いてから、譜面不足通知をもう一度クリックしてください", "먼저 MuseDashTOOL을 연 뒤 누락 채보 알림을 다시 클릭하세요.");
            Register("missing_chart.download_started", "喵斯兔正在下载缺失谱面: {0}", "MuseDashTOOL is downloading the missing chart: {0}", "喵斯兔正在下載缺失譜面: {0}", "MuseDashTOOL が不足譜面をダウンロード中: {0}", "MuseDashTOOL에서 누락 채보 다운로드 중: {0}");
            Register("missing_chart.download_completed", "缺失谱面已下载并通过校验: {0}", "Missing chart downloaded and verified: {0}", "缺失譜面已下載並通過校驗: {0}", "不足譜面のダウンロードと検証が完了: {0}", "누락 채보 다운로드 및 검증 완료: {0}");
            Register("missing_chart.download_failed", "缺失谱面自动下载失败: {0}", "Missing chart auto download failed: {0}", "缺失譜面自動下載失敗: {0}", "不足譜面の自動ダウンロードに失敗: {0}", "누락 채보 자동 다운로드 실패: {0}");
            Register("missing_chart.download_failed_reason", "缺失谱面自动下载失败: {0}\n{1}", "Missing chart auto download failed: {0}\n{1}", "缺失譜面自動下載失敗: {0}\n{1}", "不足譜面の自動ダウンロードに失敗: {0}\n{1}", "누락 채보 자동 다운로드 실패: {0}\n{1}");

            Register("prepare.update_playlist", "更新歌曲列表中...", "Updating playlist...", "正在更新歌曲列表...", "プレイリスト更新中...", "플레이리스트 업데이트 중...");
            Register("prepare.select_chart_first", "请先选择本局谱面", "Please select this round's chart first", "請先選擇本局譜面", "先に今回の譜面を選択してください", "먼저 이번 라운드 채보를 선택하세요");
            Register("prepare.valid_difficulty", "请选择有效难度", "Please choose a valid difficulty", "請選擇有效難度", "有効な難易度を選択してください", "올바른 난이도를 선택하세요");
            Register("prepare.selected_ready", "已选择并准备", "Selected and ready", "已選擇並準備", "選択して準備完了", "선택하고 준비 완료");
            Register("prepare.play", "PLAY!", "PLAY!", "PLAY!", "PLAY!", "PLAY!");

            Register("battle.start.selection_not_ready", "本地选歌界面未准备好，未能进入联机游戏。", "Local song selection is not ready. Could not enter multiplayer battle.", "本機選歌介面尚未準備好，未能進入聯機遊戲。", "ローカル選曲画面の準備ができていないため、マルチプレイに入れませんでした。", "로컬 곡 선택 화면이 준비되지 않아 멀티플레이 게임에 들어가지 못했습니다.");
            Register("battle.start.invalid_entry", "联机歌曲信息无效，未能进入游戏。", "Multiplayer song info is invalid. Could not enter battle.", "聯機歌曲資訊無效，未能進入遊戲。", "マルチプレイ曲情報が無効なため、ゲームに入れませんでした。", "멀티플레이 곡 정보가 올바르지 않아 게임에 들어가지 못했습니다.");
            Register("battle.start.chart_missing", "本地缺少谱面 {0}，未能进入联机游戏。", "Local chart {0} is missing. Could not enter multiplayer battle.", "本機缺少譜面 {0}，未能進入聯機遊戲。", "ローカルに譜面 {0} がないため、マルチプレイに入れませんでした。", "로컬에 채보 {0}이(가) 없어 멀티플레이 게임에 들어가지 못했습니다.");
            Register("battle.start.difficulty_missing", "本局未选择难度，未能进入联机游戏。", "No difficulty was selected for this round. Could not enter battle.", "本局未選擇難度，未能進入聯機遊戲。", "今回の難易度が選択されていないため、ゲームに入れませんでした。", "이번 라운드 난이도가 선택되지 않아 게임에 들어가지 못했습니다.");
            Register("battle.start.custom_closed_retry", "已关闭自制谱窗口，正在重新尝试进入多人游戏", "Closed the custom chart window. Retrying multiplayer battle.", "已關閉自訂譜視窗，正在重新嘗試進入多人遊戲", "カスタム譜面ウィンドウを閉じました。マルチプレイ開始を再試行中です。", "커스텀 채보 창을 닫았습니다. 멀티플레이 게임 입장을 다시 시도합니다");

            Register("player.profile.fallback_title", "玩家资料", "Player Profile", "玩家資料", "プレイヤープロフィール", "플레이어 프로필");
            Register("player.profile.title_label", "头衔", "Title", "頭銜", "称号", "칭호");
            Register("player.profile.status_label", "状态", "Status", "狀態", "状態", "상태");
            Register("player.profile.color_label", "名字颜色", "Name Color", "名字顏色", "名前色", "이름 색상");
            Register("player.profile.entrance_label", "入场提示", "Entrance Message", "入場提示", "入室メッセージ", "입장 메시지");
            Register("player.profile.bio_label", "个人介绍", "Bio", "個人介紹", "自己紹介", "소개");
            Register("player.profile.no_title", "暂无头衔", "No title", "暫無頭銜", "称号なし", "칭호 없음");
            Register("player.profile.no_bio", "暂无介绍", "No bio", "暫無介紹", "自己紹介なし", "소개 없음");
            Register("player.profile.no_entrance", "暂无入场提示", "No entrance message", "暫無入場提示", "入室メッセージなし", "입장 메시지 없음");
            Register("player.status.online", "在线", "Online", "在線", "オンライン", "온라인");
            Register("player.status.in_lobby", "房间中", "In room", "房間中", "ルーム内", "방에 있음");
            Register("player.status.in_battle", "游戏中", "In game", "遊戲中", "ゲーム中", "게임 중");
            Register("player.bio.empty", "暂无简介", "No bio", "暫無簡介", "自己紹介なし", "소개 없음");

            Register("tenzi.title", "天子抽曲", "Tenzi Draw", "天子抽曲", "天子抽選", "텐지 추첨");
            Register("tenzi.state.waiting", "等待抽取", "Waiting", "等待抽取", "抽選待ち", "추첨 대기");
            Register("tenzi.state.selected", "已抽中", "Selected", "已抽中", "当選", "선택됨");
            Register("tenzi.state.drawing", "抽取中", "Drawing", "抽取中", "抽選中", "추첨 중");
            Register("tenzi.desc", "状态: {0}\n谱面: {1}\n难度: {2}\n添加者: {3}", "Status: {0}\nChart: {1}\nDifficulty: {2}\nAdded by: {3}", "狀態: {0}\n譜面: {1}\n難度: {2}\n添加者: {3}", "状態: {0}\n譜面: {1}\n難易度: {2}\n追加者: {3}", "상태: {0}\n채보: {1}\n난이도: {2}\n추가한 사람: {3}");

            Register("social.request.title", "好友请求", "Friend Request", "好友請求", "フレンド申請", "친구 요청");
            Register("social.request.confirm", "{0} 想添加你为好友", "{0} wants to add you as a friend", "{0} 想新增你為好友", "{0} があなたをフレンドに追加したがっています", "{0}님이 친구로 추가하려고 합니다");
            Register("social.notify.request", "{0} 向你发送了好友请求", "{0} sent you a friend request", "{0} 向你發送了好友請求", "{0} からフレンド申請が届きました", "{0}님이 친구 요청을 보냈습니다");
            Register("social.notify.accepted", "{0} 已成为你的好友", "{0} is now your friend", "{0} 已成為你的好友", "{0} がフレンドになりました", "{0}님이 친구가 되었습니다");
            Register("social.notify.removed", "{0} 已删除好友关系", "{0} removed the friendship", "{0} 已刪除好友關係", "{0} がフレンド関係を削除しました", "{0}님이 친구 관계를 삭제했습니다");
            Register("social.notify.cancelled", "{0} 已取消好友请求", "{0} cancelled the friend request", "{0} 已取消好友請求", "{0} がフレンド申請を取り消しました", "{0}님이 친구 요청을 취소했습니다");
            Register("social.notify.declined", "{0} 拒绝了你的好友请求", "{0} declined your friend request", "{0} 拒絕了你的好友請求", "{0} がフレンド申請を拒否しました", "{0}님이 친구 요청을 거절했습니다");

            Register("settlement.title", "结算", "Settlement", "結算", "リザルト", "정산");
            Register("settlement.subtitle", "结算结果", "Settlement Result", "結算結果", "リザルト結果", "정산 결과");
            Register("settlement.award.dragon_coin", "龙币", "Dragon Coin", "龍幣", "ドラゴンコイン", "드래곤 코인");
            Register("settlement.award.combo", "最能连之人", "Combo Master", "最能連之人", "コンボマスター", "콤보 마스터");
            Register("settlement.award.perfect", "P佬", "Perfect Pro", "P 佬", "Perfect Pro", "퍼펙트 장인");
            Register("settlement.award.sleepwalk", "真·梦游少女", "True Sleepwalker", "真・夢遊少女", "真・夢遊少女", "진정한 몽유병 소녀");
            Register("settlement.played_charts", "游玩曲目", "Played Charts", "遊玩曲目", "プレイ曲", "플레이한 곡");
            Register("settlement.total_duration", "本次游玩时长", "Total Play Time", "本次遊玩時長", "今回のプレイ時間", "이번 플레이 시간");
            Register("settlement.empty", "暂无", "None", "暫無", "なし", "없음");
            Register("settlement.confirm", "确认", "Confirm", "確認", "確認", "확인");
            Register("settlement.name_separator", "，", ", ", "，", "、", ", ");

            Register("version.outdated", "当前Ensemble版本过低，请前往喵斯兔群聊更新最新模组\n点击确认自动跳转至更新网站\n<color=#ff3333>不更新无法进行游戏</color>", "Your Ensemble version is too old. Please update the mod from the Museto group.\nClick Confirm to open the update site.\n<color=#ff3333>You cannot play without updating.</color>", "目前 Ensemble 版本過低，請前往喵斯兔群聊更新最新模組\n點擊確認會自動跳轉至更新網站\n<color=#ff3333>不更新無法進行遊戲</color>", "現在の Ensemble バージョンが古すぎます。Museto グループで最新 Mod に更新してください。\n確認を押すと更新サイトを開きます。\n<color=#ff3333>更新しないとプレイできません</color>", "현재 Ensemble 버전이 너무 낮습니다. Museto 그룹에서 최신 모드를 업데이트하세요.\n확인을 누르면 업데이트 사이트로 이동합니다.\n<color=#ff3333>업데이트하지 않으면 플레이할 수 없습니다</color>");
            Register("lobby.kicked", "你已被移出房间", "You have been removed from the room", "你已被移出房間", "ルームから退出させられました", "방에서 내보내졌습니다");
            Register("config.custom_server.default_name", "节点{0}", "Server {0}", "節點{0}", "ノード{0}", "서버 {0}");
            Register("chart.unknown", "未知谱面", "Unknown Chart", "未知譜面", "不明な譜面", "알 수 없는 채보");
            Register("chart.unknown_with_difficulty", "未知谱面 {0}", "Unknown Chart {0}", "未知譜面 {0}", "不明な譜面 {0}", "알 수 없는 채보 {0}");
            Register("account.pero_uid_missing", "无法获取 PeroUid，请先登录游戏账号。", "PeroUid is not available. Please log in to the game account first.", "無法取得 PeroUid，請先登入遊戲帳號。", "PeroUid を取得できません。先にゲームアカウントへログインしてください。", "PeroUid를 가져올 수 없습니다. 먼저 게임 계정에 로그인하세요.");
            Register("account.not_logged_in", "尚未登录。", "Not logged in.", "尚未登入。", "未ログインです。", "아직 로그인하지 않았습니다.");
        }
    }
}
