#if T2FBUILD_HAS_WECHAT
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using WeChatWASM;

namespace T2FBuild.Editor.Platforms.WeChat
{
    public class ConfigureWeChatProjectStep : IBuildStep
    {
        public string Name => "Configure WeChat Project";

        public void Execute(BuildContext ctx)
        {
            var settings = T2FBuildSettings.instance;

            if (string.IsNullOrEmpty(settings.wechatAppId))
            {
                throw new InvalidOperationException(
                    "[T2FBuild] WeChat AppId is empty. Configure it in Edit > Project Settings > T2FBuild > WeChat MiniGame.");
            }

            var config = UnityUtil.GetEditorConf();
            if (config == null)
            {
                throw new InvalidOperationException(
                    "[T2FBuild] UnityUtil.GetEditorConf() returned null. " +
                    "Open Tools > WeChat > 转换小程序 once so the SDK initializes MiniGameConfig.asset.");
            }

            var relativeDst = ctx.OutputRoot.Replace('\\', '/').TrimEnd('/');
            var absoluteDst = Path.GetFullPath(relativeDst).Replace('\\', '/');
            if (!Directory.Exists(absoluteDst))
            {
                Directory.CreateDirectory(absoluteDst);
            }

            var relativePrefix = ResolveTemplate(settings.wechatFirstPackageRemotePrefixTemplate, ctx);
            var baseUrl = ResolveBaseUrl(settings);
            var cdnFullUrl = CombineUrl(baseUrl, relativePrefix);

            if (string.IsNullOrEmpty(baseUrl))
            {
                Debug.LogWarning(
                    "[T2FBuild] WeChat CDN base URL is empty: neither WeChat MiniGame > CDN Base URL nor " +
                    "Tencent COS > Bucket/Region are set. MiniGameConfig.CDN will only contain the relative " +
                    "prefix, which will likely break runtime resource loading. Fill at least Tencent COS " +
                    "Bucket+Region in Project Settings > T2FBuild.");
            }

            config.ProjectConf.Appid = settings.wechatAppId;
            config.ProjectConf.projectName = string.IsNullOrEmpty(config.ProjectConf.projectName)
                ? Application.productName
                : config.ProjectConf.projectName;
            config.ProjectConf.relativeDST = relativeDst;
            config.ProjectConf.DST = absoluteDst;
            config.ProjectConf.CDN = cdnFullUrl;
            config.CompileOptions.CustomNodePath = settings.wechatCustomNodePath ?? string.Empty;

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            ctx.Extras["wechat.dstAbsolute"] = absoluteDst;
            ctx.Extras["wechat.firstPackageRemotePrefix"] = relativePrefix;

            Debug.Log($"[T2FBuild] WeChat config: AppId={config.ProjectConf.Appid}, DST={absoluteDst}, CDN={cdnFullUrl}");
        }

        static string ResolveTemplate(string template, BuildContext ctx)
        {
            var settings = T2FBuildSettings.instance;
            var projectId = settings != null ? settings.projectId ?? string.Empty : string.Empty;
            var projectWithSlash = string.IsNullOrEmpty(projectId) ? string.Empty : projectId + "/";
            var profile = ctx.Profile ?? string.Empty;
            var profileSuffix = string.IsNullOrEmpty(profile) ? string.Empty : "_" + profile;

            var prefix = template
                .Replace("{project}/", projectWithSlash)
                .Replace("{project}", projectId)
                .Replace("{target}", ctx.Target.ToString())
                .Replace("{profileSuffix}", profileSuffix)
                .Replace("{profile}", profile)
                .Replace("{env}", ctx.Env)
                .Replace("{version}", ctx.Version);
            return prefix.EndsWith("/") ? prefix : prefix + "/";
        }

        static string ResolveBaseUrl(T2FBuildSettings settings)
        {
            if (!string.IsNullOrEmpty(settings.wechatCdnBaseUrl)) return settings.wechatCdnBaseUrl;
            if (!string.IsNullOrEmpty(settings.tencentCosBucket) && !string.IsNullOrEmpty(settings.tencentCosRegion))
            {
                return $"https://{settings.tencentCosBucket}.cos.{settings.tencentCosRegion}.myqcloud.com/";
            }
            return string.Empty;
        }

        static string CombineUrl(string baseUrl, string relativePrefix)
        {
            if (string.IsNullOrEmpty(baseUrl)) return relativePrefix;
            var trimmedBase = baseUrl.TrimEnd('/');
            var trimmedRel = relativePrefix.TrimStart('/');
            return $"{trimmedBase}/{trimmedRel}";
        }
    }
}
#endif
