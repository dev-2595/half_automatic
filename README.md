# half_automatic

반자동 공장 방치형 게임 (Unity 6.6 / 6000.7.7f1, C#).

기계는 자동으로 크레딧을 생산하지만 일정 시간이 지나면 **고장**나 멈춥니다. 직접 **수리**해야 다시 가동됩니다.

## 열기
1. Unity Hub → Add → 이 폴더 선택 → 에디터 6000.7.7f1로 열기
2. 첫 오픈 시 Unity가 `Packages/`, `ProjectSettings/` 나머지와 각 파일의 `.meta`를 생성합니다.
   **생성된 `.meta` 파일은 반드시 커밋하세요.** (`Library/` 등은 `.gitignore` 처리됨)
3. 재생(▶): 씬 구성과 관계없이 `AppRoot`가 런타임에 UI를 만듭니다.

## 테스트
Window → General → Test Runner → EditMode → Run All

## 구조
| 경로 | 책임 |
|---|---|
| `Assets/Scripts/Core` | 엔진 비의존 순수 C# (규칙·로그인 로직·추상화). `noEngineReferences` |
| `Assets/Scripts/Platform` | Unity/OS 의존 구현 (파일 저장소, 시계) |
| `Assets/Scripts/UI` | uGUI 화면과 앱 진입점 (`AppRoot`) |
| `Assets/Tests/EditMode` | 코어 단위 테스트 |

## 알려진 제한
- UI 폰트는 OS 한글 폰트(맑은 고딕 등)를 씁니다. 모바일 빌드 전에 한글 폰트 에셋을 포함해야 합니다.
