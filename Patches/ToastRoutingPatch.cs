using HarmonyLib;
using Il2CppAssets.Scripts.UI.Controls;

namespace MDEN.Patches
{
    // 拦截因使用原生兑换码输入框导致的错误提示
    [HarmonyPatch(typeof(ShowText), nameof(ShowText.ShowInfo))]
    [HarmonyPriority(Priority.First)]
    internal static class ToastRoutingPatch
    {
        private static bool Prefix(string info)
        {
            if (info == null) return true;

            // 使用 Contains 进行宽泛匹配，防止因为空格或者标点符号不同导致匹配失败
            // 过滤掉所有语言下的“兑换码不存在”或者“不能为空”的提示
            if (info == "兑换码不存在哦（T^T）" || 
                info == "兌換碼不存在哦（T^T）" || 
                info == "Redeem Code doesn't exist（T^T）" || 
                info == "コードが存在しません。（T^T）" || 
                info == "존재하지 않는 교환 코드에요.（T^T）" ||
                info == "兑换码不能为空" ||
                info == "兌換碼不能為空" ||
                info == "Redeem Code cannot be empty" ||
                info == "コードを空にすることはできません" ||
                info == "교환 코드를 비워둘 수 없습니다")
            {
                return false;
            }

            return true;
        }
    }
}
