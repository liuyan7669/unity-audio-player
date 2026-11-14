# Audio Player

Audio Player 是一个 Unity 音频播放包，用于统一管理 `AudioSource` 的播放、暂停、恢复和停止，并提供自然播放结束回调、精确采样时间、播放进度、频谱及输出波形数据。

## 功能

- 播放项目中的 `AudioClip`。
- 加载并播放 `Resources` 音频。
- 异步加载网络地址、本地文件地址及 `StreamingAssets` 音频。
- 通过内部状态区分播放、暂停和停止，不依赖 `AudioSource.isPlaying`。
- 使用 `samples`、`timeSamples` 和 DSP 时间检测自然播放结束。
- 提供归一化播放进度事件。
- 按需提供频谱和输出波形数据。
- 支持 Unity WebGL 使用的 `UnityWebRequest` 加载方式。

## 环境要求

- Unity 2021.3 或更高版本。
- Unity Audio 模块。
- Unity UnityWebRequest Audio 模块。

## 安装

### 通过 OpenUPM 安装

在 Unity 的 `Edit > Project Settings > Package Manager` 中添加：

```text
Name: package.openupm.com
URL: https://package.openupm.com
Scope: com.cowart.audio-player
```

然后通过 `Add package by name` 安装：

```text
com.cowart.audio-player
```

### 通过 Git URL 安装

```text
https://github.com/liuyan7669/unity-audio-player.git#3.1.0
```

## 快速开始

命名空间和类名都包含 `AudioPlayer`，建议使用类型别名：

```csharp
using AudioPlayerComponent = Cowart.AudioPlayer.AudioPlayer;
using UnityEngine;

public class AudioExample : MonoBehaviour
{
    [SerializeField] private AudioClip audioClip;

    private void Start()
    {
        AudioPlayerComponent player = gameObject.AddComponent<AudioPlayerComponent>();
        player.Play(audioClip, () => Debug.Log("音频自然播放完成"));
    }
}
```

`AudioPlayer` 会获取当前物体上的 `AudioSource`，不存在时自动创建。

## 示例场景

在 Package Manager 中打开 Audio Player，导入 `Audio Player Examples`，然后打开：

```text
Assets/Samples/Audio Player/3.1.0/Audio Player Examples/Audio Player.unity
```

示例界面使用 UGUI 搭建。进入 Play Mode 后可直接播放运行时生成的测试音，也可以在 Inspector 中绑定自己的 `AudioClip`。场景同时演示播放控制、外部音频加载、播放状态与进度、频谱和输出波形数据。

## 播放来源

### AudioClip

```csharp
player.Play(audioClip);
player.Play(audioClip, () => Debug.Log("播放完成"));
```

### Resources

路径相对于任意 `Resources` 文件夹，不包含扩展名：

```csharp
player.PlayResource("Audio/Guide", () => Debug.Log("播放完成"));
```

### 网络、文件和 StreamingAssets

`PlayExternal` 接收完整地址或相对路径。完整地址直接加载，相对路径自动从 `StreamingAssets` 加载。

```csharp
player.PlayExternal("https://example.com/audio/guide.mp3");
player.PlayExternal("Audio/guide.mp3");
player.PlayExternal(
    "Audio/guide.ogg",
    completedAction: () => Debug.Log("播放完成"),
    failedAction: error => Debug.LogError(error));
```

支持根据 `.mp3`、`.ogg`、`.wav`、`.aif` 和 `.aiff` 扩展名推断 `AudioType`，也可以显式传入类型。

## 播放控制

```csharp
player.Pause();
player.Resume();
player.Stop();
```

重复暂停、非暂停状态下恢复以及重复停止都不会重复改变状态。主动停止、替换音频和组件禁用不会执行自然播放结束回调。

外部应通过 `AudioPlayer` 控制播放，不要直接调用其 `AudioSource` 的 `Play`、`Pause`、`UnPause` 或 `Stop`，否则内部状态、进度和完成回调可能失去同步。

## 状态与时间

```csharp
AudioPlayerState state = player.State;
AudioClip clip = player.CurrentClip;
float progress = player.Progress;
int totalSamples = player.TotalSamples;
int currentSamples = player.CurrentTimeSamples;
double duration = player.Duration;
double currentTime = player.CurrentTime;
```

