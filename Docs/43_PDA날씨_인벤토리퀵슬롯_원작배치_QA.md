# 43 — PDA 날씨 · 인벤토리 퀵슬롯 원작 배치 QA

작성: 2026-08-24

## 1. PDA 시간·날씨 배치

- 원작 PDA 화면처럼 시간을 상단 중앙에 단독 표시했다.
- 날씨 아이콘과 날씨명은 시간 바로 아래 행으로 분리했다.
- 제공된 PDA용 맑음·비 아이콘과 안개 아이콘을 Point 필터, 무압축, PPU 16 Sprite로 가져왔다.
- 맑음은 태양, 흐림·비는 비구름, 안개는 안개 아이콘으로 자동 전환한다.
- Hub 기본 맑음과 Forest 강제 비·안개 상태에서 아이콘·문구 전환을 확인했다.

## 2. 인벤토리 퀵슬롯 배치

- 제공된 `s_hud_inv_0.png`의 중앙 하단 슬롯 영역을 기준으로 퀵슬롯 3~6을 이동했다.
- 인벤토리가 열리면 `QuickBar`를 `(240, 46)`에 배치하고 UI 최상단으로 올린다.
- 닫을 때 기존 HUD 위치 `(210, 6)`과 원래 형제 순서로 복원한다.
- Hub와 Forest 모두 중앙 하단 배치와 복원을 확인했다.
- 씬 종료 중 Canvas 비활성화 단계에서는 재정렬을 건너뛰어 종료 시 Console 오류가 발생하지 않는다.

## 3. 검증

```yaml
ClockLabel 스크립트 오류/경고    : 0 / 0
InventoryScreen 스크립트 오류/경고: 0 / 0
Hub PDA 맑음 표시                : 정상
Forest PDA 비 표시               : 정상
Forest PDA 안개 아이콘 전환       : 정상
Hub 인벤토리 퀵슬롯 위치          : (240, 46)
Forest 인벤토리 퀵슬롯 위치       : (240, 46)
인벤토리 닫기 위치 복원           : (210, 6)
최종 Unity Console               : 오류 0 / 경고 0
```

검증 캡처:

- `Assets/Screenshots/QA43_Hub_PDA_WeatherLayout.png`
- `Assets/Screenshots/QA43_Forest_PDA_RainWeatherLayout.png`
- `Assets/Screenshots/QA43_Hub_Inventory_QuickbarLayout.png`

Git 커밋과 GitHub 업로드는 수행하지 않았다.
