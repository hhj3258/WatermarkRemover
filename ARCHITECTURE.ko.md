# WatermarkRemover — 아키텍처

**언어:** [English](ARCHITECTURE.md) | 한국어

이 문서는 WatermarkRemover의 내부 동작 원리를 설명합니다. 설치·사용법은 [README](README.ko.md)를 참고하세요.

## 1. 요약

Windows 정품 인증 워터마크를 제거하는 트레이 유틸리티로, **보호 서비스를 비활성화**하고 **Win32 이벤트 후킹으로 워터마크 윈도우를 실시간 숨김**으로써 동작합니다. 시스템 트레이에 상주하며 작업 스케줄러로 자동 시작됩니다.

---

## 2. 기술 스택

| 영역 | 선택 | 이유 |
|------|------|------|
| UI 프레임워크 | WinForms (.NET 8) | 가벼운 트레이 앱. `NotifyIcon` + `ContextMenuStrip`이 기본 내장이라 외부 의존성 없음 |
| 윈도우 후킹 | `SetWinEventHook` (user32) | 워터마크 윈도우가 풀스크린 전환 시 재생성됨. 폴링으로는 못 따라가므로 생성/표시 이벤트를 후킹해 밀리초 단위로 대응 |
| 서비스 제어 | `ServiceController` + `sc.exe` + 레지스트리 | `sppsvc`/`sppamsvc`/`svsvc` 정지·비활성화. 레지스트리 `Start=4`로 재부팅 후에도 유지 |
| 설정 저장 | 레지스트리 `HKCU\SOFTWARE\WatermarkRemover` | 외부 설정 파일 없이 재설치에도 유지되고 읽고 쓰기 간단 |
| 자동 시작 | 작업 스케줄러 (`schtasks`) | 일반 Run 키로는 권한 상승 불가. 작업 스케줄러로 로그온 시 관리자 권한 실행 |
| 렌더링 | 커스텀 `ToolStripRenderer` | 기본 렌더러로는 불가능한 다크 테마 + 라운드 코너(`DwmSetWindowAttribute`) 구현 |

---

## 3. 파일 구조

```
WatermarkRemover/
├── build.ps1                    # 버전 폴더 배포 + 작업 스케줄러 갱신
├── publish/
│   └── ver_*/                   # 버전별 실행 파일 + CHANGES.md (변경 내역)
└── WatermarkRemover/
    ├── Program.cs               # 진입점, 단일 인스턴스 뮤텍스, 시작 시 항상 차단
    ├── TrayApp.cs               # 트레이 아이콘, 메뉴, 실시간 카운트다운, 설정 서브메뉴
    ├── WatermarkBlocker.cs      # 핵심: 서비스 비활성화 + 윈도우 후킹 + 폴링 백업
    ├── Settings.cs              # 사용자 설정 (레지스트리 기반)
    ├── AutoStartManager.cs      # 작업 스케줄러 등록/해제
    ├── ModernMenuRenderer.cs    # 다크 테마 메뉴 렌더러
    ├── Logger.cs                # 선택적 파일 로거 (%LOCALAPPDATA%\WatermarkRemover\log.txt)
    ├── NativeMethods.cs         # Win32 P/Invoke 선언
    ├── Utils.cs                 # 검증 유틸리티
    ├── app.manifest             # requestedExecutionLevel = requireAdministrator
    └── app.ico                  # 애플리케이션 아이콘 (파란 배경, 흰색 W)
```

---

## 4. 실행 흐름

