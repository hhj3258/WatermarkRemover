# WatermarkRemover

Windows 정품 인증 워터마크("Windows를 정품 인증하세요")를 제거하는 트레이 유틸리티입니다.
시스템 트레이에 상주하며, 워터마크가 나타나면 자동으로 숨깁니다.

## 동작 방식

두 가지 전략을 병행합니다.

1. **보호 서비스 비활성화** — `sppsvc` / `sppamsvc` / `svsvc` 서비스를 정지·비활성화해 워터마크 렌더링 자체를 막습니다.
2. **워터마크 윈도우 실시간 숨김** — 워터마크가 그려지는 `Worker Window`를 감시하다가 나타나는 즉시 숨깁니다.
   - `SetWinEventHook`으로 윈도우 생성/표시 이벤트를 실시간 후킹 (풀스크린 게임 전환 등으로 워터마크가 재생성되어도 수 ms 내 숨김)
   - 후킹이 실패할 경우를 대비한 주기적 폴링(기본 5분, 설정에서 조정 가능)도 병행

## 요구 사항

- Windows 10 / 11
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
- **관리자 권한** (서비스 제어를 위해 필요 — 매니페스트에 명시되어 실행 시 자동 요청)

## 빌드

```powershell
dotnet publish WatermarkRemover/WatermarkRemover.csproj -c Release -r win-x64 --self-contained false
```

산출물: `WatermarkRemover/bin/Release/net8.0-windows/win-x64/publish/WatermarkRemover.exe`

빌드된 버전별 실행 파일은 `publish/ver_*/` 에 있으며, 각 폴더의 `CHANGES.md`에 변경 내역이 정리되어 있습니다.

## 사용법

`WatermarkRemover.exe`를 실행하면 트레이에 아이콘이 뜹니다. 우클릭 메뉴:

- **상태 표시** — 현재 차단 상태 / 다음 갱신까지 남은 시간(카운트다운)
- **워터마크 차단 해제 / 다시 차단** — 토글
- **⚙ 설정**
  - **갱신 주기** — 워터마크 재확인 주기 (1 / 5 / 10 / 30 / 60분)
  - **Windows 시작 시 자동 실행** — 작업 스케줄러에 등록(관리자 권한으로 로그온 시 실행)
  - **시작 시 자동 차단** — 부팅 후 항상 차단 상태로 시작
  - **동작 로그 파일 기록** — `%LOCALAPPDATA%\WatermarkRemover\log.txt` 에 기록 (디버그용)
- **종료**

## 프로젝트 구조

| 파일 | 역할 |
|------|------|
| `Program.cs` | 진입점, 단일 인스턴스 보장 |
| `TrayApp.cs` | 트레이 아이콘 및 메뉴 (UI) |
| `WatermarkBlocker.cs` | 차단 핵심 로직 (서비스 제어 + 윈도우 후킹) |
| `Settings.cs` | 사용자 설정 저장 (레지스트리 `HKCU\SOFTWARE\WatermarkRemover`) |
| `AutoStartManager.cs` | 작업 스케줄러 기반 자동 시작 관리 |
| `ModernMenuRenderer.cs` | 다크 테마 메뉴 렌더러 |
| `Logger.cs` | 파일 로거 |
| `NativeMethods.cs` | Win32 P/Invoke |
| `Utils.cs` | 검증 유틸리티 |

## 주의

이 도구는 개인 학습·편의 목적입니다. Windows 정품 인증을 우회하는 것이 아니라 워터마크 표시만 숨기며, 라이선스 상태 자체는 변경하지 않습니다.