`State` 的取值为：

- `AudioPlayerState.Stopped`
- `AudioPlayerState.Playing`
- `AudioPlayerState.Paused`

时间数据不读取 `AudioClip.length` 和 `AudioSource.time`：

```text
Duration = AudioClip.samples / AudioClip.frequency
CurrentTime = AudioSource.timeSamples / AudioClip.frequency
Progress = AudioSource.timeSamples / AudioClip.samples
```

`Duration` 表示音频素材时间。修改 `AudioSource.pitch` 会改变实际播放所需时间，但不会改变素材总采样数。

## 播放进度

```csharp
player.ProgressChanged += OnProgressChanged;

private void OnProgressChanged(float progress)
{
    Debug.Log($"播放进度：{progress:P0}");
}
```

进度范围为 `0` 到 `1`。开始播放报告 `0`，自然播放结束报告 `1`，主动停止后重置为 `0`。

## 频谱与波形

频谱和波形采集默认关闭。开启开关并订阅对应事件后才会执行采集。

```csharp
player.SpectrumEnabled = true;
player.SpectrumDataChanged += OnSpectrumDataChanged;

player.OutputDataEnabled = true;
player.OutputDataChanged += OnOutputDataChanged;

private void OnSpectrumDataChanged(float[] spectrum)
{
    // 频率强度数据，适合绘制频谱柱状图。
}

private void OnOutputDataChanged(float[] outputData)
{
    // 实时振幅数据，适合绘制声音波形。
}
```

每种数据使用一个长度为 `512` 的缓冲区。缓冲区延迟创建并重复使用，不会每帧创建新数组。事件中的数组只保证在本次回调期间有效；需要长期保存时应由订阅者自行复制。

频谱 FFT 的计算量高于输出波形采样。不需要可视化时应关闭对应开关并取消订阅。

## AudioSource 配置

可以通过只读属性取得播放器使用的 `AudioSource`：

```csharp
player.AudioSource.volume = 0.8f;
player.AudioSource.outputAudioMixerGroup = mixerGroup;
```

该属性只限制引用不能被替换，不能阻止外部调用 `AudioSource` 的播放方法。建议仅用于配置音量、Mixer、声道、空间音效等参数。

## 暂停游戏

仅设置 `Time.timeScale = 0` 不会暂停 Unity 音频系统。需要暂停全部普通音频时，应同时设置：

```csharp
AudioListener.pause = true;
```

恢复时设置：

```csharp
AudioListener.pause = false;
```

`AudioListener.pause` 期间播放器不会提前报告自然播放结束。将 `AudioSource.ignoreListenerPause` 设置为 `true` 的音频不受该全局暂停控制。

## WebGL

- `StreamingAssets` 通过 `UnityWebRequest` 加载，可用于 WebGL。
- 网络音频与 WebGL 页面同域时可直接请求。
- 跨域地址必须由音频服务器正确配置 CORS。
- 浏览器可能禁止没有用户点击或触摸触发的首次音频播放。
- WebGL 通常需要完整下载音频后才能开始播放，不是真正的边下载边播放。
- 音频格式的最终可用性取决于 Unity WebGL 和目标浏览器的解码支持。

## 事件释放

组件销毁时会清空自身事件引用。对于生命周期长于普通界面的播放器，订阅者仍建议在自身禁用或销毁时取消订阅：

```csharp
private void OnDisable()
{
    player.ProgressChanged -= OnProgressChanged;
    player.SpectrumDataChanged -= OnSpectrumDataChanged;
    player.OutputDataChanged -= OnOutputDataChanged;
}
```

## License

本项目使用 MIT License，详见 [LICENSE.md](LICENSE.md)。

## Inspector 统一依赖

本包声明依赖 `com.cowart.inspector-groups` `3.1.0`，Inspector 样式统一由该基础包提供。安装前须配置 OpenUPM scoped registry（`https://package.openupm.com`，scope `com.cowart`）；UPM 会根据 dependencies 连带解析 Inspector Groups。使用未发布源码时，请同时使用本地 Inspector Groups 3.1.0。业务包之间不互相依赖，Samples 的可选组合不受此限制。
