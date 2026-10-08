# 개발 01: Unreal 프로젝트 설정

> 대상 엔진: **Unreal Engine 5.4 이상** (`Jisik.uproject`의 `EngineAssociation`)
> 다른 버전을 쓰면 `.uproject`를 우클릭 → *Switch Unreal Engine version* 으로 바꾼다.

## 1. 처음 열기

1. Visual Studio 2022(“C++를 사용한 게임 개발” 워크로드) 설치
2. `Jisik.uproject` 우클릭 → **Generate Visual Studio project files**
3. `Jisik.sln`을 열고 `JisikEditor` / `Development Editor`로 빌드
4. 에디터 실행

## 2. 신 데이터 가져오기 (최초 1회)

신 데이터의 원본은 `Data/Deities.csv`이며, 에디터에서 DataTable 에셋으로 가져온다.

1. 콘텐츠 브라우저에 `Content/Data` 폴더 생성
2. `Data/Deities.csv`를 그 폴더로 드래그
3. Import As: **DataTable**, Row Struct: **DeityRow** 선택
4. 에셋 이름을 **`DT_Deities`** 로 저장

`Config/DefaultGame.ini`가 이미 `/Game/Data/DT_Deities`를 가리키므로 추가 설정은 필요 없다.
경로를 바꿨다면 *프로젝트 설정 → Game → Jisik → Deity Table*에서 다시 지정한다.

> CSV를 고친 뒤에는 `DT_Deities` 에셋에서 **Reimport** 한다.

## 3. 코드 구조

| 파일 | 책임 |
|---|---|
| `Data/JisikTypes.h` | `ESubject`(교과), `EDeityRank`(위계) 열거형 |
| `Data/DeityRow.h` | DataTable 한 행 = 신 하나의 정적 데이터 (이름, 위계, 내부 교과, 실루엣 여부) |
| `Data/DeityTableValidator` | 세계관 규칙 검사만 담당 (엔진 의존 없음 → 단위 테스트 대상) |
| `JisikSettings.h` | 프로젝트 설정에 DataTable 경로 노출 |
| `DeityRegistrySubsystem` | 게임 시작 시 로드 → 검증 → 조회 API 제공 |

`DeityRegistrySubsystem`은 **검증을 통과한 경우에만** 데이터를 반영한다.
실패하면 `IsReady()`가 `false`이고 출력 로그(`LogJisik`)에 원인이 찍힌다.

### Blueprint에서 쓰기

`Get Game Instance Subsystem` → `Deity Registry Subsystem` →
`Find Deity`, `Find Deity Id By Subject`, `Get Deity Ids By Rank`

## 4. 세계관 규칙 (검증 대상)

- 최고신(`Supreme`)은 정확히 1명이며 교과가 없다(`None`).
- 8개 교과마다 권속신(`Kin`)이 정확히 1명씩 있다.
- 모든 신은 표시 이름이 있다.
- 표시 이름에는 교과명(국어·수학 등)이 들어가면 안 된다. 교과는 내부 데이터이며 플레이어에게 숨긴다. (`tools/validate_data.py`가 검사)
- `SilhouetteOnly`는 `True`/`False`다. 기본값 `True`는 외형을 실루엣으로만 보여준다는 뜻이다.
- CSV의 신 ID 집합은 `docs/lore/01_creation_myth.md`의 ID 집합과 같아야 한다.

## 5. 검증 실행

| 방법 | 명령 | 필요 환경 |
|---|---|---|
| 데이터 검사 | `python3 tools/validate_data.py` | Python 3.9+ (Unreal 불필요) |
| C++ 단위 테스트 | 에디터 *Tools → Session Frontend → Automation* 에서 `Jisik.Data` 실행 | Unreal 에디터 |

## 6. 알려진 한계

- 이 뼈대는 Unreal이 없는 환경에서 작성되어 **C++ 빌드는 아직 검증되지 않았다.**
  처음 빌드할 때 오류가 나면 로그를 공유할 것.
- 기본 맵은 엔진 템플릿(`/Engine/Maps/Templates/OpenWorld`)이다. 게임 맵을 만들면 교체한다.