```
Program.Main
  │
  ├─ 단일 인스턴스 뮤텍스 (이미 실행 중이면 종료)
  ├─ BlockingEnabled = true 강제   (앱이 실행되면 항상 차단)
  │
  ▼
TrayApp (ApplicationContext)
  │
  ├─ 트레이 아이콘 + 다크 테마 컨텍스트 메뉴 구성
  │
  └─ WatermarkBlocker.Start()
        │
        ├─ ApplyBlockingOnce()
        │     ├─ TryDisableProtectionServices()  (레지스트리 Start=4 + 서비스 Stop)
        │     ├─ 서비스가 정지됐으면 → RestartExplorer() + 대기
        │     └─ TryHideWatermarkWindow()         (FindWindow "Worker Window" → SW_HIDE)
        │
        ├─ 서비스 재점검 타이머 (1시간)  → ApplyBlockingOnce()   [Windows Update가 서비스를 되살릴 수 있음]
        ├─ 워터마크 갱신 타이머 (5분)    → TryHideWatermarkWindow() [폴링 백업]
        │
        └─ InstallWindowEventHook()
              └─ SetWinEventHook(CREATE, SHOW)
                    └─ 이벤트 발생 시: 클래스가 "Worker Window"이면 → ShowWindow(SW_HIDE)
```

트레이 메뉴는 `Opening` 시점마다 상태(상태 텍스트, 카운트다운, 체크 표시)를 다시 계산하며, 500ms 타이머가 메뉴가 열려 있는 동안 카운트다운을 실시간으로 갱신합니다.

---

## 5. 상세 동작 원리

### 두 가지 차단 전략

**전략 1 — 보호 서비스 비활성화.**
`sppsvc`, `sppamsvc`, `svsvc`는 정품 인증 상태와 워터마크를 담당하는 서비스입니다. `WatermarkBlocker`는 각 서비스에 대해 `HKLM\SYSTEM\CurrentControlSet\Services\<이름>` 아래 레지스트리 `Start=4`(비활성화)를 쓰고 `ServiceController.Stop()`을 호출합니다. 레지스트리 값을 쓰는 것이 **재부팅 후에도 차단이 유지**되게 하는 핵심입니다 — 실행 중인 서비스를 정지시키기만 하면 다음 부팅 때 되돌아갑니다. 서비스가 실제로 정지되면 Explorer를 재시작해 워터마크 없이 바탕화면이 다시 그려지게 합니다. 이 경로는 관리자 권한(`IsAdmin` 게이트)이 필요하며, 권한이 없으면 전략 2만으로 동작합니다.

**전략 2 — 워터마크 실시간 숨김.**
워터마크는 `Worker Window` 클래스의 최상위 윈도우 안에 그려집니다. 두 가지 방식으로 숨깁니다:

- **이벤트 후킹 (주력).** `SetWinEventHook`으로 `EVENT_OBJECT_CREATE`와 `EVENT_OBJECT_SHOW`를 구독합니다. 매칭되는 이벤트마다 콜백이 윈도우 클래스명을 읽고, `Worker Window`이면 `ShowWindow(hwnd, SW_HIDE)`를 호출합니다. `WINEVENT_SKIPOWNPROCESS` 플래그로 앱 자신의 윈도우 이벤트는 제외해 콜백 비용을 낮춥니다. 풀스크린 게임 깜빡임을 잡는 것이 바로 이 방식입니다 — 윈도우가 재생성되는 즉시 수 ms 내에 숨깁니다.

  ```csharp
  _winEventHook = NativeMethods.SetWinEventHook(
      EVENT_OBJECT_CREATE, EVENT_OBJECT_SHOW, IntPtr.Zero,
      _winEventDelegate, 0, 0,
      WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
  ```

- **폴링 (백업).** `System.Windows.Forms.Timer`(기본 5분, 설정 가능)가 주기적으로 `FindWindow("Worker Window", null)`을 호출해 숨깁니다. 후킹이 이벤트를 놓칠 경우를 대비한 안전망입니다.

### 상태 모델: 의도 vs 서비스 상태

두 개의 별도 불리언이 UI와 동작을 좌우하며, 이 둘을 혼동한 것이 초기 토글 버그의 원인이었습니다:

- **`Settings.BlockingEnabled`** — *사용자의 의도* (차단을 켜고 싶은가?). 레지스트리에 저장되어 재시작 후에도 유지.
- **`WatermarkBlocker.IsServiceStopped()`** — `sppsvc`의 *관측된 상태* (지금 서비스가 정지돼 있는가?).

