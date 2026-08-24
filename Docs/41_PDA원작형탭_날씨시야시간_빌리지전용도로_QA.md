# 41 — PDA 원작형 탭 · 날씨·시야·시간 · 빌리지 전용 도로 QA

작성: 2026-08-24

---

## 1. Perk · Base · Faction 원작형 간략 화면

기존 텍스트 요약 화면을 사용자가 제공한 원작 PDA 화면의 구성에 맞춰 다시 만들었다.

| 탭 | 적용 내용 |
|---|---|
| Perk | 퍽 포인트, 경험치 진행 바, 레벨·숙련, 현재 적용 효과를 흰 선과 좌우 분할 구조로 표시 |
| Base | 2×3 기지 모듈 슬롯, 이용 가능/잠김 색상, 위치·루블·가방·환경 요약 표시 |
| Faction | 외톨이·녹색군·크림슨·도적·과학자 5개 평판 바와 수치 표시 |

- 화면 배경은 원작과 같이 검은색에 가까운 색으로 변경했다.
- 흰 테두리, 노란 중립, 빨간 잠김·적대, 초록 우호 색상을 사용했다.
- 별도 대규모 퍽 트리·기지 성장·세력 보상 시스템은 추가하지 않고 현재 데이터만 표시한다.

실행 화면:

- Assets/Screenshots/pda_perk_reference_layout_final.png
- Assets/Screenshots/pda_base_reference_layout_final.png
- Assets/Screenshots/pda_faction_reference_layout_final.png

---

## 2. 날씨 · 시야 · 게임 시간 연동

ForestEnvironmentController를 Forest의 Global Light 2D에 연결했다.

- 게임 시간을 새로 만들지 않고 기존 GameClock의 날짜·시·분을 사용한다.
- 날씨는 3게임시간 단위로 맑음 / 흐림 / 비 / 안개 중 하나를 결정한다.
- 시간대와 날씨가 Global Light 2D의 밝기·색상에 동시에 반영된다.
- 밤에는 플레이어 중심의 제한된 시야 조명과 외곽 암부가 적용된다.
- 비는 화면 픽셀 빗줄기, 안개는 가시거리 축소와 저채도 오버레이로 표시된다.
- PDA 홈 시계는 Forest에서 시간 + 날씨를 함께 표시한다.

통제 시험:

~~~yaml
정오·맑음:
  GameClock            : 12:00
  daylight             : 0.95
  visibility           : 28.0
  Global Light 목표값   : 0.95

자정·안개:
  GameClock            : 00:00
  weather              : Fog
  visibility           : 4.1
  Global Light 목표값   : 0.116
  PDA 표시             : 00:00 안개
~~~

가시거리 마스크는 화면 전체 격자 보간 방식으로 바꿔 원형 외곽에 사각형 밝은 영역이 남지 않게 했다.

실행 화면:

- Assets/Screenshots/forest_midnight_fog_visibility_final2.png
- Assets/Screenshots/pda_home_time_weather_link.png

---

## 3. 빌리지 북쪽 도로 재작업

이전의 타일 전체 X 이동 행렬 보정은 폐기했다. tile_road_dirt_15의 실제 픽셀·투명도·하단 좌측 피벗을 유지한 전용 전환 스프라이트 2장으로만 연결한다.

~~~yaml
셀 (-2, 53): 원본 도로를 아래 0px → 위 16px로 완만하게 이동
셀 (-2, 59): 원본 도로를 아래 16px → 위 32px로 연속 이동
타일 행렬     : 두 셀 모두 identity
PPU          : 16
필터         : Point
압축         : Uncompressed
~~~

- 두 이미지의 경계 픽셀이 정확히 이어진다.
- 기존 마을 도로와 Forest 지면 도로 사이에 빈 행·중복 도로·투명 간격이 없다.
- 최종 스프라이트:
  - Assets/4.Sprite/TileMap/road_village_north_transition_0.png
  - Assets/4.Sprite/TileMap/road_village_north_transition_1.png
- 실행 화면: Assets/Screenshots/forest_village_north_transition_final2.png

---

## 4. 최종 검증

~~~yaml
PDA Perk/Base/Faction 표시 : 정상
PDA 시간·날씨 표시          : 정상
정오/자정 조명 연동          : 정상
비/안개 화면 효과            : 정상
가시거리 사각 아티팩트        : 0
북쪽 도로 단절·이음새         : 0
스크립트 검증 오류            : 0
Unity Console 오류/경고      : 0 / 0
~~~

Git 커밋과 GitHub 업로드는 진행하지 않았다.
