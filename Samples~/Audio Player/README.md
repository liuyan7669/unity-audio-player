# Audio Player Examples

导入 Sample 后打开 `Audio Player.unity`，进入 Play Mode 即可使用。示例界面完全使用 UGUI 搭建，不使用 `OnGUI`，无需手动绑定 Inspector 引用。

- “播放示例音频”优先播放 Inspector 绑定的 `AudioClip`；未绑定时会在运行时生成 6 秒测试音。
- “暂停”“恢复”“停止”分别演示 `Pause()`、`Resume()` 和 `Stop()`。
- 外部音频输入框演示 `PlayExternal(...)`，支持 URL、本地绝对路径和 StreamingAssets 相对路径。
- 状态区域实时显示 `State`、采样位置、时长和 `ProgressChanged` 事件值。
- 可分别开启频谱与输出波形采集，并查看 UGUI Image 柱状图绘制结果。
- 主动停止不会触发完成回调；等待自然播放结束会在状态栏显示完成回调结果。
- WebGL 首次播放和外部音频加载请由真实用户点击按钮触发；跨域 URL 仍需服务器允许 CORS。
