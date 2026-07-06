# ver_0.3 (2026-04-09)

- Worker Window 숨기기 전략 추가 (FindWindow + ShowWindow)
  - 이유: PaintDesktopVersion이 Activate Windows 워터마크에 효과 없음을 확인. 다른 프로젝트들이 사용하는 Worker Window 직접 숨기기 방식 도입
- svsvc 서비스를 비활성화 대상에 추가
  - 이유: gameshler/Windows-Watermark-Remover 프로젝트에서 svsvc(Software Validation Service)도 워터마크 관련임을 확인
- PaintDesktopVersion + WM_SETTINGCHANGE 전략 삭제
  - 이유: GitHub 조사 결과 어떤 프로젝트도 사용하지 않으며 Activate Windows 워터마크에 효과 없음
- 타이머 이원화: 서비스 체크 1시간 + 윈도우 숨기기 5분
  - 이유: 서비스 상태 확인은 무거우므로 1시간, 윈도우 숨기기는 가벼우므로 5분 간격으로 분리
- WatermarkOverlay.cs 진단용 코드 삭제
  - 이유: Worker Window 숨기기가 더 깔끔한 대안이므로 불필요
- NativeMethods.cs 불필요한 P/Invoke 정리
  - 이유: WatermarkOverlay, PaintDesktopVersion 삭제로 미사용 선언 대량 잔존
