using VRC.SDKBase;
using VRC.Udon;
using UdonSharp;
using UnityEngine;
using UnityEngine.Serialization;
using TMPro;


public class LRCLyricControllerTwoLanguage : UdonSharpBehaviour
{
    [Header("基本设置")]
    public TextAsset lrcFile;
    public TextAsset secondLanguageLrcFile; // 第二语言LRC文件（可选）
    public TextMeshProUGUI lyricText;
    public float timeOffset = 0f;
    [Range(0.1f, 1f)] public float fadeDuration = 0.3f;

    [Header("音频控制")]
    public AudioSource audioSource;
    public GameObject audioSourceObject;

    [Header("音乐结束设置")]
    public GameObject[] objectsToDisableOnMusicEnd;

    [Header("指定时间天空盒切换")]
    [FormerlySerializedAs("firstSkyboxObject")]
    public GameObject 第一个天空盒对象;
    [FormerlySerializedAs("secondSkyboxObject")]
    public GameObject 第二个天空盒对象;
    [FormerlySerializedAs("resetToFirstSkyboxOnEnable")]
    public bool 启用时恢复第一个天空盒 = false;
    [FormerlySerializedAs("autoBlackScreenTransitionOnEnable")]
    public bool 启用后自动黑屏切换 = true;
    [FormerlySerializedAs("skipBlackScreenWhenSecondSkyboxActive")]
    public bool 第二个天空盒已显示时跳过黑屏 = true;
    [FormerlySerializedAs("secondsBeforeBlackScreen")]
    public float 启用后多少秒黑屏 = 120f;
    [FormerlySerializedAs("fadeToBlackDuration")]
    public float 渐变到黑屏时间 = 1f;
    [FormerlySerializedAs("blackHoldDuration")]
    public float 黑屏停留时间 = 0.35f;
    [FormerlySerializedAs("fadeFromBlackDuration")]
    public float 黑屏淡出时间 = 1f;

    [Header("黑屏淡入物体")]
    [FormerlySerializedAs("screenFadeObject")]
    public GameObject 黑屏物体;
    [FormerlySerializedAs("screenFadeRenderer")]
    public Renderer 黑屏渲染器;
    [FormerlySerializedAs("followLocalHead")]
    public bool 跟随本地玩家头部 = true;
    [FormerlySerializedAs("screenFadeLocalOffset")]
    public Vector3 黑屏本地偏移 = Vector3.zero;

    private GameObject firstSkyboxObject;
    private GameObject secondSkyboxObject;
    private bool resetToFirstSkyboxOnEnable;
    private bool autoBlackScreenTransitionOnEnable;
    private bool skipBlackScreenWhenSecondSkyboxActive;
    private float secondsBeforeBlackScreen;
    private float fadeToBlackDuration;
    private float blackHoldDuration;
    private float fadeFromBlackDuration;
    private GameObject screenFadeObject;
    private Renderer screenFadeRenderer;
    private bool followLocalHead;
    private Vector3 screenFadeLocalOffset;

    private float[] lyricTimes;
    private string[] lyricTexts;
    private string[] secondLanguageTexts; // 第二语言歌词文本
    private int lyricCount;
    private int currentLyricIndex = -1;
    private int targetLyricIndex = -1;
    private float fadeProgress = 0f;
    private bool isFadingOut = false;
    private bool isFadingIn = false;
    private Color originalColor;
    private VRCPlayerApi localPlayer;
    private bool isSkyboxTransitioning = false;
    private int skyboxTransitionPhase = 0;
    private float skyboxTransitionTimer = 0f;
    private bool hasTriggeredBlackScreenTransition = false;
    private float enabledTimer = 0f;
    private bool wasAudioPlaying = false;

    private const int SkyboxPhaseFadeToBlack = 1;
    private const int SkyboxPhaseHoldBlack = 2;
    private const int SkyboxPhaseFadeFromBlack = 3;

    private void Start()
    {
        originalColor = lyricText.color;
        localPlayer = Networking.LocalPlayer;
        SyncInspectorFields();
        SetScreenFadeAlpha(0f);
        ParseLRCFile();
    }

