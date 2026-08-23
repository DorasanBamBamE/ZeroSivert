# 20 — 허브 NPC 위치 · 충돌체 · 카메라

작성: 2026-08-22
상태: 구현 및 자동 회귀 검사 완료 — 사용자 직접 이동/시각 확인 대기

## 1. 작업 기준

- 현재 열린 백업본을 직접 작업 대상으로 사용한다.
- 기존 Assets, Packages, ProjectSettings는 GitHub origin/main과 의미상 동일함을 전체 파일 비교로 확인했다.
- 로컬에 없던 .gitignore, README.md, Docs만 원격에서 복원했다.
- 커밋과 GitHub 업로드는 사용자 승인 전까지 하지 않는다.

## 2. Hub 카메라 Bounds

기존 CameraFollow는 ZoneGenerator가 있는 Forest에서만 경계를 계산했다. Hub에는 ZoneGenerator가 없어 카메라가 배경 밖으로 이동할 수 있었다.

기존 인코딩이 깨진 CameraFollow.cs는 강제로 재작성하지 않고 보존했다. 대신 Hub 전용 HubCameraBounds를 추가했다.

- 경계 소스: r_hub_0의 SpriteRenderer.bounds
- 실행 순서: DefaultExecutionOrder(1000)
- 기존 CameraFollow가 플레이어를 추적한 다음 최종 위치를 Hub 배경 안으로 제한
- Forest에는 컴포넌트를 붙이지 않아 기존 동작에 영향 없음

Play Mode에서 카메라가 Player (-31.6, -35.9)를 따라가는 것과 배경 Bounds 참조를 확인했다.

## 3. 허브 외곽 충돌

HubCollision 루트를 만들고 Obstacle 레이어/태그의 비 Trigger BoxCollider2D 4개를 배치했다.

- Boundary_Left
- Boundary_Right
- Boundary_Top
- Boundary_Bottom

경계 크기는 허브 배경 월드 Bounds 143.75 × 93.75를 기준으로 한다.

## 4. NPC 물리 충돌

기존 NPC의 CircleCollider2D는 대화 범위용 Trigger라 이동을 차단하지 않았다. 네 NPC에 발밑 비 Trigger BoxCollider2D를 추가했다.

- NPC_Bartender: 0.8 × 0.4
- NPC_Doctor: 0.9 × 0.4
- NPC_Networker: 0.6 × 0.4
- NPC_ZoneDeparture: 0.6 × 0.4
- 공통 offset: (0, -0.2)

기존 대화 Trigger와 상점·퀘스트·출발 기능은 그대로 유지한다.

초기 1차 값은 offset Y가 `+0.2`여서 의사 충돌체가 카운터 벽과 겹쳤다. 네 NPC 모두 실제 발 방향인 아래쪽 `-0.2`로 보정했고, 최종 물리 거리 검사에서 새 허브 폴리곤과의 겹침이 0건임을 확인했다.

## 5. 검증

- HubCameraBounds.cs 표준 검사: 오류 0
- Unity 컴파일 완료
- Hub Play Mode 진입/종료 정상
- 최종 콘솔 오류/경고: 0/0
- 카메라 런타임 캡처: Assets/Screenshots/hub_camera_bounds_runtime.png

기존 Library/BurstCache의 DLL 로드 예외가 재발해 재생성 가능한 캐시를 정리했다. Unity가 사용 중인 DLL 4개는 잠겨 남았으나, 이후 콘솔을 비우고 Hub Play Mode를 다시 실행한 결과 오류/경고 0건을 확인했다. 완전한 캐시 삭제가 다시 필요하면 Unity Editor를 종료한 상태에서 진행한다.

## 6. 스폰 구역 내부 충돌 1차

플레이어 시작 위치 `(-31.6, -35.9)` 주변을 실제 Game View로 확인하고, 통행 공간과 NPC 접근 경로를 피하면서 경계가 명확한 대형 오브젝트 6곳에 비 Trigger BoxCollider2D를 추가했다.

- Obstacle_Cargo_Northwest: 북서쪽 적색 화물 컨테이너
- Obstacle_Crates_West: 서쪽 상자 더미
- Obstacle_Crates_North: 북쪽 상자 더미
- Obstacle_Vending_Northeast: 북동쪽 자판기 적재물
- Obstacle_Barrels_East: 동쪽 배럴 더미
- Obstacle_Train_South: 남쪽 열차 차체

참조용 캡처는 위치 산정 후 삭제했고, 최종 런타임 캡처는 `Assets/Screenshots/hub_spawn_collision_runtime.png`에 남겼다. 씬 저장 후 5초간 Play Mode를 실행했으며 콘솔 오류/경고는 0/0이었다.

NPC 하위 오브젝트 증가처럼 보였던 항목도 확인했다. 바텐더·의사의 두 번째 자식은 기존 상점 재고용 `Stock`이고, 다른 자식은 기존 `Prompt_E` 또는 `DepartureTrigger`이므로 삭제하거나 변경하지 않았다.

## 7. 카메라 화면 비율 회귀 검사

Play Mode에서만 카메라를 배경 양 끝보다 멀리 이동시킨 뒤 `HubCameraBounds`의 최종 좌표를 측정했다. 테스트 후 Play Mode를 종료해 런타임 변경은 폐기했다.

| 화면 비율 | X 제한 | Y 제한 | 카메라 반폭 × 반높이 |
|---|---:|---:|---:|
| 16:9 | -56.875 ~ 56.875 | -38.438 ~ 38.438 | 15.000 × 8.438 |
| 16:10 | -58.375 ~ 58.375 | -38.438 ~ 38.438 | 13.500 × 8.438 |
| 4:3 | -60.625 ~ 60.625 | -38.438 ~ 38.438 | 11.250 × 8.438 |

