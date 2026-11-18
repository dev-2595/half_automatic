# half_automatic

반자동 공장 방치형 게임 (Unity 6.6 / 6000.7.7f1, C#).

기계는 자동으로 크레딧을 생산하지만 일정 시간이 지나면 **고장**나 멈춥니다. 직접 **수리**해야 다시 가동됩니다.

## 열기
1. Unity Hub → Add → 이 폴더 선택 → 에디터 6000.7.7f1로 열기
2. 첫 오픈 시 Unity가 `Packages/`, `ProjectSettings/` 나머지와 각 파일의 `.meta`를 생성합니다.
   **생성된 `.meta` 파일은 반드시 커밋하세요.** (`Library/` 등은 `.gitignore` 처리됨)
3. 재생(▶): 씬 구성과 관계없이 `AppRoot`가 런타임에 UI를 만듭니다.

## 테스트
- Unity: Window → General → Test Runner → EditMode → Run All
- Unity 없이(.NET 8 SDK 필요): 코어를 Unity와 같은 조건(C# 9, .NET Standard 2.1)으로 컴파일하고 같은 테스트를 실행한다.
  ```bash
  dotnet test tools/CoreTests
  ```

## 구조
| 경로 | 책임 |
|---|---|
| `Assets/Scripts/Core` | 엔진 비의존 순수 C# (규칙·로그인 로직·추상화). `noEngineReferences` |
| `Assets/Scripts/Platform` | Unity/OS 의존 구현 (파일 저장소, 시계) |
| `Assets/Scripts/UI` | uGUI 화면과 앱 진입점 (`AppRoot`) |
| `Assets/Tests/EditMode` | 코어 단위 테스트 |
| `tools/` | Unity 밖 검증용 프로젝트(게임 빌드에 포함되지 않음) |

### 코어 모듈 (`Assets/Scripts/Core`)
| 폴더 | 내용 |
|---|---|
| `Common` | 결과 타입(`Result`), 오류 코드, 시계·직렬화 추상화 |
| `Accounts` | 아이디·비밀번호 규칙, PBKDF2 비밀번호 해시, 계정 서비스(로그인 잠금 포함), 닉네임 규칙 |
| `Persistence` | 체크섬 + 백업 파일 저장소, 세이브/계정 저장 형식과 저장소 |
| `Session` | 게임 규칙 묶음, 계정별 진행 상태(`GameState`), 로그인 흐름(`LoginFlow`) |
| `Balance` | 밸런스 수치(`BalanceConfig`, 임시값 포함)와 비용 곡선 |
| `Economy` | 코인·강화의 서 지갑, 비용 |
| `Jobs` | 직업과 무기 계열 |
| `Progression` | 플레이어 레벨/EXP, 레벨업 계획기(수동·자동 레벨업 공용) |
| `Weapons` | 무기 정의·보유·장착, 자동 합성(최대 5성), 무기 레벨업, 공격력 계산 |
| `Stats` | 체력/공격력/쿨타임 스탯, 최종 능력치 |
| `Loot` | 직업별 드랍 |
| `Tutorial` | 데이터 기반 튜토리얼 엔진과 기본 스크립트(T1~T29) |

## 저장 데이터
`Application.persistentDataPath` 아래에 저장된다.
| 파일 | 내용 |
|---|---|
| `accounts.dat` | 계정 목록(아이디, 솔트, PBKDF2-SHA256 해시). 비밀번호 원문은 저장하지 않는다 |
| `saves/save_{아이디}.dat` | 계정별 진행 데이터(레벨, 코인, 무기, 스탯, 직업, 튜토리얼 진행) |
| `*.bak` | 직전 저장본. 본 파일이 손상되면 자동으로 여기서 복구 |
| `*.corrupt` | 본 파일과 백업이 모두 손상되어 격리한 파일(덮어쓰지 않도록 보관) |

모든 파일은 SHA-256 체크섬으로 손상을 감지하고, 임시 파일에 쓴 뒤 교체한다.

## 알려진 제한
- UI 폰트는 OS 한글 폰트(맑은 고딕 등)를 씁니다. 모바일 빌드 전에 한글 폰트 에셋을 포함해야 합니다.
- 계정은 기기에만 저장되는 로컬 계정입니다. 기기를 가진 사람이 파일을 직접 고칠 수 있으므로 "본인 확인" 이상의 보안은 제공하지 않습니다.
