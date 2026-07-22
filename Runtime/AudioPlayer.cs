using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace Cowart.AudioPlayer
{
    /// <summary>AudioPlayer 当前的播放状态。</summary>
    public enum AudioPlayerState
    {
        /// <summary>当前没有正在播放或暂停的音频。</summary>
        Stopped,

        /// <summary>当前音频正在播放。</summary>
        Playing,

        /// <summary>当前音频已由 AudioPlayer 暂停。</summary>
        Paused
    }

    /// <summary>负责当前物体上的音频播放、停止和自然播放结束检测。</summary>
    [AddComponentMenu("")]
    public sealed class AudioPlayer : MonoBehaviour
    {
        /// <summary>每次采集的频谱和波形样本数量；必须是 2 的幂。</summary>
        private const int VisualizationSampleCount = 512;

        /// <summary>由当前播放器独占控制的 Unity 音频源。</summary>
        private AudioSource audioSource;

        /// <summary>当前正在追踪的音频剪辑。</summary>
        private AudioClip trackedClip;

        /// <summary>音频自然播放结束后的回调。</summary>
        private Action onCompleted;

        /// <summary>当前播放器内部状态。</summary>
        private AudioPlayerState playbackState;

        /// <summary>上一帧读取到的 PCM 采样位置。</summary>
        private int previousTimeSamples;

        /// <summary>播放开始后采样位置是否已经向前推进。</summary>
        private bool samplesAdvanced;

        /// <summary>未发生暂停时预计结束播放的 DSP 时间。</summary>
        private double expectedEndDspTime;

        /// <summary>执行局部暂停时记录的 DSP 时间。</summary>
        private double pausedAtDspTime;

        /// <summary>当前正在执行的外部音频加载请求。</summary>
        private UnityWebRequest activeLoadRequest;

        /// <summary>用于废弃旧加载结果的请求版本号。</summary>
        private int loadVersion;

        /// <summary>当前归一化播放进度，取值范围为 0 到 1。</summary>
        private float progress;

        /// <summary>是否启用频谱数据采集和事件。</summary>
        [SerializeField] private bool spectrumEnabled;

        /// <summary>是否启用输出波形数据采集和事件。</summary>
        [SerializeField] private bool outputDataEnabled;

        /// <summary>重复使用的频谱数据缓冲区，避免每帧分配内存。</summary>
        private float[] spectrumBuffer;

        /// <summary>重复使用的输出波形缓冲区，避免每帧分配内存。</summary>
        private float[] outputDataBuffer;

        /// <summary>播放进度发生变化时触发，参数为 0 到 1 的归一化进度。</summary>
        public event Action<float> ProgressChanged;

        /// <summary>频谱数据更新时触发；数组由播放器重复使用，订阅者需要长期保存时应自行复制。</summary>
        public event Action<float[]> SpectrumDataChanged;

        /// <summary>输出波形数据更新时触发；数组由播放器重复使用，订阅者需要长期保存时应自行复制。</summary>
        public event Action<float[]> OutputDataChanged;

        /// <summary>获取当前由 AudioPlayer 维护的播放状态。</summary>
        public AudioPlayerState State => playbackState;

        /// <summary>获取当前归一化播放进度，取值范围为 0 到 1。</summary>
        public float Progress => progress;

        /// <summary>获取当前播放或暂停的音频剪辑；没有音频时返回 null。</summary>
        public AudioClip CurrentClip => trackedClip;

        /// <summary>获取当前音频的总 PCM 采样数；没有音频时返回 0。</summary>
        public int TotalSamples => trackedClip != null ? trackedClip.samples : 0;

        /// <summary>获取当前音频的 PCM 采样位置；没有音频时返回 0。</summary>
        public int CurrentTimeSamples => audioSource != null && trackedClip != null
            ? audioSource.timeSamples
            : 0;

        /// <summary>
        /// 获取当前音频的总时长（秒），使用 samples / frequency 计算，
        /// 不读取 AudioClip.length。
        /// </summary>
        public double Duration => trackedClip != null && trackedClip.frequency > 0
            ? trackedClip.samples / (double)trackedClip.frequency
            : 0d;

        /// <summary>
        /// 获取当前音频位置（秒），使用 timeSamples / frequency 计算，
        /// 不读取 AudioSource.time。
        /// </summary>
        public double CurrentTime => trackedClip != null && trackedClip.frequency > 0
            ? CurrentTimeSamples / (double)trackedClip.frequency
            : 0d;

        /// <summary>
        /// 获取当前播放器使用的 AudioSource，不存在时自动创建。
        /// 外部可读取或配置音量、Mixer 等参数，但不应直接调用 Play、Pause、UnPause 或 Stop，
        /// 否则 AudioPlayer 维护的状态和回调可能失去同步。
        /// </summary>
        public AudioSource AudioSource => GetAudioSource();

        /// <summary>获取或设置是否采集并发送频谱数据，默认关闭。</summary>
        public bool SpectrumEnabled
        {
            get => spectrumEnabled;
            set => spectrumEnabled = value;
        }

        /// <summary>获取或设置是否采集并发送输出波形数据，默认关闭。</summary>
        public bool OutputDataEnabled
        {
            get => outputDataEnabled;
            set => outputDataEnabled = value;
        }

        /// <summary>播放指定音频。</summary>
        public void Play(AudioClip clip, Action completedAction = null)
        {
            if (clip == null)
            {
                Debug.LogWarning($"{name} 播放失败：AudioClip 为空。", this);
                return;
            }

            AudioSource source = GetAudioSource();
            source.Stop();
            source.clip = clip;
            source.timeSamples = 0;

            trackedClip = clip;
            onCompleted = completedAction;
            playbackState = AudioPlayerState.Playing;
            previousTimeSamples = 0;
            samplesAdvanced = false;
            pausedAtDspTime = 0d;
            expectedEndDspTime = AudioSettings.dspTime + GetPlaybackDuration(clip, source.pitch);

            source.Play();
            SetProgress(0f, true);
        }

        /// <summary>加载并播放 Resources 目录下的音频。</summary>
        /// <param name="resourcePath">相对于 Resources 目录的路径，不包含扩展名。</param>
        public void PlayResource(string resourcePath, Action completedAction = null)
        {
            AudioClip clip = Resources.Load<AudioClip>(resourcePath);
            if (clip == null)
            {
                Debug.LogWarning($"{name} 播放失败：Resources 中未找到音频 {resourcePath}。", this);
                return;
            }

            Play(clip, completedAction);
        }

        /// <summary>异步加载并播放外部音频；完整地址直接使用，相对路径从 StreamingAssets 加载。</summary>
        /// <param name="pathOrUrl">网络、本地文件完整地址，或相对于 StreamingAssets 的路径。</param>
        /// <param name="audioType">音频类型；传入 Unknown 时根据文件扩展名推断。</param>
        /// <param name="completedAction">音频自然播放结束后的回调。</param>
        /// <param name="failedAction">加载失败时接收错误信息的回调。</param>
        public void PlayExternal(string pathOrUrl, AudioType audioType = AudioType.UNKNOWN,
            Action completedAction = null, Action<string> failedAction = null)
        {
            if (string.IsNullOrWhiteSpace(pathOrUrl))
            {
                ReportLoadFailure("外部音频地址为空。", failedAction);
                return;
            }

            string url = pathOrUrl;
            if (!IsAbsoluteAddress(pathOrUrl))
            {
                url = $"{Application.streamingAssetsPath.TrimEnd('/', '\\')}/{pathOrUrl.TrimStart('/', '\\')}";
            }

            StartExternalLoad(NormalizeUrl(url), audioType, completedAction, failedAction);
        }

        /// <summary>暂停当前音频；重复暂停不会改变状态。</summary>
        public void Pause()
        {
            if (playbackState != AudioPlayerState.Playing || audioSource == null)
            {
                return;
            }

            audioSource.Pause();
            pausedAtDspTime = AudioSettings.dspTime;
            playbackState = AudioPlayerState.Paused;
        }

        /// <summary>从暂停位置继续播放；非暂停状态下调用不会产生效果。</summary>
        public void Resume()
        {
            if (playbackState != AudioPlayerState.Paused || audioSource == null)
            {
                return;
            }

            expectedEndDspTime += AudioSettings.dspTime - pausedAtDspTime;
            pausedAtDspTime = 0d;
            playbackState = AudioPlayerState.Playing;
            audioSource.UnPause();
        }

        /// <summary>停止播放并取消完成回调。</summary>
        public void Stop()
        {
            CancelExternalLoad();

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }

            if (audioSource == null)
            {
                ResetPlayback();
                return;
            }

            audioSource.Stop();
            audioSource.clip = null;
            ResetPlayback();
        }

        private void Update()
        {
            if (playbackState != AudioPlayerState.Playing || audioSource == null || trackedClip == null)
            {
                return;
            }

            if (audioSource.clip != trackedClip)
            {
                ResetPlayback();
                return;
            }

            if (AudioListener.pause && !audioSource.ignoreListenerPause)
            {
                return;
            }

            int currentTimeSamples = audioSource.timeSamples;
            bool reachedSampleEnd = trackedClip.samples > 0 && currentTimeSamples >= trackedClip.samples - 1;
            bool samplesWrapped = samplesAdvanced && currentTimeSamples < previousTimeSamples;

            if (currentTimeSamples > previousTimeSamples)
            {
                samplesAdvanced = true;
            }

            previousTimeSamples = currentTimeSamples;
            UpdateVisualizationData();

            bool completed = !audioSource.loop
                && (reachedSampleEnd || samplesWrapped || AudioSettings.dspTime >= expectedEndDspTime);
            if (!completed)
            {
                SetProgress(trackedClip.samples > 0
                    ? currentTimeSamples / (float)trackedClip.samples
                    : 0f);
                return;
            }

            Action completedAction = onCompleted;
            SetProgress(1f);
            audioSource.Stop();
            audioSource.clip = null;
            ResetPlayback(false);
            completedAction?.Invoke();
        }

        private void OnDisable()
        {
            Stop();
        }

        /// <summary>组件销毁时释放对所有外部事件订阅者的引用。</summary>
        private void OnDestroy()
        {
            ProgressChanged = null;
            SpectrumDataChanged = null;
            OutputDataChanged = null;
        }

        private AudioSource GetAudioSource()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }

            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }

            return audioSource;
        }

        /// <summary>根据开关和订阅情况采集频谱与输出波形数据。</summary>
        private void UpdateVisualizationData()
        {
            if (spectrumEnabled && SpectrumDataChanged != null)
            {
                if (spectrumBuffer == null)
                {
                    spectrumBuffer = new float[VisualizationSampleCount];
                }

                audioSource.GetSpectrumData(spectrumBuffer, 0, FFTWindow.BlackmanHarris);
                SpectrumDataChanged.Invoke(spectrumBuffer);
            }

            if (outputDataEnabled && OutputDataChanged != null)
            {
                if (outputDataBuffer == null)
                {
                    outputDataBuffer = new float[VisualizationSampleCount];
                }

                audioSource.GetOutputData(outputDataBuffer, 0);
                OutputDataChanged.Invoke(outputDataBuffer);
            }
        }

        /// <summary>取消旧请求并开始加载新的外部音频。</summary>
        private void StartExternalLoad(string url, AudioType audioType, Action completedAction,
            Action<string> failedAction)
        {
            Stop();
            int version = ++loadVersion;
            StartCoroutine(LoadAndPlay(url, ResolveAudioType(url, audioType), completedAction, failedAction, version));
        }

        /// <summary>通过 UnityWebRequest 加载音频，成功后交给统一播放入口。</summary>
        private IEnumerator LoadAndPlay(string url, AudioType audioType, Action completedAction,
            Action<string> failedAction, int version)
        {
            using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(url, audioType))
            {
                activeLoadRequest = request;
                yield return request.SendWebRequest();

                if (version != loadVersion)
                {
                    yield break;
                }

                activeLoadRequest = null;
                if (request.result != UnityWebRequest.Result.Success)
                {
                    ReportLoadFailure($"音频加载失败：{request.error}，地址：{url}", failedAction);
                    yield break;
                }

                AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
                if (clip == null)
                {
                    ReportLoadFailure($"音频加载失败：未能解析音频数据，地址：{url}", failedAction);
                    yield break;
                }

                Play(clip, completedAction);
            }
        }

        /// <summary>取消当前外部音频加载请求，使其结果不再生效。</summary>
        private void CancelExternalLoad()
        {
            loadVersion++;
            if (activeLoadRequest == null)
            {
                return;
            }

            activeLoadRequest.Abort();
            activeLoadRequest = null;
        }

        /// <summary>将本地绝对路径转换为 UnityWebRequest 可识别的 file 地址。</summary>
        private static string NormalizeUrl(string pathOrUrl)
        {
            if (pathOrUrl.Contains("://") || pathOrUrl.StartsWith("jar:", StringComparison.OrdinalIgnoreCase))
            {
                return pathOrUrl.Replace('\\', '/');
            }

            if (Path.IsPathRooted(pathOrUrl))
            {
                return new Uri(pathOrUrl).AbsoluteUri;
            }

            return pathOrUrl.Replace('\\', '/');
        }

        /// <summary>判断路径是否已经是网络地址、平台地址或本地绝对路径。</summary>
        private static bool IsAbsoluteAddress(string pathOrUrl)
        {
            return pathOrUrl.Contains("://")
                || pathOrUrl.StartsWith("jar:", StringComparison.OrdinalIgnoreCase)
                || Path.IsPathRooted(pathOrUrl);
        }

        /// <summary>根据地址扩展名推断常用音频类型。</summary>
        private static AudioType ResolveAudioType(string url, AudioType audioType)
        {
            if (audioType != AudioType.UNKNOWN)
            {
                return audioType;
            }

            string path = url.Split('?')[0];
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".mp3":
                    return AudioType.MPEG;
                case ".ogg":
                    return AudioType.OGGVORBIS;
                case ".wav":
                    return AudioType.WAV;
                case ".aif":
                case ".aiff":
                    return AudioType.AIFF;
                default:
                    return AudioType.UNKNOWN;
            }
        }

        /// <summary>向调用方报告加载错误；未提供回调时输出警告。</summary>
        private void ReportLoadFailure(string message, Action<string> failedAction)
        {
            if (failedAction != null)
            {
                failedAction.Invoke(message);
                return;
            }

            Debug.LogWarning($"{name} {message}", this);
        }

        /// <summary>根据采样数量、采样率和播放速度计算预计播放时长。</summary>
        private static double GetPlaybackDuration(AudioClip clip, float pitch)
        {
            double playbackSpeed = Math.Max(Math.Abs(pitch), 0.01f);
            return clip.samples / (double)clip.frequency / playbackSpeed;
        }

        /// <summary>清除内部播放状态和完成回调。</summary>
        private void ResetPlayback(bool resetProgress = true)
        {
            trackedClip = null;
            onCompleted = null;
            playbackState = AudioPlayerState.Stopped;
            previousTimeSamples = 0;
            samplesAdvanced = false;
            expectedEndDspTime = 0d;
            pausedAtDspTime = 0d;

            if (resetProgress)
            {
                SetProgress(0f);
            }
        }

        /// <summary>更新播放进度，并仅在值变化时通知外部订阅者。</summary>
        private void SetProgress(float value, bool forceNotify = false)
        {
            value = Mathf.Clamp01(value);
            if (!forceNotify && Mathf.Approximately(progress, value))
            {
                return;
            }

            progress = value;
            ProgressChanged?.Invoke(progress);
        }
    }
}
