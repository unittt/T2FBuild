# T2FBuild

Unity 自动化打包框架。

## 特性

- **多平台支持**：Android、iOS、WebGL、微信小游戏
- **CI 集成**：GitHub Actions（基于 [GameCI](https://game.ci)）
- **AB 系统抽象**：Addressables（首期），YooAsset（后期）
- **上传层抽象**：腾讯云 COS（首期），可扩展其他对象存储
- **可插拔扩展**：特性 + 反射注册，新增 Provider 不修改框架核心

## Unity 版本

- 最低：Unity 2022.3 LTS
- 兼容：Unity 6000.x（首期未在 CI 矩阵中验证）

## 安装

`Packages/manifest.json`：

```json
{
  "dependencies": {
    "com.t2f.build": "https://github.com/unittt/T2FBuild.git"
  }
}
```

开发期可改用本地路径：

```json
"com.t2f.build": "file:../../T2FBuild"
```

## 设计文档

完整设计参考 [docs/T2FBuild-Design.md](https://github.com/unittt/BounceBlast/blob/main/docs/T2FBuild-Design.md)。

## License

MIT
