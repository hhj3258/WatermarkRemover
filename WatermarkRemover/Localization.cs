using System.Reflection;
using System.Text.Json;

namespace WatermarkRemover;

/// <summary>
/// 다국어 문자열 로더. 언어별 JSON(Locales/*.json)을 임베디드 리소스로 읽어 키→문자열로 제공한다.
/// 코드에 사용자 문자열을 하드코딩하지 않기 위한 단일 진입점.
/// </summary>
internal static class Localization
{
    private const string DefaultLanguage = "en";

    // (코드, 표시 이름) — 표시 이름은 각 언어의 고유 표기라 번역하지 않는다.
    public static readonly (string Code, string Name)[] Available =
    {
        ("en", "English"),
        ("ko", "한국어"),
    };

    private static Dictionary<string, string> _strings = new();

    public static string CurrentLanguage { get; private set; } = DefaultLanguage;

    public static void Load(string language)
    {
        CurrentLanguage = IsSupported(language) ? language : DefaultLanguage;

        _strings = LoadJson(CurrentLanguage)
                   ?? LoadJson(DefaultLanguage)
                   ?? new Dictionary<string, string>();
    }

    public static bool IsSupported(string language)
    {
        foreach (var (code, _) in Available)
            if (code == language) return true;
        return false;
    }

    /// <summary>키에 해당하는 문자열. 없으면 키 자체를 반환(누락 시 눈에 띄도록).</summary>
    public static string T(string key)
        => _strings.TryGetValue(key, out var value) ? value : key;

    /// <summary>서식 문자열 + 인자 치환.</summary>
    public static string T(string key, params object[] args)
        => string.Format(T(key), args);

    private static Dictionary<string, string>? LoadJson(string language)
    {
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            var resourceName = $"WatermarkRemover.Locales.{language}.json";

            using var stream = asm.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                Logger.Warn($"Locale resource not found: {resourceName}");
                return null;
            }

            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        }
        catch (Exception ex)
        {
            Logger.Warn($"Locale load failed ({language}): {ex.Message}");
            return null;
        }
    }
}
