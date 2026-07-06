---
name: coding-style
description: WatermarkRemover (WinForms / .NET 8) 프로젝트 C# 네이밍·코드 스타일 컨벤션. 모든 .cs 파일 작성·수정 시 반드시 적용. 필드 네이밍, null 체크, 초기화 검증, 로그, switch, 예외 처리, 레이아웃 규칙.
---

# WatermarkRemover C# 네이밍·코드 스타일

본 스킬은 `WatermarkRemover/**/*.cs` 모든 파일에 적용한다. SupercentTest 프로젝트의 Unity판 `coding-style` 스킬을 WinForms/.NET 환경에 맞게 어댑트한 버전.

---

## 1. 필드 · 프로퍼티

### 네이밍
- **private/internal 필드**: 언더스코어 + camelCase
  - 예: `_watermarkRefreshTimer`, `_blocker`, `_statusItem`
- **public/protected 필드/상수/프로퍼티**: PascalCase
  - 예: `public bool BlockingEnabled`, `private const string PrefKeyPath`

### 프로퍼티
- **읽기 전용 공개값**: 자동 프로퍼티 사용
  - ✅ 권장: `public DateTime NextRefreshAt { get; private set; }`
  - ❌ 지양: `private DateTime _nextRefreshAt; public DateTime NextRefreshAt => _nextRefreshAt;` (백킹 필드가 외부 동작에 필요 없는 경우)

### 행동 기반 네이밍
- 변수·메서드 이름은 **구현 디테일(`WorkerWindow`, `Sppsvc`)이 아닌 행동(`WatermarkRefresh`, `Blocking`)**을 기준으로 짓는다.
- 메서드명은 동사로 시작: `Apply...`, `Disable...`, `Enable...`, `Try...`, `Is...`, `Get...`.
- `Killer`, `Manager`, `Helper` 같은 모호한 접미사는 피하고 실제 책임을 나타내는 명사 사용 (`Blocker`, `Renderer`, `Logger`).

---

## 2. Null · 참조 검증

### null 비교 방식
- ❌ 금지: `if (!obj)` / `if (obj)` — 가독성 떨어짐
- ✅ 사용: `if (obj == null)` / `if (obj != null)` — 명시적

### 필수 참조 누락 처리
- 생성자/초기화에서 필수 의존성이 누락된 경우 **조용히 복구 금지** — `Logger.Error` 후 즉시 `return`(또는 throw).
- 의도: 설정/조립 실수를 런타임에 감춰 디버깅 난이도를 올리지 않기.

### 초기화 단계 검증
생성자 또는 `Start()`/`Init()` 같은 초기화 메서드에서 필수 참조를 `Utils.IsValidObject(...)`로 검증하고 invalid면 즉시 `return;`.

```csharp
public TrayApp()
{
    _blocker = new WatermarkBlocker();
    if (!Utils.IsValidObject(_blocker, nameof(_blocker))) return;

    // 이후 로직에서 _blocker null 체크 불필요
    _blocker.Start();
}
```

> `Utils.IsValidObject` 시그니처:
> `static bool IsValidObject(object? obj, string fieldName)` — invalid 시 `Logger.Error` 후 `false` 반환.

### 중복 null 체크 금지
- 초기화에서 한 번 검증한 필드는 메서드 중간에서 다시 null 체크하지 않는다.
- 예외: 하위 속성/외부 상태가 런타임에 null일 수 있는 경우만.

### 검증 조건 분리
유효성 조건이 **3개 이상**이면 인라인 `&&` 체이닝 대신 전용 검증 메서드로 분리:

```csharp
private bool IsMenuReady()
{
    if (_statusItem  == null) { Logger.Error("_statusItem is null");  return false; }
    if (_toggleItem  == null) { Logger.Error("_toggleItem is null");  return false; }
    if (_settingsItem == null) { Logger.Error("_settingsItem is null"); return false; }
    return true;
}
```

- 공용 유틸이 아니라면 과도하게 범용화하지 않는다 (`context`, `isLog` 같은 옵션 파라미터 금지).
- 내부에서 `Logger.Error(msg)`로 로그 + `bool` 반환.

---

## 3. Enum 분기

