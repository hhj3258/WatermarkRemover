namespace WatermarkRemover;

/// <summary>
/// 공용 검증 유틸리티. 초기화 단계에서 필수 의존성 누락을 빠르게 드러내기 위함.
/// </summary>
internal static class Utils
{
    /// <summary>
    /// obj가 null이면 Logger.Error 후 false 반환. 호출 측은 invalid 시 즉시 return.
    /// </summary>
    public static bool IsValidObject(object? obj, string fieldName)
    {
        if (obj == null)
        {
            Logger.Error($"'{fieldName}' is null");
            return false;
        }
        return true;
    }
}
