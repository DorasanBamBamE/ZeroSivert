# Claude용 ZeroSivert 최종 마감 프롬프트

현재 Unity Editor에 열려 있는 프로젝트는 2D ZeroSivert의 최신 로컬 원본이다.

```text
프로젝트 경로: C:\Users\m\Desktop\Backup0823\2D ZeroSivert
Unity 버전: 6000.2.8f1
GitHub 문서 저장소: https://github.com/DorasanBamBamE/ZeroSivert
```

## 절대 준수 사항

- 실제 최신 원본은 위 로컬 Unity 프로젝트다.
- GitHub에는 Unity 프로젝트가 아니라 Docs와 인수인계 문서만 최신 상태로 올라가 있다.
- GitHub 파일로 로컬 프로젝트를 덮어쓰지 마라.
- `git pull`, `reset`, `checkout`, 자동 복구를 임의로 실행하지 마라.
- 기존 씬, 프리팹, GUID, 사용자 저장 파일과 현재 기능을 보존한다.
- 파일 이동은 Unity `AssetDatabase.MoveAsset`처럼 GUID가 유지되는 방법만 사용한다.
- Git 커밋이나 GitHub 업로드는 변경 내용을 보고하고 사용자 승인을 받은 뒤에만 실행한다.
- 사용자 작성 C#의 Debug 로그는 제거된 상태다. 임시 로그를 남기지 말고 Console과 검사 코드로 검증한다.
- 오류를 발견하면 원인을 확인한 뒤 수정하고, 스크립트 변경 후 컴파일 종료와 Console을 반드시 확인한다.
- 최종 판정은 직접 UI 클릭·이동·사격·상호작용을 포함한 실제 조작을 우선한다. 메서드 직접 호출만으로 완료 처리하지 마라.

## 시작 절차

1. Unity Editor와 Unity MCP 연결 상태, 프로젝트 경로, Unity 버전을 확인한다.
2. 현재 씬, Play 상태, 컴파일 상태와 Console 오류·경고를 확인한다.
3. 로컬 `Docs`의 문서를 전부 읽되 다음 문서를 우선한다.
   - `19_향후_작업목록_로드맵.md`
   - `30_다른PC_작업인수인계.md`
   - `31_다른PC_통합QA_무기_루팅_VFX.md`부터 `45_타이틀호버_코드정리_에셋재배치.md`까지
4. `Assets`, `Packages`, `ProjectSettings`, Build Settings의 MainTitle·Hub·Forest 등록 상태를 확인한다.
5. 수정 전 현재 상태와 가장 안전한 작업 순서를 짧게 보고하고 계속 진행한다.

## 최신 완료 상태

- MainTitle의 Play / Exit / Config, NEW GAME / LOAD GAME 구현 완료
- 타이틀·PLAY 하위 메뉴·설정 패널 Button 9개 모두 노란색 마우스 호버 적용
- Hub NPC 4명과 역무원 대화·Forest 출발 연출 구현
- Hub → Forest → 5초 탈출 → 결과 화면 1회 → Hub 복귀 및 저장 흐름 검증
- 인벤토리 퀵슬롯 등록·해제·원작형 하단 배치 구현
- Hub 사격 차단, Forest 사격 유지
- Forest 구조물 충돌·루팅·적 스폰포인트 전수 보정
- 밴딧 정찰대, 좀비·늑대 무리, 빌리지 보스/가드 조우 구현
- 적 장애물 우회와 밴딧 엄폐 행동 구현
- PDA Perk / Base / Faction 간략 구현
- 맑음·흐림·비·안개, 시간·조명·시야 연동 구현
- 빌리지 북쪽·남쪽 도로 연결 및 렌더 정렬 보정
- 사용자 작성 C# 112개 정리, Debug 호출 52개 제거, 핵심 설명 주석 8줄만 유지
- Item/Loot/Weapon 데이터 90개를 `Assets/4.Data`로 이동
- 숲 타일 27개를 `Assets/4.Sprite/TileMap/Tiles/Forest`로 이동
- TrainWorker 애니메이션을 `Assets/5.Animation/NPC`로 이동
- 마지막 검증: 컴파일 오류 0, Console 오류 0 / 경고 0

## 최종 마감 우선순위

1. MainTitle → LOAD GAME → Hub → 역무원 → Forest → 정상 탈출 → Hub 전체 루프를 실제 입력으로 다시 확인한다.
2. Hub에서 NPC 4명 접근, 말풍선, 대화, 상점, 작업대와 벽·가구 충돌을 직접 이동으로 전수 검사한다.
3. Forest에서 10분 이상 이동·전투하며 적의 벽 통과, 벽 너머 감지·사격, 엄폐·우회·무리 복귀·겹침을 검사한다.
4. 빌리지와 북쪽·남쪽 도로를 여러 해상도에서 확인하고 길 단절, 타일 이음새, 프롭 침범, 회색 빈 타일을 제거한다.
5. 인벤토리·루팅·상점 사이 아이템 이동, 여러 스택 수량, 장비·탄종·퀵슬롯·저장 복원을 회귀 검사한다.
6. 총기 12종의 아이콘, 탄약, 탄종, 발사·재장전·피격음을 실제 플레이로 확인하고 음량을 체감 기준으로 조정한다.
7. 피격·사망·루팅·탈출의 남은 VFX를 현재 픽셀 아트 스타일을 해치지 않는 최소 범위로 보강한다.
8. EditMode/PlayMode 자동 회귀 테스트와 Forest 랜덤 시드 20회 생성 검사를 추가한다.
9. Windows Development Build에서 Hub·Forest·전투·빌리지 평균 FPS, 프레임 타임, GC, 배칭을 측정하고 필요한 경우 풀 밀도부터 조정한다.
10. 창/전체화면, 주요 해상도, Alt+Tab, 입력 포커스, Development/Release Build를 최종 확인한다.

## 완료 조건

```yaml
핵심 루프: MainTitle → Hub → Forest → 탈출/사망 → Hub 정상
허브 충돌: 벽·가구 통과 0, NPC 접근 불가 0
숲 충돌: 투명벽·구조물 내부 스폰·벽 관통 0
적 AI: 벽 통과·벽 너머 사격 0, 우회·엄폐·복귀 정상
UI: 인벤토리·퀵슬롯·PDA·상점·결과 화면 회귀 오류 0
저장: 장비·아이템·탄약·퀵슬롯·퀘스트 유지
성능: 1920×1080 평균 60 FPS 목표
빌드: Development와 Release 실행 가능
Console: 오류 0, 경고 0
```

작업 단위마다 실제 동작을 검증하고 관련 Docs를 다음 번호로 갱신한다. 기존 기능을 다시 만들거나 범위를 불필요하게 확장하지 말고, 최종 출시 가능한 안정성과 회귀 검증에 집중한다.
