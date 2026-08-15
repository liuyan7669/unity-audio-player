using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using AudioPlayerComponent = Cowart.AudioPlayer.AudioPlayer;

namespace Cowart.AudioPlayer.Samples
{
    /// <summary>
    /// Audio Player 的 UGUI 示例。未绑定 AudioClip 时会生成一段测试音，无需额外资源。
    /// </summary>
    public sealed class AudioPlayerExample : MonoBehaviour
    {
        private const int GeneratedClipFrequency = 44100;
        private const float GeneratedClipDuration = 6f;

        [Header("Audio")]
        [SerializeField] private AudioClip audioClip;

        [Header("Playback Controls")]
        [SerializeField] private Button playButton;
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button stopButton;

        [Header("External Audio")]
        [SerializeField] private InputField externalPathInput;
        [SerializeField] private Button playExternalButton;

        [Header("State")]
        [SerializeField] private Text stateText;
        [SerializeField] private Text timeText;
        [SerializeField] private Text statusText;
        [SerializeField] private Slider progressSlider;

        [Header("Visualization")]
        [SerializeField] private Toggle spectrumToggle;
        [SerializeField] private Toggle outputToggle;
        [SerializeField] private RectTransform spectrumContainer;
        [SerializeField] private RectTransform outputContainer;
        [SerializeField] private Image[] spectrumBars;
        [SerializeField] private Image[] outputBars;

        private AudioPlayerComponent player;
        private AudioClip generatedClip;
        private string status = "点击“播放示例音频”开始。";
        private Coroutine refreshCoroutine;

        private void Awake()
        {
            if (!ValidateReferences())
            {
                enabled = false;
                return;
            }

            player = GetComponent<AudioPlayerComponent>();
            if (player == null)
            {
                player = gameObject.AddComponent<AudioPlayerComponent>();
            }

            player.AudioSource.playOnAwake = false;
            player.AudioSource.loop = false;
            player.AudioSource.spatialBlend = 0f;

            progressSlider.minValue = 0f;
            progressSlider.maxValue = 1f;
            progressSlider.value = 0f;
            progressSlider.interactable = false;

            playButton.onClick.AddListener(PlaySampleClip);
            pauseButton.onClick.AddListener(Pause);
            resumeButton.onClick.AddListener(Resume);
            stopButton.onClick.AddListener(Stop);
            playExternalButton.onClick.AddListener(PlayExternal);
            spectrumToggle.onValueChanged.AddListener(HandleSpectrumToggleChanged);
            outputToggle.onValueChanged.AddListener(HandleOutputToggleChanged);

            player.SpectrumEnabled = spectrumToggle.isOn;
            player.OutputDataEnabled = outputToggle.isOn;
            RefreshUI();
        }

        private void OnEnable()
        {
            if (player == null)
            {
                return;
            }

            player.ProgressChanged += HandleProgressChanged;
            player.SpectrumDataChanged += HandleSpectrumDataChanged;
            player.OutputDataChanged += HandleOutputDataChanged;
            refreshCoroutine = StartCoroutine(RefreshUILoop());
        }

        private void OnDisable()
        {
            if (player == null)
            {
                return;
            }

            player.ProgressChanged -= HandleProgressChanged;
            player.SpectrumDataChanged -= HandleSpectrumDataChanged;
            player.OutputDataChanged -= HandleOutputDataChanged;
            if (refreshCoroutine != null)
            {
                StopCoroutine(refreshCoroutine);
                refreshCoroutine = null;
            }
        }

        private void OnDestroy()
        {
            if (playButton != null) playButton.onClick.RemoveListener(PlaySampleClip);
            if (pauseButton != null) pauseButton.onClick.RemoveListener(Pause);
            if (resumeButton != null) resumeButton.onClick.RemoveListener(Resume);
            if (stopButton != null) stopButton.onClick.RemoveListener(Stop);
            if (playExternalButton != null) playExternalButton.onClick.RemoveListener(PlayExternal);
            if (spectrumToggle != null) spectrumToggle.onValueChanged.RemoveListener(HandleSpectrumToggleChanged);
            if (outputToggle != null) outputToggle.onValueChanged.RemoveListener(HandleOutputToggleChanged);

            if (generatedClip != null)
            {
                Destroy(generatedClip);
                generatedClip = null;
            }
        }

        public void PlaySampleClip()
        {
            AudioClip clip = audioClip != null ? audioClip : GetOrCreateGeneratedClip();
            status = audioClip != null ? "正在播放 Inspector AudioClip。" : "正在播放运行时生成的测试音。";
            player.Play(clip, () => status = "示例音频自然播放完成，完成回调已触发。");
        }

        private void Pause()
        {
            player.Pause();
            status = "已暂停。";
        }

        private void Resume()
        {
            player.Resume();
            status = "已恢复播放。";
        }

        private void Stop()
        {
            player.Stop();
            status = "已主动停止；不会触发自然结束回调。";
        }

        private void PlayExternal()
        {
            string pathOrUrl = externalPathInput.text;
            if (string.IsNullOrWhiteSpace(pathOrUrl))
            {
                status = "请先输入外部音频地址或路径。";
                return;
            }

            status = "正在加载外部音频……";
            player.PlayExternal(
                pathOrUrl,
                completedAction: () => status = "外部音频自然播放完成。",
                failedAction: error => status = error);
        }