트레이 상태는 이 둘의 조합으로 결정됩니다: 의도-켜짐 + 서비스-정지 = "차단 중", 의도-꺼짐 + 서비스-정지 = "재시작 후 차단 해제 예정", 서비스-실행 중 = "차단 해제됨".

### 영속성

모든 사용자 설정은 `HKCU\SOFTWARE\WatermarkRemover` 아래에 저장됩니다 (`BlockingEnabled`, `RefreshIntervalMinutes`, `LogToFile`). `BlockingEnabled`는 매 실행 시작 시 `true`로 강제되므로, 차단 해제는 해당 실행 동안만 유효합니다. 선택적 동작 로그는 `%LOCALAPPDATA%\WatermarkRemover\log.txt`에 기록되며 `LogToFile` 값으로 제어됩니다 (꺼져 있으면 비용 0).

### 타이머

| 타이머 | 기본 주기 | 목적 |
|--------|-----------|------|
| 서비스 재점검 | 1시간 | Windows Update가 서비스를 되살렸을 경우 차단 재적용 |
| 워터마크 갱신 | 5분 (설정 가능) | 윈도우를 다시 숨기는 폴링 백업 |
| 카운트다운 | 500ms (메뉴 열림 시에만) | "다음 갱신" 카운트다운 텍스트 실시간 갱신 |

---

## 6. 해결한 기술적 도전

**1. 재부팅 후 워터마크 재등장**

서비스를 한 번 정지시키는 것만으로는 부족했습니다 — Windows가 다시 활성화했습니다. 세 보호 서비스 모두에 레지스트리 `Start=4`(비활성화)를 함께 쓰고, Windows Update가 되돌릴 경우를 대비한 1시간 주기 재점검 타이머로 차단을 재적용하도록 해결했습니다.

**2. 풀스크린 게임에서의 워터마크 깜빡임**

풀스크린을 껐다 켤 때(예: 리그 오브 레전드) Windows가 `Worker Window`를 재생성하는데, 5분 폴링으로는 너무 느려 워터마크가 눈에 띄게 깜빡였습니다. `SetWinEventHook`의 `EVENT_OBJECT_CREATE`/`EVENT_OBJECT_SHOW`로 해결 — 재생성 즉시 수 ms 내에 숨깁니다. 폴링은 백업으로 유지합니다. 파일 로그에 각 전환마다 `hidden via WinEvent` 항목이 찍히는 것으로 검증했습니다.

**3. 토글 상태 버그 (동작 vs 의도)**

`IsBlocking()`이라는 이름의 메서드가 실제로는 `sppsvc` 서비스 정지 여부만 보고했지 사용자의 차단 *의도*는 아니었습니다. "재시작 대기" 상태에서 잘못된 값을 반환해, 다시 차단을 누르면 차단 해제 팝업이 떴습니다. 두 개념을 분리해 해결했습니다 — 사용자 의도는 `Settings.BlockingEnabled`에 두고, 서비스 상태 확인은 `IsServiceStopped()`로 이름을 바꿔 의도 플래그인 척하지 못하게 했습니다.

**4. 커스텀 렌더링 메뉴의 컬러 이모지**

상태 행에 ✅/⏳/⚠ 글리프를 씁니다. 커스텀 `ToolStripRenderer`는 GDI/GDI+로 텍스트를 그리는데, 폰트와 무관하게 이모지를 단색으로 렌더링합니다. 여러 방식(`TextRenderer`, `Segoe UI Emoji`, AntiAlias)을 시도했으나 컬러 이모지는 GDI의 구조적 한계로 판명됐고, 그래서 글리프는 흰색으로 그리고 상태 색상은 행 배경으로 전달합니다.

**5. 메뉴 가장자리의 흰 픽셀 라인**