- ❌ 금지: enum 값에 따른 `if`/`else if` 체이닝
- ✅ 사용: **switch 문**으로 항목별 case 명시
- **미구현 case**는 `default`에서 `Logger.Error`로 드러나게 처리

```csharp
switch (status)
{
    case BlockingStatus.Active:
        ShowActiveUi();
        break;
    case BlockingStatus.PendingRestart:
        ShowPendingUi();
        break;
    case BlockingStatus.Disabled:
        ShowDisabledUi();
        break;
    default:
        Logger.Error($"Unhandled BlockingStatus: {status}");
        break;
}
```

---

## 4. 로그

본 프로젝트의 표준 로거는 `WatermarkRemover.Logger` 정적 클래스. 사용자가 설정에서 `LogToFile`을 켰을 때만 파일에 기록되며, 끔 상태에서는 비용 0.

```csharp
Logger.Info("워터마크 차단 적용 완료");
Logger.Warn($"서비스 정지 실패: {ex.Message}");
Logger.Error("필수 의존성 누락");
```

- `Console.WriteLine` / `Debug.WriteLine` / `MessageBox` 직접 사용으로 디버그 로그 남기지 않는다. 사용자 노출 알림이 필요한 경우에만 `MessageBox`.

---

## 5. 예외 처리

### 빈 catch 금지
- ❌ 금지: `catch { }` — 실패 원인을 영원히 잃어버린다.
- ✅ 최소: `catch (Exception ex) { Logger.Warn(ex.Message); }`
- 복구 불가능한 결정적 실패는 `catch (Exception ex) { Logger.Error(ex.ToString()); throw; }`로 재throw.

### catch 범위 최소화
- `try` 블록은 가능한 좁게. 예외가 의미 있는 단일 작업만 감싼다.

---

## 6. 레이아웃

### 메서드 간
- 메서드 선언 사이에 **빈 줄 1줄** 유지.

### 메서드 내
긴 메서드는 **논리 단위마다 빈 줄로 구분**:
- if/foreach/while 전후
- early return 다음
- 블록 전환 지점

```csharp
public void ApplyBlockingOnce()
{
    if (!BlockingEnabled) return;

    var msg = TryDisableProtectionServices();
    if (msg != null)
    {
        RestartExplorer();
        Task.Delay(3000).Wait();
    }

    TryHideWatermarkWindow();
    ScheduleNextRefresh();
}
```

### using 정렬
파일 상단 using은 다음 순서, 각 그룹 내 알파벳 정렬:

```csharp
// 1) System.*
using System.Diagnostics;
using System.IO;

// 2) Microsoft.*
using Microsoft.Win32;

// 3) 프로젝트 네임스페이스 (필요 시)
using WatermarkRemover.Utils;
```

---

## 7. 클래스 선언

- 기본 `sealed` 권장 — 상속 의도가 명확한 경우에만 비-sealed.
- 정적 유틸리티 클래스는 `static class`.
- 파일명 = 클래스명. 한 파일에 클래스 하나(중첩 클래스 제외).

---

## 8. 금지 사항

- **빈 catch 블록** — 무조건 최소 `Logger.Warn(ex.Message)`.
- **모호한 동사 네이밍** — `RunOnce`, `DoWork`, `Process` 같은 무의미한 동사. 행동을 명시.
- **구현 디테일 누출 네이밍** — `_workerWindowHideTimer` → `_watermarkRefreshTimer`.
- **하드코딩 매직 넘버** — 타이머 주기 같은 사용자 조정 가능 값은 `Settings` 클래스를 통해 읽기.

---

## 9. 적용 체크리스트 (코드 리뷰 시)

- [ ] private 필드 `_camelCase` 규칙 준수
- [ ] public/상수 `PascalCase`
- [ ] 행동 기반 이름 (구현 디테일 X)
- [ ] `if (obj == null)` 명시적 비교
- [ ] 생성자/초기화에서 필수 참조 `Utils.IsValidObject` 검증
- [ ] 검증 조건 3개↑는 전용 메서드 분리
- [ ] enum 분기는 `switch` + `default` 에러
- [ ] 로그는 `Logger.*` 사용
- [ ] 빈 `catch {}` 없음
- [ ] 메서드 간/내 빈 줄 규칙 준수
- [ ] using 그룹 정렬
- [ ] 하드코딩 사용자 설정값 없음 (`Settings`로 위임)