세 비율 모두 배경 Bounds와 카메라 가시 범위를 반영한 좌표로 정상 제한됐고, 검사 후 콘솔 오류/경고는 0/0이었다.

## 8. 네트워커 대기 애니메이션

사용자가 새로 만든 `NetWorker.controller`와 `NetWorkerIdle.anim`의 연결을 보존하면서 클립 재생 시간을 보정했다.

- 스프라이트 키: 2개 유지
- Samples: 60 → 2 FPS
- 키 시간: 0초 / 0.5초
- 전체 루프: 1.0초
- Loop Time: 활성
- Animator State Speed: 1.0

Play Mode에서 실제 `NPC_Networker`가 `NetWorkerIdle`을 재생하는 것을 확인했다. 시간 간격을 두고 런타임 스프라이트가 `Sprite (5)`에서 `Sprite (6)`으로 교대했으며, 최종 콘솔 오류/경고는 0/0이었다.

## 9. 원작 리소스 대조

사용자가 제공한 원작 추출 리소스를 읽어 현재 허브와 픽셀 좌표를 비교했다.

- 원본 방 이미지: `resource/Room/r_hub.png` — 2300 × 1500
- 현재 클린 배경: `Assets/4.Sprite/Hub/Processed/r_hub_clean_2x.png` — 4600 × 3000
- 현재 배경을 1/2 Nearest 축소해 비교했을 때 RGB 차이는 약 4,570픽셀이며, 대부분 배경에서 제거한 원작 고정 NPC 영역이다.
- 바텐더·의사·네트워커는 현재 좌표가 원작 앵커와 일치했다.
- 역무원은 원작보다 약 1.4유닛 왼쪽이어서 `(-29.31, -37.04)`에서 `(-27.94, -37.17)`로 보정했다.

## 10. 원작 기반 허브 전체 충돌

원작 `r_hub.png`를 16 × 16픽셀 셀로 분석해 검정/보이드 영역과 중성색 벽 셀을 충돌 후보로 만들었다.

- 최초 후보: 9,007셀
- 플레이어 시작 셀: 정상 개방
- 바텐더·네트워커·역무원 경로: 정상 연결
- 의사 경로: 문틀 색을 벽으로 인식한 `(55, 61)` 한 칸만 제외하여 연결
- 최종 충돌 셀: 9,006셀
- 저장 형식: `PolygonCollider2D` 100경로 / 834점

타일 9,006개를 그대로 저장하면 `Hub.unity`가 약 9만 줄 늘어났기 때문에, 동일한 복합 경계를 폴리곤으로 굳힌 뒤 이번 작업에서 만든 임시 Tile 자산과 Tilemap 컴포넌트는 제거했다. 최종 씬에는 `HubCollision/OriginalCollisionGrid/OriginalBlockedCollision`의 폴리곤만 남는다.

물리 거리 검사 결과 플레이어와 네 NPC의 비 Trigger 충돌체가 폴리곤 내부에 겹치는 경우는 0건이었다.

## 11. 기능 회귀 검사

Play Mode에서 UI를 실제로 열고 닫아 다음을 확인했다.

| 대상 | 퀘스트 줄 | 거래 | 출발 | 결과 |
|---|---:|---:|---:|---|
| 바텐더 | 1 | 표시, 재고 5칸 | 숨김 | 정상 |
| 의사 | 1 | 표시, 재고 9칸 | 숨김 | 정상 |
| 네트워커 | 일일 3 | 숨김 | 숨김 | 정상 |
| 역무원 | 0 | 숨김 | 표시 | 정상 |

- 대화 중 `UIBlocker` 활성 및 `Time.timeScale = 0`
- 닫은 뒤 입력 차단 해제 및 `Time.timeScale = 1`
- 바텐더·의사·네트워커 퀘스트 수락 성공, 중복 수락 차단
- 상점 종료 뒤 `ShopSession`과 `InventoryScreen` 모두 닫힘
- 역무원 `Depart()` 1회 호출로 Hub → Forest 전환
- Forest에서 Player, ExtractionZone, RunEndHandler 존재
- 전 과정 콘솔 오류/경고 0/0

Unity Test Runner의 EditMode/PlayMode 기본 스위트도 Passed였지만 등록된 실제 테스트 케이스 수는 0개이므로, 이는 컴파일/러너 기동 확인으로만 기록한다.

## 12. 최종 자동 검증

- Hub 씬 Validator: 누락 스크립트 0, 깨진 Prefab 0, 전체 이슈 0
- Networker 런타임 클립: `NetWorkerIdle` 2 FPS
- 역무원 런타임 클립: `TrainWorker_Idle` 4 FPS
- Hub → Forest 로드 후 콘솔 오류/경고: 0/0
- Play Mode 종료 후 편집 씬 Hub 복귀 확인

## 13. 사용자가 직접 확인할 항목

- 플레이어로 외벽·방 벽·카운터·열차·상자 더미를 밀어 통과되는 틈이 없는지
- 의사 방 출입구가 체감상 너무 좁지 않은지
- 네 NPC의 발 위치와 플레이어 앞/뒤 가림이 원작처럼 보이는지
- 역무원 위치와 애니메이션 피벗이 열차 발판에 자연스럽게 맞는지
- 카메라 이동 중 픽셀 흔들림·블러·타일 이음새가 보이지 않는지
- 16:9 외에도 16:10, 4:3에서 UI가 고정되고 배경 밖이 노출되지 않는지

직접 검사에서 막힘이나 시각 어긋남이 발견되면 해당 월드 좌표/스크린샷을 기준으로 폴리곤 또는 NPC 좌표를 미세 조정한다.
