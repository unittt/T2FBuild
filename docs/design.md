# T2FBuild 自动化打包框架设计

> 状态：设计已定稿（v0.1）
> 目标：独立 UPM Git 仓库（GitHub 托管），可被任意 Unity 项目引用
> Unity 版本：2022.3 LTS（首期），Unity 6 兼容但首期不验证
> 最后更新：2026-05-18

---

## 1. 目标与范围

提供一个**独立、可复用**的 Unity 自动化打包框架，满足以下需求：

- **多平台**：Android、iOS、WebGL、微信小游戏（后续可扩展抖音 / 小米等其他小游戏平台）
- **CI 集成**：先支持 GitHub Actions（基于 GameCI），后续可扩展自建 Jenkins / 腾讯云 CODING
- **AB 系统抽象**：当前支持 Addressables，后期接入 YooAsset
- **云端上传抽象**：当前支持腾讯云 COS，后期可扩展其他对象存储
- **可独立演进**：与业务项目解耦，类似 `T2FCore` 系列作为基础设施

---

## 2. 已确认的设计决策

| 项 | 决策 | 说明 |
|----|------|------|
| 命名 | **T2FBuild** | 遵循 T2F 系列命名约定 |
| 包发布方式 | **独立 Git 仓库**（UPM Git Package） | 与 `T2FCore` 系列一致，可被多项目复用 |
| 仓库地址 | **https://github.com/unittt/T2FBuild** | 已建空仓库 |
| Unity 版本下限 | **2022.3 LTS** | 对齐 BounceBlast 当前版本 |
| Unity 6（6000.x） | **API 层兼容，但首期不在 CI 矩阵中验证** | 后期补测试 |
| CI 平台（首期） | **GitHub Actions** | 基于 [GameCI](https://game.ci) |
| AB 系统（首期） | **Addressables** | 通过 `IAssetBundleProvider` 抽象 |
| AB 系统（后期） | **YooAsset** | 新增 `Providers/YooAsset/`，不改框架核心 |
| 上传目标（首期） | **腾讯云 COS** | 通过 `IAssetBundleUploader` 抽象 |
| Runtime 代码 | **不包含** | 框架仅在 Editor 工作；AB 加载由 `T2FResource` 处理 |
| 扩展模式 | **特性标记 + 反射注册**（符合通用编码原则 §3） | 新增 Provider / Uploader 不修改已有代码 |

---

## 3. 整体架构

```
┌─────────────────────────────────────────────────────────────┐
│  GitHub Actions (workflow 入口 + 矩阵触发)                    │
│    ├── android.yml   → ubuntu-latest  (GameCI)              │
│    ├── ios.yml       → macos-latest   (GameCI)              │
│    ├── webgl.yml     → ubuntu-latest  (GameCI)              │
│    └── wechat.yml    → ubuntu-latest  (GameCI + 微信工具)     │
└────────────────────┬────────────────────────────────────────┘
                     │ 调用 Unity batchmode -executeMethod
                     ▼
┌─────────────────────────────────────────────────────────────┐
│  Unity Editor (T2FBuild package)                            │
│    ├── BuildEntry            — CLI 入口（静态方法）           │
│    ├── BuildPipeline         — 流水线驱动                    │
│    ├── IBuildStep            — 步骤接口                      │
│    ├── IPlatformBuilder      — 平台构建器                    │
│    ├── IAssetBundleProvider  — AB 抽象（Addressables/YooAsset)│
│    └── IAssetBundleUploader  — 上传抽象（COS/...)            │
└────────────────────┬────────────────────────────────────────┘
                     │ AB 产物 + 平台产物
                     ▼
┌─────────────────────────────────────────────────────────────┐
│  上传层（CI 侧 Python 脚本 + Unity 侧 SDK 双路径）             │
│    ├── tools/upload-cos.py（生产用，CI 调用）                 │
│    └── TencentCosUploader.cs（开发用，BuildWindow 一键上传）   │
│        → 腾讯云 COS（Bucket + CDN）                          │
└─────────────────────────────────────────────────────────────┘
```

---

## 4. 包目录结构

```
T2FBuild/                                # 独立 Git 仓库根
├── package.json                         # UPM 包定义
├── README.md
├── CHANGELOG.md
├── Runtime/
│   └── T2FBuild.Runtime.asmdef          # 几乎为空（仅常量）
└── Editor/
    ├── T2FBuild.Editor.asmdef
    ├── Core/
    │   ├── BuildContext.cs              # 流水线上下文
    │   ├── BuildRunner.cs               # 驱动器（避免与 UnityEditor.BuildPipeline 冲突）
    │   ├── IBuildStep.cs
    │   ├── BuildEntry.cs                # CLI 入口
    │   └── RegistryScanner.cs           # 共用反射扫描助手（三个注册器复用）
    ├── Steps/                            # 跨平台共用构建步骤
    │   ├── SwitchPlatformStep.cs
    │   ├── ApplyVersionStep.cs
    │   ├── BuildAssetBundleStep.cs
    │   ├── GenerateUploadManifestStep.cs
    │   ├── UploadAssetBundleStep.cs
    │   └── BuildPlayerStep.cs
    ├── Platforms/
    │   ├── IPlatformBuilder.cs
    │   ├── PlatformBuilderAttribute.cs
    │   ├── PlatformBuilderRegistry.cs
    │   ├── AndroidBuilder.cs
    │   ├── IOSBuilder.cs
    │   ├── WebGLBuilder.cs
    │   └── WeChatBuilder.cs
    ├── AssetBundle/
    │   ├── IAssetBundleProvider.cs
    │   ├── AssetBundleBuildResult.cs
    │   ├── AssetBundleProviderAttribute.cs
    │   ├── AssetBundleProviderRegistry.cs
    │   ├── IAssetBundleUploader.cs
    │   ├── UploadRequest.cs
    │   ├── UploadResult.cs
    │   ├── UploadMode.cs
    │   ├── UploadManifest.cs            # 共享协议：upload-manifest.json schema
    │   ├── AssetBundleUploaderAttribute.cs
    │   ├── AssetBundleUploaderRegistry.cs
    │   ├── Providers/
    │   │   └── Addressables/            # versionDefines 隔离
    │   │       ├── AddressablesProvider.cs
    │   │       └── T2FBuild.Editor.Addressables.asmdef
    │   └── Uploaders/
    │       └── TencentCos/              # 子 asmdef：调用 upload-cos.py
    │           ├── TencentCosUploader.cs
    │           └── T2FBuild.Editor.TencentCos.asmdef
    ├── Config/
    │   ├── T2FBuildSettings.cs          # ScriptableSingleton（ProjectSettings/T2FBuildSettings.asset）
    │   └── T2FBuildSettingsProvider.cs   # 接入 Edit > Project Settings > T2FBuild
    ├── CI/
    │   └── Templates~/                  # `~` 结尾不被 Unity 导入
    │       ├── workflows/
    │       │   ├── build.yml
    │       │   ├── android.yml
    │       │   ├── ios.yml
    │       │   ├── webgl.yml           # 首期已实现：构建 + AB + Player 双上传
    │       │   └── wechat.yml
    │       └── tools/
    │           ├── upload-cos.py       # 双模式：--manifest 或 --dir，自动设置 Content-Type/Encoding
    │           ├── requirements.txt
    │           └── README.md
    └── Window/
    └── Window/
        ├── CITemplateInstallerWindow.cs  # 一键复制 CI 模板到项目（多选）
        └── BuildWindow.cs                # 开发期一键打包窗口（后期）
```

**为什么 Runtime 几乎为空**：框架只在 Editor 工作，玩家运行时不需要任何代码；AB 加载由业务侧的 `T2FResource` 处理，避免重复职责。

**为什么 Templates 用 `~` 后缀**：Unity 不会扫描带 `~` 后缀的目录，避免模板里的 yml/py 被当成资源处理。`CITemplateInstallerWindow`（`Window > T2FBuild > CI Template Installer`）扫描该目录列出可安装的 workflow，开发者勾选后一键复制到项目的 `.github/workflows/` 和 `tools/`。

---

## 5. 核心抽象

### 5.1 BuildContext

贯穿整条流水线的上下文，所有步骤共享。

```csharp
public class BuildContext
{
    public BuildTarget Target;
    public string Profile;          // wechat / null（区分同 target 的子变体）
    public string Version;          // BUILD_VERSION
    public int BuildNumber;
    public string Env;              // dev / staging / prod
    public string OutputRoot;       // Build/<target>/
    public BuildConfig Config;      // 项目级配置
    public Dictionary<string, object> Extras;

    public static BuildContext FromEnvironment(BuildTarget target, string profile = null);
}
```

### 5.2 IBuildStep + BuildPipeline

```csharp
public interface IBuildStep
{
    string Name { get; }
    void Execute(BuildContext ctx);
}

public static class BuildPipeline   // 实际命名为 BuildRunner，避免与 UnityEditor.BuildPipeline 冲突
{
    public static void Execute(BuildContext ctx)
    {
        var builder = PlatformBuilderRegistry.Get(ctx.Target, ctx.Profile);
        foreach (var step in builder.GetSteps(ctx))
        {
            Log($"[T2FBuild] >>> {step.Name}");
            step.Execute(ctx);
        }
    }
}
```

平台 Builder = 步骤的有序组合，例如：

```csharp
public class AndroidBuilder : IPlatformBuilder
{
    public IEnumerable<IBuildStep> GetSteps(BuildContext ctx) => new IBuildStep[]
    {
        new ValidateConfigStep(),
        new SwitchPlatformStep(),
        new ApplyVersionStep(),
        new BuildAssetBundleStep(),     // 调用 IAssetBundleProvider
        new UploadAssetBundleStep(),    // 调用 IAssetBundleUploader（可选）
        new BuildPlayerStep(),
        new SignAndroidStep(),
        new GenerateUploadManifestStep(),
    };
}
```

### 5.3 IAssetBundleProvider（★ 抽象核心）

```csharp
public interface IAssetBundleProvider
{
    string Name { get; }                              // "Addressables" / "YooAsset"
    bool IsInstalled { get; }                         // 通过 PackageInfo 检测
    AssetBundleBuildResult Build(BuildContext ctx);
    string GetOutputDirectory(BuildContext ctx);      // 待上传根目录
    string GetManifestRelativePath(BuildContext ctx); // manifest 文件
}

public class AssetBundleBuildResult
{
    public bool Success;
    public string OutputDirectory;
    public string Version;       // Provider 自管理（Addressables PlayerVersion / YooAsset PackageVersion）
    public string Error;
}

[AttributeUsage(AttributeTargets.Class)]
public class AssetBundleProviderAttribute : Attribute
{
    public string Name { get; }
    public AssetBundleProviderAttribute(string name) { Name = name; }
}
```

**关键决策**：

- **版本号由 Provider 自管理** — 不在框架层强加版本系统，Addressables 用 PlayerVersion，YooAsset 用 PackageVersion
- **产物结构由 Provider 自决定** — 框架只关心"输出目录"，上传时按目录推送
- **asmdef `versionDefines` 隔离** — `AddressablesProvider.asmdef` 仅在检测到 `com.unity.addressables` 时编译，避免硬依赖
- **特性 + 反射注册** — `[AssetBundleProvider("Addressables")]`，符合通用编码原则 §3

### 5.4 IAssetBundleUploader

```csharp
public interface IAssetBundleUploader
{
    string Name { get; }    // "TencentCos" / 后期 AliyunOss / AwsS3
    Task<UploadResult> UploadAsync(UploadRequest req, CancellationToken ct);
}

public class UploadRequest
{
    public string LocalDirectory;
    public string RemotePrefix;       // ab/<platform>/<env>/<version>/
    public UploadMode Mode;           // Full | Incremental
    public Func<string, bool> Filter;
}
```

**上传双路径策略（共用同一份 Python 脚本作为单一真实来源）**：

- **CI 路径（生产用）**：Unity 仅构建并把产物放到 `Build/<platform>/AB/`，同时生成 `upload-manifest.json`；GitHub Actions 在 Unity 退出后调用 `tools/upload-cos.py <manifest>` 上传。优势：解耦、Unity license 时间最小化。
- **Unity 路径（开发用）**：`TencentCosUploader` 通过 `Process.Start` 调同一个 `upload-cos.py`（先找 `<project>/tools/upload-cos.py`，再回落到 `<package>/CI/Templates~/tools/upload-cos.py`）。优势：开发者点一下就能完整跑通，无需自己写额外脚本。

两条路径**共用同一份 Python 脚本和 manifest 协议**（`upload-manifest.json`），避免双实现导致的不一致。

**默认关闭，显式启用**：`UploadAssetBundleStep` 检查 `T2FBUILD_UPLOAD_ENABLED` 环境变量，未设置或不是 `true`/`1` 时跳过上传（开发期默认行为，避免误传）。CI 流程在 yml 中显式设置 `T2FBUILD_UPLOAD_ENABLED=true`。

### 5.5 T2FBuildSettings（ScriptableSingleton）

存放于 `ProjectSettings/T2FBuildSettings.asset`，与 Unity 自带的 `EditorBuildSettings`、`EditorSettings` 同套机制。通过 `Edit > Project Settings > T2FBuild` 编辑，随 ProjectSettings/ 提交。

**为什么用 ScriptableSingleton 而非用户自建的 .asset**：
- 零配置：自动创建，无需用户手动 Create
- 单例语义贴合实际：一个项目一套构建配置；环境差异（dev/staging/prod）走环境变量
- 符合 Unity 约定：可通过 `[SettingsProvider]` 自动出现在 Project Settings 窗口

**首期字段**（已实现）：

```
T2FBuildSettings (ProjectSettings/T2FBuildSettings.asset)
├── Asset Bundle
│   ├── assetBundleProvider:    "Addressables"     ← 切换 AB 实现
│   └── abRemotePrefixTemplate: "ab/{target}/{env}/{version}/"
└── Upload
    ├── defaultUploader:           "TencentCos"
    ├── playerRemotePrefixTemplate:"webgl/{env}/{version}/"
    └── uploadEnabledByDefault:    false             ← T2FBUILD_UPLOAD_ENABLED 未设置时的回退
```

**后期里程碑追加字段**（落位明确，无迁移成本）：

```
├── Android (里程碑 6):   { keystorePath, keyAlias, packageFormat: AAB|APK, abiList }
├── iOS     (里程碑 8):   { teamId, bundleId, capabilities }
├── WebGL   (里程碑 6 后): { compression: Brotli|Gzip, memorySize }
└── WeChat  (里程碑 7):   { appId, transformToolPath, cdnUrl }
```

**敏感凭据不入 settings**：keystore 密码、签名证书内容、COS SecretKey 等仅通过环境变量或 GitHub Secrets 传入，不持久化到磁盘。settings 里只放路径/alias 等可公开的字段。

**优先级规则**（已实现 `T2FBUILD_UPLOAD_ENABLED`）：环境变量 > settings 默认值。CI 用 yaml 显式 `T2FBUILD_UPLOAD_ENABLED=false`/`true` 强制控制；本地开发可在 settings 里勾上 `uploadEnabledByDefault` 一次配置长期生效。

---

## 6. CLI 入口

GitHub Actions 通过 `-executeMethod` 调用：

```csharp
public static class BuildEntry
{
    public static void BuildAndroid() => Run(BuildTarget.Android);
    public static void BuildIOS()     => Run(BuildTarget.iOS);
    public static void BuildWebGL()   => Run(BuildTarget.WebGL);
    public static void BuildWeChat()  => Run(BuildTarget.WebGL, "wechat");

    static void Run(BuildTarget target, string profile = null)
    {
        var ctx = BuildContext.FromEnvironment(target, profile);
        BuildRunner.Execute(ctx);
    }
}
```

GitHub Actions yml：

```yaml
- uses: game-ci/unity-builder@v4
  with:
    targetPlatform: Android
    buildMethod: T2FBuild.Editor.BuildEntry.BuildAndroid
```

---

## 7. GitHub Actions 工作流编排

仓库根 `.github/workflows/`（由框架模板复制并定制）：

- **build.yml**：主入口，`workflow_dispatch` + `tag push`，矩阵触发各平台（v0.1 未实现，单平台时直接用平台 yml）
- **android.yml / ios.yml / webgl.yml / wechat.yml**：被调用的 reusable workflow
- 公共步骤：checkout → restore Library cache → game-ci/unity-builder → 上传 AB → 上传产物

**首期已实现：webgl.yml**

- 触发：`workflow_dispatch`（version / env / upload 三个 input）+ `push tags v*`（自动 prod + upload）
- 单 job 顺序：checkout → 计算参数 → 缓存 Library → game-ci/unity-builder 调 `BuildEntry.BuildWebGL`（`T2FBUILD_UPLOAD_ENABLED=false`，让 Unity 早退出释放 license）→ 安装 Python → 调 `upload-cos.py --manifest` 上传 AB → 调 `upload-cos.py --dir Build/WebGL/Player --remote-prefix webgl/<env>/<version>/` 上传整个 player → upload-artifact → job summary 输出可访问 URL
- COS 直接作为 WebGL 静态托管：`upload-cos.py` 按扩展名设置 `Content-Type` 和 `Content-Encoding`（`.br` → `br`, `.gz` → `gzip`），无需 COS 端额外配置
- 并发控制：`concurrency.group=webgl-<ref>` + `cancel-in-progress`，避免同分支堆积

**Secrets**：
- `UNITY_LICENSE` — GameCI Personal license（`.ulf` 文件内容，通过 `unity-request-activation-file` 获取）
- `TENCENT_SECRET_ID` / `TENCENT_SECRET_KEY` / `COS_BUCKET` / `COS_REGION` — COS 上传
- `ANDROID_KEYSTORE_BASE64` / `ANDROID_KEYSTORE_PASS` / `ANDROID_KEY_ALIAS` / `ANDROID_KEY_PASS`（后期）
- `IOS_P12_BASE64` / `IOS_P12_PASS` / `IOS_PROVISION_BASE64`（后期）
- `WECHAT_APPID`（后期，可选）

**关键缓存**：`Library/` 目录，冷启动 10+ 分钟、热启动 1-2 分钟。Library 缓存 key 包含 `Assets/**`、`Packages/**`、`ProjectSettings/**` 的 hash。

**项目接入步骤**：
1. `cp -r <package>/CI/Templates~/tools <project>/tools` 并提交
2. `cp <package>/CI/Templates~/workflows/webgl.yml <project>/.github/workflows/` 并提交
3. 配置上述 GitHub Secrets
4. tag push 或 workflow_dispatch 触发

---

## 8. 平台特殊处理

| 平台 | 关键问题 |
|------|----------|
| **Android** | keystore base64 进 secret，CI 中解码；AAB 用于 Google Play，APK 用于国内分发；ABI 配置（armv7 / arm64） |
| **iOS** | 必须 macOS runner；证书/Profile 用 `apple-actions/import-codesign-certs`；Xcode 后处理用 `OnPostprocessBuild` 改 Info.plist + 添加 Capabilities |
| **WebGL** | 压缩选 Brotli（COS 支持 Content-Encoding 静态分发）；CORS 配置；首屏加载优化 |
| **微信小游戏** | 装 [wechat-minigame Unity 转换工具](https://github.com/wechat-miniprogram/minigame-unity-webgl-transform)；WebGL build 后再调用转换；主包 ≤4MB，AB 必须放 COS+CDN；用 `wx.downloadFile` 拉取 |

---

## 9. 业务项目接入方式

`Packages/manifest.json`：
```json
{
  "dependencies": {
    "com.t2f.build": "https://github.com/unittt/T2FBuild.git"
  }
}
```

通过 `Edit > Project Settings > T2FBuild` 编辑 `T2FBuildSettings`（自动创建到 `ProjectSettings/T2FBuildSettings.asset`），随项目提交即可。

打开 `Window > T2FBuild > CI Template Installer` 选择需要的 workflow（多选），勾选「Also copy tools/」一并安装 Python 上传脚本，点 Apply 自动写入到 `<project>/.github/workflows/` 和 `<project>/tools/`，提交即可。

---

## 10. 落地里程碑

按顺序验证设计，每步可独立交付：

| # | 里程碑 | 验证目标 |
|---|--------|----------|
| 1 | 包骨架 + Core 抽象 | UPM 结构 + IBuildStep/BuildContext/BuildPipeline + BuildEntry |
| 2 | WebGLBuilder | 流水线跑通（最简单，无签名问题） |
| 3 | AddressablesProvider | 验证 AB 抽象 |
| 4 | TencentCosUploader + upload-cos.py | 验证上传抽象、双路径一致性 |
| 5 | GitHub Actions WebGL workflow | 验证 CI 链路 |
| 6 | AndroidBuilder + 签名 + COS 上传 | 验证完整生产链路 |
| 7 | WeChatBuilder | 在 WebGL 基础上加转换步骤 |
| 8 | iOSBuilder | macOS runner + 证书 |

---

## 11. 已记录的未来扩展点（暂不在 v0.1 实现）

以下需求**已识别但不在首期范围**，待 v0.2+ 或具体需求出现时再细化。如果在落地过程中发现首期接口确实无法承载，再回头重构。

### 11.1 AB 系统更细粒度抽象

当前 `IAssetBundleProvider` 假设 "Addressables 和 YooAsset 都是目录化产物"，接口最小。未来若需支持以下场景，可能需要更细粒度（如 `IPackageGroup`、`IBundleEncryption`）：

- **加密 AB**：自定义加解密 Provider 钩子
- **分包 / 分组下载**：主包 + 多个可独立更新的子包（小游戏强需求）
- **资源版本回滚**：通过远端版本号切换到历史版本
- **差量补丁（binary diff）**：上传 patch 文件而非整文件

### 11.2 上传层扩展

- **多对象存储**：阿里云 OSS、AWS S3、华为云 OBS
- **CDN 主动刷新**：上传完触发腾讯云 CDN PurgeUrlsCache
- **断点续传**：大文件上传失败时续传
- **并发上传调优**：根据 CI 网络环境动态调整并发数

### 11.3 平台扩展

- **抖音小游戏**：字节跳动 starkc 转换工具
- **小米快游戏 / OPPO 快游戏**
- **PC（Windows / macOS Standalone）**
- **Switch / PS5 / Xbox**（需要平台 SDK，可能不开源）

### 11.4 CI 平台扩展

- **自建 Jenkins**：模板调整即可
- **腾讯云 CODING DevOps**：国内拉镜像更快
- **本地 Mac mini 机器**：iOS 构建专用

### 11.5 高级能力

- **版本号自动管理**：从 Git tag / commit count 推导 versionCode
- **构建产物归档**：产物自动归档到对象存储 + 元数据（commit、changelog）
- **通知集成**：飞书 / 钉钉 / 企业微信 webhook
- **构建报告**：包大小变化、依赖变化、首屏性能等报表
- **冒烟测试集成**：构建后自动跑 PlayMode/UTF 测试
- **多渠道打包**（Android）：渠道 ID 注入、多签名

---

## 12. 与现有项目（BounceBlast）的关系

- BounceBlast 作为 T2FBuild 的**第一个落地项目**，用于真实场景验证
- BounceBlast 现有 `Assets/Res/` Addressables 目录，第一期可直接对接
- 框架稳定后，T2FBuild 应支持任何 T2F 系列项目零成本接入

---

## 13. 设计签收

第 2 节所有项均已确认，无待定事项。后续如需调整设计，在本文件中追加 ADR（架构决策记录）章节即可。
