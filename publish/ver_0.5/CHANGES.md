# ver_0.5 (2026-05-12)

- 코딩 스타일 스킬 도입 + 전면 리팩토링
  - ?댁쑀: .claude/skills/coding-style 신규 추가, 네이밍을 행동 기반으로 통일,WatermarkKiller → WatermarkBlocker 리네임|Killer 표현이 실제 동작(차단/유지)과 맞지 않아서,_windowHideTimer → _watermarkRefreshTimer|구현 디테일(Worker Window)이 아닌 동작(워터마크 갱신)을 드러내기 위함,Settings 클래스 신설|타이머 주기/자동 차단/로그 옵션을 사용자가 조정 가능하게,Logger 클래스 신설|빈 catch 블록 제거 + 디버그용 파일 로그,트레이 메뉴에 다음 갱신 카운트다운 표시|타이머가 언제 동작하는지 사용자가 알 수 있도록,트레이 메뉴 ⚙ 설정 서브메뉴 추가|갱신 주기 프리셋(1/5/10/30/60분), 시작 시 자동 차단, 동작 로그 토글
