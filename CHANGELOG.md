# Changelog

## [Unreleased]

## [3.1.0] - 2026-11-14

- 接入 OpenUPM 官方 tag 发布工作流，自动触发扫描并检查注册表发布结果。

- 统一 Cowart 包版本为 3.1.0。

- 声明 Inspector Groups 3.1.0 依赖，随包安装统一 Inspector 支持。

## [1.0.3] - 2026-11-08

- 统一 Cowart 包图标：重新设计为按功能类别配色的圆角图标（扬声器 + 声波），同步更新 `Icon.png` 与 `Icon@2x.png`。

## [1.0.2] - 2026-08-23

- 新增包专用图标，并将包内组件脚本的默认图标统一替换为该图标。

## [1.0.1] - 2026-08-15

- 新增可直接运行的 UGUI Audio Player 示例场景，覆盖播放控制、外部音频、状态、进度、频谱和波形数据。

## [1.0.0] - 2026-07-22

- 新增 `AudioPlayer` 统一播放、暂停、恢复和停止控制。
- 支持 `AudioClip`、Resources、网络地址、本地文件及 StreamingAssets 音频。
- 新增播放状态、当前 Clip、AudioSource、采样位置和采样时长只读接口。
- 新增自然播放结束回调和归一化播放进度事件。
- 新增可独立启用的频谱与输出波形数据事件。
- 使用内部状态、PCM 采样位置和 DSP 时间检测自然播放结束，不依赖 `AudioSource.isPlaying`。
- 支持 Unity WebGL 的异步外部音频加载方式。
