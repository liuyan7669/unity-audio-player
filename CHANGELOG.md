# Changelog

## [1.0.0] - 2026-07-22

- 新增 `AudioPlayer` 统一播放、暂停、恢复和停止控制。
- 支持 `AudioClip`、Resources、网络地址、本地文件及 StreamingAssets 音频。
- 新增播放状态、当前 Clip、AudioSource、采样位置和采样时长只读接口。
- 新增自然播放结束回调和归一化播放进度事件。
- 新增可独立启用的频谱与输出波形数据事件。
- 使用内部状态、PCM 采样位置和 DSP 时间检测自然播放结束，不依赖 `AudioSource.isPlaying`。
- 支持 Unity WebGL 的异步外部音频加载方式。
