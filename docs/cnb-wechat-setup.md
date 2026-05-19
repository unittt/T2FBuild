# CNB × WeChat MiniGame 端到端配置指南

> 把 T2FBuild 框架在 CNB 上跑通微信小游戏自动打包的实操手册。框架架构与设计权衡见 [design.md](design.md)。

适用范围：当前仓库托管在 [cnb.cool](https://cnb.cool)，目标产物是微信小游戏（main package + AB 资源 + 首包数据）。WebGL/Android/iOS 流程类似，差异点在 §7 末尾注明。

---

## 0. 前置依赖

确认以下条件已满足：

- [x] Unity **2022.3.x** 已激活 Personal License（本机已能正常打开项目）
- [x] 项目依赖 `com.qq.weixin.minigame` 包已装（`Packages/manifest.json` 中存在）
- [x] T2FBuild 包已通过 UPM Git 或 git submodule 接入
- [x] 拥有一个腾讯云 COS Bucket 和一对 SecretId / SecretKey

---

## 1. 准备 Unity License（`.ulf` → base64）

**重要**：Unity Hub 本机激活后，`.ulf` 已存在于：

```
Windows:  C:\ProgramData\Unity\Unity_lic.ulf
macOS:    /Library/Application Support/Unity/Unity_lic.ulf
Linux:    /var/lib/unity/Unity_lic.ulf
```

> 推荐使用 §4 的 **Project Settings > T2FBuild > Secrets** section（位于 `Edit > Project Settings > T2FBuild`，所有 secrets 字段直接编辑，后端落 envs.yml）一次性填完 `.ulf` 编码 + 4 个 COS 字段——把当前 §1/§2 + §3 Option B 几步合一。本节作为命令行兜底方式保留。

### 命令行方式

```bash
# Git Bash / WSL
base64 -w0 "/c/ProgramData/Unity/Unity_lic.ulf" > license.b64

# PowerShell
[Convert]::ToBase64String([IO.File]::ReadAllBytes("C:\ProgramData\Unity\Unity_lic.ulf")) `
  | Out-File license.b64 -NoNewline -Encoding ASCII
```

`.alf` 流程（已可跳过，仅在本地 `.ulf` 在容器内被拒时回退）：

```bash
docker run --rm -v "$PWD":/work -w /work unityci/editor:2022.3.14f1-webgl-3 \
  unity-editor -batchmode -nographics -createManualActivationFile -logFile -
# 把生成的 Unity_v2022.x.alf 上传 https://license.unity3d.com/manual 换 .ulf
```

> Unity Personal License 与硬件指纹（MAC/主机名）绑定。本机 `.ulf` 拿进 Docker 通常能用；偶尔会被拒，那时再走 `.alf` 流程在容器里激活一次即可。

---

## 2. 准备腾讯云 COS

- 建 Bucket（如 `bounceblast-1234567890`），记录 region（如 `ap-shanghai`）
- 在 CAM 给子账号最小权限：COS 写入相关策略
- 拿到 `SecretId` / `SecretKey`

---

## 3. Secrets 存放方案（三选一）

CNB 没有 web UI Secrets，通过 `imports:` 引用 `envs.yml`。按需选：

### Option A —— 内联到 `.cnb.yml`（最简单）

`.cnb.yml` 里删掉 `imports:`，把每个 pipeline 的 `env:` 块直接写满：

```yaml
env:
  UNITY_LICENSE_BASE64: |
    <license.b64 内容>
  TENCENT_SECRET_ID: AKID...
  TENCENT_SECRET_KEY: ...
  COS_BUCKET: bounceblast-1234567890
  COS_REGION: ap-shanghai
```

适用：单人项目，secrets 跟代码同密级。

### Option B —— 同仓库 `envs.yml`（**单人开发推荐**）

仓库根建 `envs.yml`，`.cnb.yml` 改 `imports:` 指向当前仓库：

```yaml
# .cnb.yml
x-imports: &imports
  - https://cnb.cool/<your-org>/<your-repo>/-/blob/main/envs.yml
```

```yaml
# envs.yml（提交到仓库，不要进 .gitignore——CI 要拉）
env:
  UNITY_LICENSE_BASE64: |
    <license.b64 内容>
  TENCENT_SECRET_ID: AKID...
  TENCENT_SECRET_KEY: ...
  COS_BUCKET: bounceblast-1234567890
  COS_REGION: ap-shanghai
```

适用：单人 / 小团队，所有有代码读权限的人也该看 secrets。

#### 用 Project Settings > Secrets section 自动填写（推荐）

打开 `Edit > Project Settings > T2FBuild`，最下方的「Secrets (envs.yml)」section 一窗填完 envs.yml 全部 5 个字段：

- **加载现有值**：进入面板自动读取仓库根 `envs.yml`，把已填字段灌回 UI（Secret Key 走密码字段掩码；License 显示长度而不是完整 base64）
- **Tencent Secret ID / Secret Key**：从腾讯云 CAM 复制；密码字段防肩窥但可 `Ctrl+C` 复制
- **Unity License**：「Load from .ulf...」按钮自动检测 `C:\ProgramData\Unity\Unity_lic.ulf`（Mac / Linux 同理），一键 base64 编码灌入字段
- **Bucket / Region**：**不在 Secrets section 重复**——它们已经在上面的「Tencent COS」section 里维护，Save 时自动一起写入 envs.yml
- **保存策略**：表单值只活在 UI 内存，**点「Save to envs.yml」按钮才落盘**，避免每次敲键写文件；上方「Reload」可丢弃未保存改动重读

**写入策略**：
- 行级合并——envs.yml 里 5 个标准字段之外的所有自定义 key 和注释**全部保留**
- BuildWindow 启动构建时自动 inject envs.yml 中所有字段到进程环境变量，构建结束恢复
- 同一份 envs.yml 同时被本地 BuildWindow + CNB pipeline (`imports:`) 消费，**单一来源**

**安全约束**：
- T2FBuildSettings.asset（committed）只存非密配置；secrets 落在 envs.yml（按 §3 选项决定要不要 commit）
- Secret Key 字段值在 UI 内存中，永不持久化到 EditorPrefs / ScriptableObject
- 不调用任何外部 API（无连接测试、无凭据校验），凭据仅本地读写

### Option C —— 独立私有 secrets repo（团队场景）

另建私有仓库 `https://cnb.cool/<your-org>/secrets`，`envs.yml` 内容同上。`.cnb.yml`：

```yaml
x-imports: &imports
  - https://cnb.cool/<your-org>/secrets/-/blob/main/envs.yml
```

适用：要给某些人代码权限但不给 secrets 权限；或多个项目共用同一套 secrets 集中管理。

---

## 4. 配置 T2FBuildSettings

Unity → `Edit > Project Settings > T2FBuild`：

### Project

| 字段 | 值 | 备注 |
|---|---|---|
| Project ID | `bounceblast`（或空） | 多项目共用一个 COS bucket 时填——COS key 会以 `<projectId>/` 开头隔离各项目；单项目 bucket 留空 |

填了 Project ID 后，AB / Player / WeChat 首包路径都会自动加前缀。例：`bounceblast/ab/WebGL/dev/0.0.1/<file>`、`bounceblast/webgl/dev/0.0.1/index.html`。

如果 CI 端需要（Player 上传走 `--dir` 模式），还要在 CI 配置里同步设置 `BUILD_PROJECT`：
- GitHub Actions：Settings > Secrets and variables > Actions > Variables 加一条 `BUILD_PROJECT=bounceblast`
- CNB：`.cnb.yml` 里 `web_trigger_webgl` / 标签 webgl 两个 pipeline 的 `env:` 块改 `BUILD_PROJECT: 'bounceblast'`

### Tencent COS

| 字段 | 值 | 备注 |
|---|---|---|
| Bucket | `bounceblast-1234567890` | 与 §2 COS Bucket 对应；公开标识符，非密钥 |
| Region | `ap-shanghai` | 公开标识符 |

这两个字段同时作为 envs.yml 中 `COS_BUCKET` / `COS_REGION` 的来源（Secrets section 保存时自动写入），并在「WeChat MiniGame > CDN Base URL」为空时**自动派生**运行时 CDN URL：`https://<Bucket>.cos.<Region>.myqcloud.com/`。

### WeChat MiniGame

| 字段 | 值 | 备注 |
|---|---|---|
| AppId | `wxXXXXXXXXXXXXXXXX` | 微信公众平台 → 小游戏 → 开发设置 |
| CDN Base URL | 空 / 或 `https://cdn.example.com/` | **空** = 自动用上方 Bucket+Region 派生（直连 COS 静态托管）；填 = 自定义 CDN 域名（运行时从这个 URL 取首包数据） |
| Custom Node Path | 留空 | CNB 容器走系统 PATH |
| First Package Glob | `webgl.data*` | 默认值，Unity 2022 命名约定 |
| First Package Remote Prefix | `wechat/{env}/{version}/data/` | 默认值；token 自动替换 |

主包大小校验**不**由框架做——微信 SDK 自身的「首资源管理 CDN 模式」（`MiniGameConfig.asset` 的 `assetLoadType` 配置）+ 微信后台发布校验已经把关，框架重复校验只会误伤。

**保存后 commit** `ProjectSettings/T2FBuildSettings.asset`。

---

## 5. 本地先跑通（强烈推荐）

避免在 CNB 上才发现 SDK 配置问题。打开 `Window > T2FBuild > Build`：

- **Target** 下拉选 `WebGL (wechat)`（注册表自动列出所有可用平台 + profile）
- **Version**: `0.0.1`
- **Env**: `dev`
- **Upload to COS**: **取消勾选**（首次只验证本地构建链路，不传 COS）
- 点 **Build** 按钮

确认：
- `Build/WebGL_wechat/minigame/` 出现
- 窗口底部状态条显示 `Build succeeded` + 耗时
- 把 `minigame/` 拖进微信开发者工具能正常打开 + 预览运行

> 也可以直接调静态方法：`T2FBuild.Editor.BuildEntry.BuildWeChat()`（参数走环境变量 `BUILD_VERSION` / `BUILD_ENV`，回退默认 `0.0.1` / `dev`）。CI 走的就是这个入口。

本地过了再上 CNB。

---

## 6. 安装 CI 模板到项目

Unity → `Window > T2FBuild > CI Template Installer`：

1. **CI Platform** 下拉选 **CNB**
2. 勾选 `cnb.yml` + `Also copy tools/`
3. 点 **Apply**

写入：
- 仓库根 `.cnb.yml`
- 仓库根 `tools/upload-cos.py`、`tools/requirements.txt`、`tools/README.md`

---

## 7. 编辑 `.cnb.yml`

定位顶部：

```yaml
x-imports: &imports
  - https://cnb.cool/REPLACE_ME_ORG/secrets/-/blob/main/envs.yml
```

按 §3 选的方案改：
- **Option A**：删掉 `x-imports`、`imports: *imports` 行，把 secrets 写到每个 pipeline 的 `env:` 里
- **Option B**：改成 `https://cnb.cool/<your-org>/<your-repo>/-/blob/main/envs.yml`
- **Option C**：改成 `https://cnb.cool/<your-org>/secrets/-/blob/main/envs.yml`

---

## 8. 检查 `.gitignore`

确保：
- `Build/` **被** ignore（构建产物别推上去）
- `tools/` **不被** ignore（CI 从仓库读 `tools/upload-cos.py`）
- `.cnb.yml` **不被** ignore
- 若用 Option B：`envs.yml` **不被** ignore

---

## 9. 切分支调试（重要）

`.cnb.yml` 首次接入未经过端到端验证，前 2-5 次 push 大概率要改（Library 挂载路径、`unity-editor` 命令名、心跳脚本等），别直接在 `main` 上反复试，主要原因：

- 含真实 secrets 的 `envs.yml` 一旦 commit 到 `main`，即使后续删除文件，git history 里仍可恢复
- 子模块 pointer 反复指向不稳定的 T2FBuild commits 会污染主仓 history

### 建议分支名：`ci/cnb-wechat-trial`

```bash
git checkout -b ci/cnb-wechat-trial
```

命名含义：`ci/` 前缀表明是 CI 相关；`cnb-wechat` 指代目标平台 + 业务方向；`trial` 表明这是验证阶段，可以反复 force-push、可以最后丢弃。

### T2FBuild 子模块策略

| 子模块改动类型 | 落在哪 |
|---|---|
| 框架本身的改进（CIPlatformDef、PackagePath helper、CNB 模板首版） | 子模块 `main` 直接 commit + push（独立于 trial） |
| 真跑过程中发现要改 `.cnb.yml` 模板 / 步骤的 bug | 子模块开 `fix/cnb-template` 分支；主仓 trial 分支的 submodule pointer 临时指向它 |

trial 通过 → 子模块 `fix/cnb-template` 合 `main` → 主仓 trial 的 submodule pointer 切回子模块 main → 主仓 trial 合 main。

### Secrets 轮换（trial 合 main 前必做）

Option B（同仓库 `envs.yml`）时，trial 分支历史会留下含真 secrets 的 commit。即使最后只把「干净」状态合 main，git history 仍包含敏感值。**合 main 前一定要轮换**：

1. 腾讯云 CAM 重新生成一对 SecretId / SecretKey，作废 trial 里的那对
2. 如果担心 license 泄露：在 Unity License Manager 撤销旧 `.ulf`、重新激活（Personal License 一般不必，影响小）
3. 用新值更新 `envs.yml` 后再合 main

如果选了 Option A 或 Option C，这步可以跳过。

### CNB 触发器在分支上的行为

模板里的触发结构：

| 触发器 | 分支限制 |
|---|---|
| `web_trigger_webgl` / `web_trigger_wechat` | **不限分支**，在 CNB Web UI 点 Run 时可以选当前分支 |
| `tag_push v*` / `tag_push wx-v*` | 跟分支无关，只看标签 |

所以 trial 分支随便 push（CNB 不会自动跑），要测试就在 Web UI 选 trial 分支 + `web_trigger_wechat`。标签触发慎用——`wx-v0.0.1` 一旦 push 就跑 prod env + 自动上传 COS。

---

## 10. Commit & Push

```bash
git add .cnb.yml tools/ ProjectSettings/T2FBuildSettings.asset
# Option B 额外：
git add envs.yml
git commit -m "ci(cnb): add WeChat MiniGame pipeline"
git push origin main
```

> T2FBuild 是 git submodule，前面 §4 / §6 涉及框架自身的改动需要先在 `Assets/T2FBuild/` 子模块内 commit + push，再回主仓 commit submodule pointer。

---

## 11. 触发首次构建

### 手动触发（推荐首跑）

打开 CNB 流水线页：`https://cnb.cool/<your-org>/<your-repo>` → 流水线 → **Run**

选 `web_trigger_wechat`，表单填：

| 参数 | 首跑值 | 说明 |
|---|---|---|
| `VERSION` | `0.0.1` | 任意 semver |
| `ENV` | `dev` | 写入 manifest 的 prefix |
| `UPLOAD` | `false` | **首跑设 false** — 只验证构建链路，不传 COS |

### 标签触发（生产）

```bash
git tag wx-v0.0.1
git push origin wx-v0.0.1
```

自动用 prod env + UPLOAD=true 跑。

---

## 12. 监控关键 stage

按顺序看 log：

| Stage | 期望输出 | 失败信号 |
|---|---|---|
| `install-node` | `v20.x.x` | apt 报错 → 网络问题 |
| `activate-unity-license` | `Next license update check is after` | `License is invalid` → 走 §1 `.alf` 流程 |
| `build-wechat` | Addressables build + WXConvertCore 日志持续滚动 | 静默 >10min 被杀 → 见 §14 心跳 |
| `validate-wechat-main-package-size` | `WeChat main package OK: X MB` | `too large` → 调整 First Package Glob 或缩资源 |
| `upload-asset-bundles` | `Uploaded N files, M bytes total` | 仅在 UPLOAD=true 时跑 |
| `upload-wechat-first-package` | 同上 | 同上 |

冷启动总时长 25-45 分钟（Library 缓存空）；热启动 8-15 分钟。

---

## 13. 取产物 → 提交微信

**当前模板的局限**：`pack-minigame-artifact` 只在容器内 `tar`，容器退出就丢。要拿到本地有两种办法：

### 方案 1（暂时）：本地重跑

CI 上验证 secrets / 上传链路即可，最终用于提交微信的产物**从本地 §5 那一步拿**——本地 `Build/WebGL_wechat/minigame/` 直接拖进微信开发者工具上传。

### 方案 2（待补）：minigame.tar.gz 也上传 COS

需要给 `.cnb.yml` 末尾加一步：

```yaml
- name: upload-minigame-archive
  script: |
    if [ "$UPLOAD" != "true" ]; then exit 0; fi
    python tools/upload-cos.py \
      --dir Build/WebGL_wechat \
      --remote-prefix "wechat-artifacts/${BUILD_VERSION}/" \
      # 限定只传 minigame.tar.gz（upload-cos.py 当前不支持 glob 过滤，
      # 临时用 tar 后单独建目录再传）
```

这步框架还没做，下次需求时再补。

---

## 14. 已知坑点

### Library 缓存挂载路径

模板假设代码挂在 `/workspace/`，volume 是 `/workspace/Library:copy-on-write`。如果 CNB 实际路径不同，第一次缓存不会生效。诊断：

```yaml
- name: debug-pwd
  script: pwd && ls -la
```

按实际路径修 volume。

### `unity-editor` 命令名

unityci/editor 镜像内可执行文件应该叫 `unity-editor`。如果 `command not found` 试：
- `/opt/unity/Editor/Unity`
- `xvfb-run -a /opt/unity/Editor/Unity`

### 10 分钟无日志超时

Unity 资源 import 阶段可能长时间静默。在 build stage 外套心跳：

```yaml
- name: build-wechat
  script: |
    set -e
    ( while true; do echo "[heartbeat $(date +%H:%M:%S)]"; sleep 60; done ) &
    HEARTBEAT_PID=$!
    unity-editor -batchmode -nographics -logFile - \
      -projectPath "$PWD" \
      -executeMethod T2FBuild.Editor.BuildEntry.BuildWeChat \
      -quit
    RET=$?
    kill $HEARTBEAT_PID
    exit $RET
```

### License 在容器内被拒

Unity 验证硬件指纹严格时本机 `.ulf` 可能不被容器接受。回退方案：

```bash
# 在与 CI 相同的镜像里激活
docker run --rm -v "$PWD":/work -w /work unityci/editor:2022.3.14f1-webgl-3 \
  unity-editor -batchmode -nographics -createManualActivationFile -logFile -
# 上传 .alf 到 https://license.unity3d.com/manual 换 .ulf，再 base64
```

---

## 15. 推荐首跑 ramp-up 顺序

每步只验证一件事，出问题好定位：

1. ✅ §5 本地 Unity 跑 `BuildEntry.BuildWeChat()` 出 `minigame/`
2. ✅ §11 CNB `web_trigger_wechat`，`UPLOAD=false` → 验证 license 激活 + 构建链路
3. ✅ CNB `web_trigger_wechat`，`UPLOAD=true` → 验证 COS 上传链路
4. ✅ `git tag wx-v0.0.1 && git push --tags` → 验证生产路径（prod env + 自动上传）
5. ✅ COS Bucket 看到 `ab/WebGL/prod/0.0.1/*` 和 `wechat/prod/0.0.1/data/webgl.data*`
6. ✅ 本地 `minigame/` 拖进微信开发者工具 → 验证首屏能从 COS 拉首包数据

---

## 16. WebGL 流程的差异

WebGL（非小游戏）走 `web_trigger_webgl` 或 `v*` 标签。区别：

- 不需要 §4 中 WeChat 那几个 settings 字段
- `cnb.yml` 里 WebGL 块**不装 Node**（微信小游戏才需要）
- 上传第二条是 `--dir Build/WebGL/Player --remote-prefix webgl/{env}/{version}/`（Player 整目录作为静态站点）
- 不存在「首包数据上传」一步

其它机制（license、secrets、Library 缓存）完全一致。