    private void SyncInspectorFields()
    {
        firstSkyboxObject = 第一个天空盒对象;
        secondSkyboxObject = 第二个天空盒对象;
        resetToFirstSkyboxOnEnable = 启用时恢复第一个天空盒;
        autoBlackScreenTransitionOnEnable = 启用后自动黑屏切换;
        skipBlackScreenWhenSecondSkyboxActive = 第二个天空盒已显示时跳过黑屏;
        secondsBeforeBlackScreen = 启用后多少秒黑屏;
        fadeToBlackDuration = 渐变到黑屏时间;
        blackHoldDuration = 黑屏停留时间;
        fadeFromBlackDuration = 黑屏淡出时间;
        screenFadeObject = 黑屏物体;
        screenFadeRenderer = 黑屏渲染器;
        followLocalHead = 跟随本地玩家头部;
        screenFadeLocalOffset = 黑屏本地偏移;
    }

    private void ParseLRCFile()
    {
        if (lrcFile == null) return;

        string[] lines = lrcFile.text.Split('\n');
        lyricTimes = new float[lines.Length];
        lyricTexts = new string[lines.Length];
        secondLanguageTexts = new string[lines.Length];
        lyricCount = 0;

        // 解析主语言文件
        ParseMainLanguage(lines);
        
        // 解析第二语言文件（如果存在）
        if (secondLanguageLrcFile != null)
        {
            ParseSecondLanguage();
        }
    }

