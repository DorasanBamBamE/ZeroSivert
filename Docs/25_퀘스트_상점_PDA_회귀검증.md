# 25 — 퀘스트 · 상점 · PDA 회귀 검증

작성: 2026-08-22  
상태: 데이터 정합성, 핵심 UI, 실제 처치/거래/탭 전환 검증 완료

## 1. 데이터 정합성

Unity AssetDatabase와 실제 직렬화 참조를 기준으로 검사했다.

```yaml
ItemData                 : 28개
QuestData                : 7개
SaveGameCatalog 등록      : 아이템 28 / 퀘스트 7
적 ID                    : Bandit, Wolf, Ghoul
중복/빈 itemId·questId    : 0
아이콘 누락               : 0
무기 WeaponData 누락      : 0
퀘스트 목표 아이템 누락    : 0
도달 불가능한 처치 ID      : 0
```

전리품 테이블 8개는 빈 테이블, null 아이템, 잘못된 최소/최대 수량이 모두 0건이었다.

## 2. 퀘스트 실제 경로

수집형과 처치형을 각각 수락하고 완료했다.

```yaml
붕대 수집 의뢰 : 5개 제출 / 루블 +500 / 경험치 +120
밴딧 처치 의뢰 : 8회 진행 / 루블 +1,000 / 경험치 +250
일일 의뢰      : 3개 추첨 / 중복 0
```

처치형은 `QuestManager.ReportKill`만 직접 호출한 것이 아니라 Forest의 실제 Bandit `EnemyHealth.TakeDamage → Die → EnemyIdentity.ReportDeath` 경로를 실행했다. 진행도는 0에서 1로 증가했고 디스크 저장 및 에디터 재시작 뒤에도 1로 복원됐다.

현재 7개 퀘스트의 `rewardItems`는 선택형이며 별도 보상 전리품 테이블이 지정되지 않은 데이터가 있다. 루블·경험치·평판 경로는 동작하지만, 퀘스트별 아이템 보상을 추가하려면 콘텐츠 결정을 별도로 해야 한다.

## 3. 상점과 경제

데이터 연결:

```yaml
바텐더 : 판매 배율 1.4 / 매입 배율 0.5 / Stock_Bartender / 서비스 없음
의사   : 판매 배율 1.3 / 매입 배율 0.45 / Stock_Doctor / 치료 300·출혈 150·방사능 400
ShopController : NPC_Bartender, NPC_Doctor 각 1개
무한 차익 조건 : 0건
```

실제 거래:

```yaml
의사 붕대 5개 구매 : 계산가 390 / 실제 차감 390
같은 붕대 판매      : 계산가 135 / 실제 지급 135
루블 0 구매         : 거래 취소 / 재고 유지 / 260루블 필요 안내
```

게임 날짜를 1일에서 2일로 변경하고 두 상점의 재고 갱신을 실행했을 때 `stockedDay`가 모두 1에서 2로 바뀌어 일일 재고 갱신도 확인했다.

## 4. Hub Canvas 누락 수정

Hub의 화면 UI 전체를 담은 루트 `Canvas`가 비활성 상태였다. 이 상태에서는 다음 컴포넌트가 존재해도 `Update`가 돌지 않고 화면이 렌더링되지 않는다.

- InventoryScreen
- DialogueUI
- QuickSlotBar
- PDAController
- QuestListUI
- MapUI

Hub Canvas를 활성화해 저장했다. 활성화 직후 Canvas 시스템이 루트 RectTransform을 화면 크기에 맞게 정상화했으며, 기본 Hub 화면에는 닫힌 패널이 노출되지 않았다.

런타임에서 다음 화면을 실제 Game View 캡처로 확인했다.

- `hub_inventory_runtime.png`
- `hub_dialogue_runtime.png`
- `hub_shop_runtime.png`
- `hub_pda_runtime.png`
- `hub_pda_tab0_runtime.png`
- `hub_pda_tab1_runtime.png`
- `hub_quest_runtime.png`

## 5. PDA 탭과 외부 화면 복귀

직렬화 검사:

```yaml
PDA 내부 패널/버튼 : 2 / 2, null 0
전체 탭            : 7
사용 가능 탭        : 4
잠긴 준비 중 탭     : 3
외부 화면 탭        : Quest, Map 각 1
StatsPanel 필수 참조 : 누락 0
```

동작 검사:

- PDA 열기: 홈 표시, `Time.timeScale = 0`
- 내부 탭 0/1: 통계와 무기 숙련도 화면 표시
- 홈 복귀와 닫기: 시간 1 복원
- Quest/Map 탭: PDA를 접고 외부 화면을 표시
- 외부 화면 닫기: PDA 홈으로 즉시 복귀, 시간 0 유지
- 마지막 PDA 닫기: 시간 1 복원

기존에는 외부 화면을 닫은 뒤 `PDAController.Update`의 폴링에만 복귀를 맡겨 일부 상황에서 PDA가 다시 열리지 않았다. `QuestListUI.Close`와 `MapUI.Close`가 `PDAController.ResumeFromExternal()`을 직접 호출하도록 수정했다.

## 6. 전체 루프 재검증

Hub Canvas 및 PDA 수정 뒤 `Hub → Forest → 정상 탈출 → Hub → 생존 결과 화면 Skip`을 다시 실행했다.

```yaml
Forest 플레이어/인벤토리 : 정상
GameManager              : 유지
PlayerLevel/GameStats    : 유지
RaidCount                : 1
생존 결과 화면            : 열림, 시간 0
Skip 뒤                  : 닫힘, 시간 1
콘솔 오류/경고            : 0 / 0
```

## 7. 사용자가 직접 확인할 항목

1. Hub에서 `Tab`, `J`, `Q`, `M` 키가 원하는 입력 배치인지.
2. PDA의 작은 한글 통계 글꼴이 실제 모니터에서 읽기 편한지.
3. 바텐더·의사 대화와 거래 화면의 투명도·크기가 적절한지.
4. PDA Quest/Map 화면을 닫으면 PDA 홈으로 돌아오는 동작이 자연스러운지.
5. 잠긴 PDA 탭 3개를 계속 회색으로 둘지, `준비 중` 문구를 추가할지.

## 8. 최종 빌드

이 문서의 Hub Canvas 및 PDA 복귀 수정까지 포함해 Windows Development Build `build-3f5da10299`를 다시 생성했다. 17.79초, 총크기 약 329.22MB, 오류 0, 경고 486건으로 성공했으며 Null 그래픽 장치 10초 스모크 로그에는 C# 예외가 없었다.
