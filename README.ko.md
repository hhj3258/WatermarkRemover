# WatermarkRemover

Windows 정품 인증 워터마크("Windows를 정품 인증하세요")를 제거하는 시스템 트레이 유틸리티입니다.
트레이에 상주하며 워터마크가 나타날 때마다 자동으로 숨깁니다.

**언어:** [English](README.md) | 한국어

---

## 고지

이 프로젝트는 **어디까지나 개인 학습·연구 목적**으로 만들어졌습니다.
Windows 정품 인증을 **우회하거나 크랙하지 않으며**, 워터마크 표시만 숨길 뿐 Windows 라이선스 상태 자체는 변경하지 않습니다.

만약 이 리포지토리가 문제를 일으키거나 삭제 요청이 있을 경우 **주저 없이 삭제합니다.** 사용에 따른 책임은 사용자 본인에게 있습니다.

---

## 요구사항

- Windows 10 / 11
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) — 실행 전 설치 필요
- **관리자 권한** — 서비스 제어에 필요 (매니페스트에 명시되어 실행 시 자동 요청)

---

## 다운로드 및 실행

1. [최신 릴리스](../../releases/latest)에서 `WatermarkRemover.exe` 다운로드
2. 실행 (UAC 창이 자동으로 뜸)
3. 트레이에 파란색 **W** 아이콘이 나타남
4. 트레이 아이콘 우클릭 → **⚙ 설정** → **Windows 시작 시 자동 실행** 체크

> 워터마크는 수 초 내 사라집니다. 풀스크린 게임을 껐다 켜도 자동으로 다시 숨겨집니다.

---

## 트레이 메뉴

트레이 아이콘 우클릭:

- **상태 표시** — 현재 차단 상태 / 다음 갱신까지 남은 시간(카운트다운)
- **워터마크 차단 해제 / 다시 차단** — 토글
- **⚙ 설정**
  - **갱신 주기** — 워터마크 재확인 주기 (1 / 5 / 10 / 30 / 60분)
  - **Windows 시작 시 자동 실행** — 작업 스케줄러에 등록 (로그온 시 관리자 권한으로 실행)
  - **시작 시 자동 차단** — 부팅 후 항상 차단 상태로 시작
  - **동작 로그 파일 기록** — `%LOCALAPPDATA%\WatermarkRemover\log.txt` 에 기록 (디버그용)
- **종료**

---

## 소스에서 빌드

```powershell
dotnet publish WatermarkRemover/WatermarkRemover.csproj -c Release -r win-x64 --self-contained false
```

산출물: `WatermarkRemover/bin/Release/net8.0-windows/win-x64/publish/WatermarkRemover.exe`

`build.ps1` 스크립트는 `publish/ver_*/` 아래에 버전별 빌드를 만들고 작업 스케줄러를 갱신하는 작업을 한 번에 처리합니다.

---

## 동작 방식

두 가지 전략을 병행합니다: Windows 보호 서비스 비활성화, 그리고 Win32 이벤트 후킹을 통한 워터마크 윈도우 실시간 숨김.

**상세한 동작 원리·프로젝트 구조·설계 노트는 [ARCHITECTURE.md](ARCHITECTURE.md)를 참고하세요.** (영문)
