# ver_0.2 (2026-04-09)

- WM_SETTINGCHANGE 브로드캐스트 추가
  - 이유: PaintDesktopVersion=0 설정 후 Explorer가 변경사항을 즉시 인식하지 못해 워터마크가 계속 표시됨. 두 번째 부팅부터는 sppsvc가 이미 Disabled 상태라 Explorer 재시작이 호출되지 않았기 때문. 설정 변경 후 "Policy" 메시지를 브로드캐스트하여 재시작 없이 즉시 반영되도록 수정.