다크 라운드 메뉴 상단에 1~2px 흰 선이 보였습니다. 기본 `ToolStrip` 보더와 시스템 기본 `BackColor` 때문이었습니다. `OnRenderToolStripBorder`를 오버라이드해 아무것도 그리지 않도록 하고, 메뉴(및 모든 서브메뉴 드롭다운)의 `BackColor`를 다크 테마 색으로, 패딩을 0으로 설정해 해결했습니다.

---

## 7. 보안 및 설계 노트

- **로컬 전용** — 앱은 어떤 외부 서버와도 통신하지 않습니다. 모든 동작은 로컬 서비스/레지스트리/윈도우 조작입니다.
- **자격증명·텔레메트리 없음** — 아무것도 수집·전송하지 않습니다. 선택적 파일 로그는 `%LOCALAPPDATA%` 아래에만 남습니다.
- **권한 상승은 명시적** — 관리자 권한은 매니페스트에 선언되고 UAC로 요청됩니다. 트레이에 보이지 않는 작업을 백그라운드에서 몰래 하지 않습니다.
- **정품 인증을 변경하지 않음** — 시각적 워터마크 오버레이만 숨기며, Windows 라이선스 상태는 절대 수정하지 않습니다.

---

## 8. 배포

`build.ps1`이 버전 릴리스의 단일 진입점입니다:

```powershell
.\build.ps1 -Version "1.0" -Changes "수정 내용|수정 이유"
```

이 스크립트는 (1) 프레임워크 의존 단일 파일 exe를 배포하고, (2) exe와 생성된 `CHANGES.md`가 담긴 `publish/ver_<버전>/`을 만들고, (3) 작업 스케줄러 항목을 새 버전으로 재등록합니다(관리자 권한, UAC 창).

GitHub 릴리스에는 빌드된 `WatermarkRemover.exe`를 직접 첨부하므로, 다른 컴퓨터에서는 클론 없이 다운로드해서 실행할 수 있습니다 — .NET 8 Desktop Runtime만 있으면 됩니다.

---

## 9. AI 활용 개발

이 프로젝트는 빌드·런타임 환경에 직접 접근할 수 있는 AI 활용 세션(Claude Code)으로 제작·개선되었습니다.

| 단계 | 작업 | AI 역할 |
|------|------|---------|
| 리서치 | 워터마크가 그려지는 방식 조사 (Worker Window 클래스, 보호 서비스) | 주도 — 웹/GitHub 조사 + 실기기 검증 |
| 구현 | 전체 소스 작성 (blocker, 트레이 UI, 설정, 로거, 렌더러) | 전담 |
| 디버깅 | 풀스크린 깜빡임 재현, 파일 로그로 WinEvent 후킹 동작 확인 | 주도 — 앱 실행, 로그 분석, 반복 |
| 검증 | 레지스트리/서비스 상태 확인, 아이콘 렌더링 확인, 실시간 트레이 점검 | 공동 — AI 실행 / 사용자 시각 판단 |
| 협업 규칙 | 네이밍·null 체크·로깅·예외 규칙을 담은 `coding-style` 스킬을 전 파일에 적용 | 규칙 수행 |

핵심 사례:

- **이론이 아닌 실제 동작 검증** — 이벤트 후킹이 동작한다고 가정하는 대신, AI가 파일 로그를 켜고 워터마크를 유발한 뒤 각 풀스크린 전환마다 `hidden via WinEvent` 항목이 찍히는 것을 확인했습니다.
- **동작 기반 리팩토링** — 구현 디테일 식별자를 동작 기반으로 일괄 변경(`WatermarkKiller` → `WatermarkBlocker`, `_windowHideTimer` → `_watermarkRefreshTimer`)했고, 이 과정에서 `IsBlocking()`의 의도 vs 상태 버그도 드러났습니다.
- **환경 내 자산 생성** — 애플리케이션 아이콘(다중 해상도 `.ico`, 파란 배경 + 흰색 W)을 기존 트레이 아이콘에 맞춰 코드로 생성하고, 미리보기로 렌더링해 시각 확인 후 내장했습니다.