        private void HandleSpectrumToggleChanged(bool isOn)
        {
            player.SpectrumEnabled = isOn;
            if (!isOn)
            {
                ResetBars(spectrumBars);
            }
        }

        private void HandleOutputToggleChanged(bool isOn)
        {
            player.OutputDataEnabled = isOn;
            if (!isOn)
            {
                ResetBars(outputBars);
            }
        }

        private void HandleProgressChanged(float progress)
        {
            progressSlider.value = progress;
        }

        private void HandleSpectrumDataChanged(float[] samples)
        {
            UpdateBars(samples, spectrumBars, spectrumContainer, true);
        }

        private void HandleOutputDataChanged(float[] samples)
        {
            UpdateBars(samples, outputBars, outputContainer, false);
        }

        private IEnumerator RefreshUILoop()
        {
            WaitForSecondsRealtime wait = new WaitForSecondsRealtime(0.1f);
            while (true)
            {
                RefreshUI();
                yield return wait;
            }
        }

        private void RefreshUI()
        {
            AudioClip currentClip = player.CurrentClip;
            stateText.text = string.Format(
                "State：{0}    Clip：{1}    Samples：{2} / {3}",
                player.State,
                currentClip != null ? currentClip.name : "无",
                player.CurrentTimeSamples,
                player.TotalSamples);
            timeText.text = string.Format(
                "Time：{0:0.00} / {1:0.00} 秒    Progress：{2:P1}",
                player.CurrentTime,
                player.Duration,
                player.Progress);
            statusText.text = string.Concat("状态：", status);

            pauseButton.interactable = player.State == AudioPlayerState.Playing;
            resumeButton.interactable = player.State == AudioPlayerState.Paused;
            stopButton.interactable = player.State != AudioPlayerState.Stopped;
            playExternalButton.interactable = !string.IsNullOrWhiteSpace(externalPathInput.text);
        }

        private AudioClip GetOrCreateGeneratedClip()
        {
            if (generatedClip != null)
            {
                return generatedClip;
            }

            int sampleCount = Mathf.RoundToInt(GeneratedClipFrequency * GeneratedClipDuration);
            float[] samples = new float[sampleCount];
            for (int i = 0; i < samples.Length; i++)
            {
                float time = i / (float)GeneratedClipFrequency;
                float fade = Mathf.Min(1f, i / 1024f, (samples.Length - 1 - i) / 1024f);
                samples[i] = (Mathf.Sin(2f * Mathf.PI * 220f * time) * 0.16f +
                              Mathf.Sin(2f * Mathf.PI * 440f * time) * 0.08f) * fade;
            }

            generatedClip = AudioClip.Create(
                "Generated Audio Player Sample",
                sampleCount,
                1,
                GeneratedClipFrequency,
                false);
            if (!generatedClip.SetData(samples, 0))
            {
                status = "生成测试音失败。";
            }

            return generatedClip;
        }

        private static void UpdateBars(
            float[] source,
            Image[] bars,
            RectTransform container,
            bool amplify)
        {
            if (source == null || source.Length == 0)
            {
                return;
            }

            int samplesPerBar = Mathf.Max(1, source.Length / bars.Length);
            float maximumHeight = Mathf.Max(2f, container.rect.height - 4f);
            for (int i = 0; i < bars.Length; i++)
            {
                float value = 0f;
                int start = i * samplesPerBar;
                int end = Mathf.Min(source.Length, start + samplesPerBar);
                for (int j = start; j < end; j++)
                {
                    value = Mathf.Max(value, Mathf.Abs(source[j]));
                }

                if (amplify)
                {
                    value = Mathf.Sqrt(value);
                }

                RectTransform barRect = bars[i].rectTransform;
                Vector2 size = barRect.sizeDelta;
                size.y = Mathf.Max(2f, Mathf.Clamp01(value) * maximumHeight);
                barRect.sizeDelta = size;
            }
        }

        private static void ResetBars(Image[] bars)
        {
            for (int i = 0; i < bars.Length; i++)
            {
                RectTransform barRect = bars[i].rectTransform;
                Vector2 size = barRect.sizeDelta;
                size.y = 2f;
                barRect.sizeDelta = size;
            }
        }

        private bool ValidateReferences()
        {
            bool valid =
                playButton != null &&
                pauseButton != null &&
                resumeButton != null &&
                stopButton != null &&
                externalPathInput != null &&
                playExternalButton != null &&
                stateText != null &&
                timeText != null &&
                statusText != null &&
                progressSlider != null &&
                spectrumToggle != null &&
                outputToggle != null &&
                spectrumContainer != null &&
                outputContainer != null &&
                spectrumBars != null &&
                spectrumBars.Length > 0 &&
                outputBars != null &&
                outputBars.Length > 0;

            if (!valid)
            {
                Debug.LogError("[AudioPlayerExample] UGUI 引用未完整绑定，请重新导入 Sample。", this);
            }

            return valid;
        }
    }
}
