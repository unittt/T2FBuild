#if T2FBUILD_HAS_WECHAT
using System;
using System.IO;
using UnityEngine;
using WeChatWASM;

namespace T2FBuild.Editor.Platforms.WeChat
{
    public class RunWeChatExportStep : IBuildStep
    {
        public string Name => "Run WeChat Export (DoExport)";

        public void Execute(BuildContext ctx)
        {
            Debug.Log("[T2FBuild] Invoking WXConvertCore.DoExport(buildWebGL: true)...");
            var result = WXConvertCore.DoExport(true);
            if (result != WXConvertCore.WXExportError.SUCCEED)
            {
                throw new Exception($"[T2FBuild] WeChat export failed: {result}");
            }

            var dstAbsolute = ctx.Extras.TryGetValue("wechat.dstAbsolute", out var d) ? d as string : null;
            if (string.IsNullOrEmpty(dstAbsolute))
            {
                throw new InvalidOperationException(
                    "[T2FBuild] wechat.dstAbsolute missing from BuildContext.Extras. " +
                    "RunWeChatExportStep must run after ConfigureWeChatProjectStep.");
            }

            var miniGameDir = Path.Combine(dstAbsolute, "minigame");
            if (!Directory.Exists(miniGameDir))
            {
                throw new InvalidOperationException($"[T2FBuild] Expected MiniGame output not found: {miniGameDir}");
            }

            ctx.Extras["wechat.miniGameDir"] = miniGameDir;
            Debug.Log($"[T2FBuild] WeChat export complete: {miniGameDir}");
        }
    }
}
#endif
