# T2FBuild Roadmap

> 状态：v0.1 阶段 — WebGL CI 闭环 + 微信小游戏已完成
> 最后更新：2026-05-19

里程碑划分对应 [docs/design.md](design.md) §10。每个 PR 完成后应回到本文件勾选状态、补充 commit。

---

## 已完成（v0.1 — WebGL CI 闭环 + 微信小游戏）

| # | 内容 | 关键产物 | Commit |
|---|------|----------|--------|
| 1 | 包骨架 + Core 抽象 | `BuildContext` / `IBuildStep` / `BuildRunner` / `BuildEntry` + `PlatformBuilderRegistry`（反射注册） | `3579946` |
| 2 | WebGL + AB 抽象 + Addressables | `IAssetBundleProvider`, `WebGLBuilder`, `AddressablesProvider`（versionDefines 隔离） | `a717fa4` |
| 3 | 上传抽象 + Tencent COS | `IAssetBundleUploader`, `UploadManifest`, `TencentCosUploader`（Python 壳），`upload-cos.py` + `RegistryScanner` 抽取 | `9f8483a` |
| 4 | GitHub Actions webgl.yml | 完整 CI 链路 + Player 直传 COS 静态托管（`--dir` 模式 + Content-Type/Encoding） | `55ed497` |
| 6 | **WeChat 小游戏** | `WeChatBuilder`（profile=wechat，versionDefines 隔离）+ `ConfigureWeChatProjectStep` / `RunWeChatExportStep`（一行调 `WXConvertCore.DoExport`）/ `ValidateWeChatPackageSizeStep`（4MB 主包检查）/ `UploadWeChatFirstPackageStep`（首包数据→COS 复用 manifest 协议）+ `wechat.yml`（Node + AB/首包双 manifest 上传 + minigame artifact） | pending |
| – | 编辑器辅助 — CI 模板安装器 | `CITemplateInstallerWindow`（多选 + 自动连带 `tools/`） | `3a53391` |
| – | 编辑器辅助 — 配置单例 | `T2FBuildSettings` + Project Settings UI（注册表驱动下拉框） | `c1bc7cd` |

设计文档对应章节：§4（结构）、§5（抽象）、§7（CI）、§8（平台）。

---

## 计划中（v0.2 — 完整平台覆盖）

| # | 内容 | 主要工作 | 状态 |
|---|------|----------|------|
| 5 | **Android** | `AndroidBuilder` + `SignAndroidStep`（keystore base64 → 文件 → 注入 PlayerSettings）+ `.apk` / `.aab` 产物 + `android.yml` workflow + Settings 中的 Android section | ⏳ |
| 7 | **iOS** | `IOSBuilder`（macOS runner）+ Xcode 工程后处理（Info.plist / Capabilities）+ 证书导入步骤 + `ios.yml` | ⏳ |

每个平台落地都包含：Builder + 必要 Step + workflow yml + Settings 字段补充。**新增平台只加文件、不改框架核心**（开闭原则）。

### Android 落地拆解

- `Editor/Steps/SignAndroidStep.cs` —— 读 `ANDROID_KEYSTORE_BASE64` 写临时文件，注入 `PlayerSettings.Android.keystoreName/keyaliasName` + pass（密码走 env）
- `Editor/Platforms/AndroidBuilder.cs` —— `[PlatformBuilder(BuildTarget.Android)]`，组合：`SwitchPlatform → ApplyVersion → BuildAssetBundle → SignAndroid → BuildPlayer → GenerateUploadManifest → UploadAssetBundle`（Player 产物不上传 COS，等分发平台逻辑）
- `T2FBuildSettings` 追加：`androidKeystorePath`, `androidKeyAlias`, `androidPackageFormat: AAB|APK`, `androidAbiList`
- `CI/Templates~/workflows/android.yml` —— 同 webgl.yml 结构，多一步 keystore base64 解码

### iOS 落地拆解

- 需 macOS runner（GameCI 支持，但成本翻倍）
- `Editor/Steps/IOSPostProcessStep.cs` —— `[PostProcessBuild]` 改 Info.plist / 加 Capabilities
- 证书：`apple-actions/import-codesign-certs` action 处理 p12 + provisioning profile
- 产物：Xcode project（archive 通常在 CI 后续步骤里 xcodebuild）

---

## 验证待办

在加新平台前应做的端到端验证：

- [ ] **BounceBlast 真跑 webgl.yml**：
  1. 切 `Packages/manifest.json` 中 `com.t2f.build` 从 `file:` 到 GitHub URL
  2. `Window > T2FBuild > CI Template Installer` 一键复制模板到 BounceBlast
  3. 配 GitHub Secrets（`UNITY_LICENSE` + 4 个 COS 变量）
  4. `git tag v0.0.1` push 触发，验证 prod + upload 路径
- [ ] **Addressables 远端 LoadPath 实际指向 COS URL**：在 BounceBlast 验证 AB 真能从上传后的 COS 路径加载（影响 §5.3 的 Provider 接口是否够用）

---

## 未来扩展（v0.3+，按需调度）

来自 [design.md](design.md) §11，仅作 backlog 不承诺时间表。

### AB 系统更细粒度

- 加密 AB（自定义加解密 Provider 钩子）
- 分包 / 分组下载（小游戏强需求）
- 资源版本回滚
- 差量补丁（binary diff）
- **第二个 Provider — YooAsset**：验证当前 `IAssetBundleProvider` 接口是否足够通用

### 上传层扩展

- 阿里云 OSS / AWS S3 / 华为云 OBS uploader
- CDN 主动刷新（腾讯云 `PurgeUrlsCache`）
- 断点续传
- 上传并发调优（按 CI 网络环境动态）

### 平台扩展

- 抖音小游戏
- 小米 / OPPO 快游戏
- PC Standalone（Windows / macOS）
- 主机平台（商业授权可行时）

### CI 平台扩展

- 自建 Jenkins
- 腾讯云 CODING DevOps（国内镜像更快）
- 本地 Mac mini 池（iOS 专用）

### 工程化能力

- 版本号自动管理（git tag / commit count 推导 versionCode）
- 多渠道打包（Android 渠道 ID 注入）
- 通知集成（飞书 / 钉钉 / 企业微信 webhook）
- 构建报告（包大小变化、依赖变化、首屏性能）
- 冒烟测试集成（构建后跑 PlayMode/UTF 测试）
- BuildWindow（一键本地打包窗口，预留位置在 `Editor/Window/`）

---

## 已知技术债

- `GenerateUploadManifestStep` 使用 `Path.GetRelativePath`（.NET Standard 2.1），Unity 2022.3 OK，若兼容更老版本需替换
- `IAssetBundleUploader.UploadAsync` 返回 Task，目前 step 端 `.GetAwaiter().GetResult()` 阻塞 — 后续若引入 `IAsyncBuildStep`，整条流水线可异步化
- `upload-cos.py` `--dir` 模式不计算 SHA256（速度优先），增量上传需要时再补
- 三个反射注册器（`PlatformBuilder` / `AssetBundleProvider` / `AssetBundleUploader`）现已通过 `RegistryScanner` 抽取共用扫描；若再加第四个，考虑泛型 `NamedRegistry<TInterface, TAttribute>` 进一步压缩