    private void ParseMainLanguage(string[] lines)
    {
        for (int i = 0; i < lines.Length; ++i)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            // 查找时间标记
            int timeStart = line.IndexOf('[');
            int timeEnd = line.IndexOf(']');
            if (timeStart == -1 || timeEnd == -1) continue;

            // 提取时间部分
            string timePart = line.Substring(timeStart + 1, timeEnd - timeStart - 1);
            string[] timeComponents = timePart.Split(':');
            if (timeComponents.Length != 2) continue;

            // 解析时间戳
            if (TryParseTime(timeComponents[0], timeComponents[1], out float timestamp))
            {
                lyricTimes[lyricCount] = timestamp;
                
                // 提取歌词文本部分
                string lyricContent = line.Substring(timeEnd + 1).Trim();
                
                // 如果没有第二语言文件，使用原有的双语检测逻辑
                if (secondLanguageLrcFile == null)
                {
                    lyricTexts[lyricCount] = ProcessBilingualLyric(lyricContent);
                }
                else
                {
                    lyricTexts[lyricCount] = lyricContent;
                }
                
                secondLanguageTexts[lyricCount] = ""; // 初始化为空
                lyricCount++;
            }
        }
    }

    private void ParseSecondLanguage()
    {
        string[] secondLines = secondLanguageLrcFile.text.Split('\n');
        
        for (int i = 0; i < secondLines.Length; ++i)
        {
            string line = secondLines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            // 查找时间标记
            int timeStart = line.IndexOf('[');
            int timeEnd = line.IndexOf(']');
            if (timeStart == -1 || timeEnd == -1) continue;

            // 提取时间部分
            string timePart = line.Substring(timeStart + 1, timeEnd - timeStart - 1);
            string[] timeComponents = timePart.Split(':');
            if (timeComponents.Length != 2) continue;

            // 解析时间戳
            if (TryParseTime(timeComponents[0], timeComponents[1], out float timestamp))
            {
                // 查找匹配的时间戳在主语言中的位置
                int matchingIndex = FindMatchingTimeIndex(timestamp);
                if (matchingIndex != -1)
                {
                    // 提取第二语言歌词文本
                    string secondLyricContent = line.Substring(timeEnd + 1).Trim();
                    secondLanguageTexts[matchingIndex] = secondLyricContent;
                }
            }
        }
    }

    private int FindMatchingTimeIndex(float targetTime)
    {
        const float tolerance = 0.1f; // 允许0.1秒的误差
        
        for (int i = 0; i < lyricCount; i++)
        {
            if (Mathf.Abs(lyricTimes[i] - targetTime) <= tolerance)
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>
    /// 处理双语歌词文本，支持日中混合显示
    /// </summary>
    /// <param name="originalText">原始歌词文本</param>
    /// <returns>处理后的歌词文本</returns>
    private string ProcessBilingualLyric(string originalText)
    {
        if (string.IsNullOrEmpty(originalText))
            return "";

        // 如果歌词中既包含日文又包含中文，则认为是双语歌词
        bool hasJapanese = ContainsJapanese(originalText);
        bool hasChinese = ContainsChinese(originalText);
        
                 if (hasJapanese && hasChinese)
         {
             // 双语歌词：保持原格式，但可以添加换行使显示更美观
             // 这里简单地在日文和中文之间添加换行符
             string processed = SeparateJapaneseAndChinese(originalText);
             return processed;
         }
        
        // 单语歌词：直接返回
        return originalText;
    }

    /// <summary>
    /// 检查文本是否包含日文字符
    /// </summary>
    private bool ContainsJapanese(string text)
    {
        foreach (char c in text)
        {
            // 平假名：0x3040-0x309F
            // 片假名：0x30A0-0x30FF  
            // 日文汉字等：0x4E00-0x9FAF (CJK统一汉字，但结合上下文判断)
            if ((c >= 0x3040 && c <= 0x309F) || (c >= 0x30A0 && c <= 0x30FF))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 检查文本是否包含中文字符
    /// </summary>
    private bool ContainsChinese(string text)
    {
        foreach (char c in text)
        {
            // 中文字符范围（简化判断）
            if (c >= 0x4E00 && c <= 0x9FAF)
            {
                // 进一步检查是否为常见中文字符
                if (IsCommonChineseChar(c))
                {
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// 检查是否为常见中文字符（简单判断）
    /// </summary>
    private bool IsCommonChineseChar(char c)
    {
        // 这里可以添加更精确的中文字符判断
        // 暂时使用简单的范围判断
        return c >= 0x4E00 && c <= 0x9FAF;
    }

    /// <summary>
    /// 分离日文和中文，用换行符连接
    /// </summary>
    private string SeparateJapaneseAndChinese(string text)
    {
        string japanese = "";
        string chinese = "";
        string current = "";
        bool inJapanese = false;
        
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            
            // 判断当前字符类型
            bool isJapaneseChar = (c >= 0x3040 && c <= 0x309F) || (c >= 0x30A0 && c <= 0x30FF);
            bool isChineseChar = (c >= 0x4E00 && c <= 0x9FAF) && IsCommonChineseChar(c);
            bool isSpace = char.IsWhiteSpace(c);
            
            if (isJapaneseChar)
            {
                if (!inJapanese && !string.IsNullOrEmpty(current))
                {
                    chinese += current.Trim() + " ";
                    current = "";
                }
                inJapanese = true;
                current += c;
            }
            else if (isChineseChar)
            {
                if (inJapanese && !string.IsNullOrEmpty(current))
                {
                    japanese += current.Trim() + " ";
                    current = "";
                }
                inJapanese = false;
                current += c;
            }
            else if (isSpace)
            {
                // 遇到空格，结束当前片段
                if (!string.IsNullOrEmpty(current))
                {
                    if (inJapanese)
                    {
                        japanese += current.Trim() + " ";
                    }
                    else
                    {
                        chinese += current.Trim() + " ";
                    }
                    current = "";
                }
            }
            else
            {
                // 其他字符（如标点符号）跟随当前语言
                current += c;
            }
        }
        
        // 处理最后的片段
        if (!string.IsNullOrEmpty(current))
        {
            if (inJapanese)
            {
                japanese += current.Trim();
            }
            else
            {
                chinese += current.Trim();
            }
        }
        
        // 组合结果
        japanese = japanese.Trim();
        chinese = chinese.Trim();
        
        if (!string.IsNullOrEmpty(japanese) && !string.IsNullOrEmpty(chinese))
        {
            return japanese + "\n" + chinese;
        }
        else if (!string.IsNullOrEmpty(japanese))
        {
            return japanese;
        }
        else if (!string.IsNullOrEmpty(chinese))
        {
            return chinese;
        }
        
        return text; // fallback到原文本
    }

    private bool TryParseTime(string minutesStr, string secondsStr, out float result)
    {
        result = 0f;
        int minutes;
        float seconds;

        if (!int.TryParse(minutesStr, out minutes))
            return false;

        string[] secondsParts = secondsStr.Split('.');
        if (secondsParts.Length > 2)
            return false;

        if (!int.TryParse(secondsParts[0], out int wholeSeconds))
            return false;

        int milliseconds = 0;
        if (secondsParts.Length == 2)
        {
            if (!int.TryParse(secondsParts[1], out milliseconds))
            {
                result = 0f;
                return false;
            }
            milliseconds = (int)(milliseconds * Mathf.Pow(10, 2 - secondsParts[1].Length));
        }

        seconds = wholeSeconds + milliseconds / 100f;
        result = minutes * 60 + seconds;
        return true;
    }

    void Update()
    {
        UpdateScreenFadeTransform();

        if (autoBlackScreenTransitionOnEnable && !hasTriggeredBlackScreenTransition && !isSkyboxTransitioning)
        {
            if (ShouldSkipBlackScreenTransition())
            {
                hasTriggeredBlackScreenTransition = true;
            }
            else
            {
                enabledTimer += Time.deltaTime;

                if (enabledTimer >= Mathf.Max(0f, secondsBeforeBlackScreen))
                {
                    StartBlackScreenTransition();
                }
            }
        }

        if (audioSource != null && audioSource.isPlaying)
        {
            float currentTime = audioSource.time + timeOffset;
            UpdateLyricDisplay(currentTime);
            HandleFadeEffects();
        }

        // 检测音乐播放完毕，关闭指定对象
        if (audioSource != null && wasAudioPlaying && !audioSource.isPlaying)
        {
            OnMusicEnd();
        }
        wasAudioPlaying = audioSource != null && audioSource.isPlaying;

        HandleSkyboxTransition();
    }

    private void UpdateLyricDisplay(float currentTime)
    {
        int newTargetIndex = -1;
        for (int i = 0; i < lyricCount; ++i)
        {
            if (currentTime >= lyricTimes[i])
            {
                newTargetIndex = i;
            }
        }

        if (newTargetIndex != targetLyricIndex)
        {
            targetLyricIndex = newTargetIndex;

            if (currentLyricIndex != -1 && targetLyricIndex != currentLyricIndex)
            {
                StartFadeOut();
            }
            else
            {
                StartFadeIn();
                // 组合主语言和第二语言文本
                lyricText.text = CombineLyricTexts(targetLyricIndex);
                currentLyricIndex = targetLyricIndex;
            }
        }
    }

    private string CombineLyricTexts(int index)
    {
        if (index < 0 || index >= lyricCount) return "";
        
        string mainText = lyricTexts[index];
        string secondText = secondLanguageTexts[index];
        
        // 如果有第二语言文本，则组合显示
        if (!string.IsNullOrEmpty(secondText))
        {
            return mainText + "\n" + secondText;
        }
        
        // 否则只显示主语言文本
        return mainText;
    }

    private void StartFadeOut()
    {
        isFadingOut = true;
        fadeProgress = 0f;
    }

    private void StartFadeIn()
    {
        isFadingIn = true;
        fadeProgress = 0f;
        lyricText.text = CombineLyricTexts(targetLyricIndex);
        UpdateAlpha(0f);
    }

    private void HandleFadeEffects()
    {
        if (isFadingOut)
        {
            fadeProgress += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, fadeProgress / fadeDuration);
            UpdateAlpha(alpha);

            if (fadeProgress >= fadeDuration)
            {
                isFadingOut = false;
                StartFadeIn();
                currentLyricIndex = targetLyricIndex;
            }
        }
        else if (isFadingIn)
        {
            fadeProgress += Time.deltaTime;
            float alpha = Mathf.Lerp(0f, 1f, fadeProgress / fadeDuration);
            UpdateAlpha(alpha);

            if (fadeProgress >= fadeDuration)
            {
                isFadingIn = false;
            }
        }
    }

    private void UpdateAlpha(float alpha)
    {
        Color newColor = originalColor;
        newColor.a = alpha;
        lyricText.color = newColor;
    }

    public void StartBlackScreenTransition()
    {
        if (hasTriggeredBlackScreenTransition || isSkyboxTransitioning) return;

        if (ShouldSkipBlackScreenTransition())
        {
            hasTriggeredBlackScreenTransition = true;
            return;
        }

        hasTriggeredBlackScreenTransition = true;
        isSkyboxTransitioning = true;
        skyboxTransitionPhase = SkyboxPhaseFadeToBlack;
        skyboxTransitionTimer = 0f;

        SetScreenFadeAlpha(0f);

        if (screenFadeObject != null)
        {
            screenFadeObject.SetActive(true);
        }
    }

    private void HandleSkyboxTransition()
    {
        if (!isSkyboxTransitioning) return;

        skyboxTransitionTimer += Time.deltaTime;

        if (skyboxTransitionPhase == SkyboxPhaseFadeToBlack)
        {
            float duration = Mathf.Max(0.01f, fadeToBlackDuration);
            float progress = Mathf.Clamp01(skyboxTransitionTimer / duration);
            SetScreenFadeAlpha(Mathf.SmoothStep(0f, 1f, progress));

            if (skyboxTransitionTimer >= duration)
            {
                SetScreenFadeAlpha(1f);
                ApplyEndSkybox();
                skyboxTransitionPhase = SkyboxPhaseHoldBlack;
                skyboxTransitionTimer = 0f;
            }
        }
        else if (skyboxTransitionPhase == SkyboxPhaseHoldBlack)
        {
            SetScreenFadeAlpha(1f);

            if (skyboxTransitionTimer >= blackHoldDuration)
            {
                skyboxTransitionPhase = SkyboxPhaseFadeFromBlack;
                skyboxTransitionTimer = 0f;
            }
        }
        else if (skyboxTransitionPhase == SkyboxPhaseFadeFromBlack)
        {
            float duration = Mathf.Max(0.01f, fadeFromBlackDuration);
            float progress = Mathf.Clamp01(skyboxTransitionTimer / duration);
            SetScreenFadeAlpha(1f - Mathf.SmoothStep(0f, 1f, progress));

            if (skyboxTransitionTimer >= duration)
            {
                SetScreenFadeAlpha(0f);
                isSkyboxTransitioning = false;
                skyboxTransitionPhase = 0;

                if (screenFadeObject != null)
                {
                    screenFadeObject.SetActive(false);
                }
            }
        }
    }

    private void ApplyEndSkybox()
    {
        if (firstSkyboxObject != null)
        {
            firstSkyboxObject.SetActive(false);
        }

        if (secondSkyboxObject != null)
        {
            secondSkyboxObject.SetActive(true);
        }
    }

    private bool ShouldSkipBlackScreenTransition()
    {
        return skipBlackScreenWhenSecondSkyboxActive && secondSkyboxObject != null && secondSkyboxObject.activeSelf;
    }

    private void ResetSkyboxObjects()
    {
        if (firstSkyboxObject != null)
        {
            firstSkyboxObject.SetActive(true);
        }

        if (secondSkyboxObject != null)
        {
            secondSkyboxObject.SetActive(false);
        }
    }

    private void SetScreenFadeAlpha(float alpha)
    {
        if (screenFadeRenderer == null) return;

        Color fadeColor = screenFadeRenderer.material.color;
        fadeColor.a = Mathf.Clamp01(alpha);
        screenFadeRenderer.material.color = fadeColor;
    }

    private void UpdateScreenFadeTransform()
    {
        if (!followLocalHead || screenFadeObject == null || localPlayer == null) return;

        VRCPlayerApi.TrackingData headData = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
        screenFadeObject.transform.SetPositionAndRotation(headData.position + headData.rotation * screenFadeLocalOffset, headData.rotation);
    }

    private void OnEnable()
    {
        SyncInspectorFields();
        isSkyboxTransitioning = false;
        skyboxTransitionPhase = 0;
        skyboxTransitionTimer = 0f;
        hasTriggeredBlackScreenTransition = false;
        enabledTimer = 0f;
        SetScreenFadeAlpha(0f);

        if (screenFadeObject != null)
            screenFadeObject.SetActive(false);

        if (resetToFirstSkyboxOnEnable)
            ResetSkyboxObjects();

        if (audioSourceObject != null)
            audioSourceObject.SetActive(true);

        if (audioSource != null && !audioSource.isPlaying)
            audioSource.Play();

        wasAudioPlaying = audioSource != null && audioSource.isPlaying;
    }

    private void OnDisable()
    {
        isFadingOut = false;
        isFadingIn = false;
        isSkyboxTransitioning = false;
        skyboxTransitionPhase = 0;
        UpdateAlpha(1f);
        SetScreenFadeAlpha(0f);

        if (screenFadeObject != null)
            screenFadeObject.SetActive(false);

        if (audioSourceObject != null)
            audioSourceObject.SetActive(false);

        if (audioSource != null && audioSource.isPlaying)
            audioSource.Stop();

        wasAudioPlaying = false;
        lyricText.text = "";
    }

    /// <summary>
    /// 音乐播放完毕时调用，关闭指定的游戏对象
    /// </summary>
    private void OnMusicEnd()
    {
        if (objectsToDisableOnMusicEnd == null) return;

        foreach (GameObject obj in objectsToDisableOnMusicEnd)
        {
            if (obj != null)
            {
                obj.SetActive(false);
            }
        }
    }
}