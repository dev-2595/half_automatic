# 지식 (Knowledge) — 아트 02: 외부 에셋 선정

> 상태: 후보 조사 (v0.2, 아트 03 반영해 권장안 수정) · 기획자 승인: "그림체에 맞는 에셋 사용 가능"
> 기준 그림체: [아트 01: 캐릭터 그림체](01_character_style.md)

## 1. 선정 기준

| # | 기준 | 근거 |
|---|---|---|
| 1 | 약 2등신, 큰 원형 머리, 짧고 뭉툭한 다리, 손가락 없는 손 | 아트 01 §2 |
| 2 | 얼굴·옷을 덧입힐 수 있는 단순한 몸 (또는 이미 단순한 캐릭터) | 아트 01 §1 (마네킹) |
| 3 | 리깅·애니메이션 포함 (걷기, 대기 등) | RPG 이동·연출 |
| 4 | Unreal에서 가져올 수 있는 형식 (FBX 또는 glTF) | 엔진 |
| 5 | **상업적 이용 가능, 재배포 제한 없음 (CC0 우선)** | 저장소에 포함해야 하므로 |

## 2. 후보

라이선스·구성 정보는 아래 출처 기준이다. 이 환경에서는 배포 사이트 접속이 막혀 있어
**원문 페이지와 실제 모델 외형은 내려받을 때 직접 확인**해야 한다.

| 후보 | 구성 | 라이선스 | 그림체 적합도 (예상) | 출처 |
|---|---|---|---|---|
| **Kenney – Mini Characters** | 캐릭터 12종, 캐릭터마다 애니메이션 32개, 약 2.4MB | CC0 (출처 표기 권장, 필수 아님) | 높음: 작고 단순한 저폴리 캐릭터 | [OpenGameArt](https://opengameart.org/content/mini-character-1) |
| **KayKit – Adventurers** (Kay Lousberg) | 무료판 캐릭터 4~5종(유료 EXTRA는 3종 더), 무기·장신구 25개 이상, FBX/glTF, 1024 그라디언트 텍스처 1장 | CC0 (출처 표기 불필요) | 중간: 2등신대 치비지만 얼굴·옷이 이미 그려져 있음 | [itch.io](https://kaylousberg.itch.io/kaykit-adventurers), [Godot Asset Library](https://godotengine.org/asset-library/asset/edit/11829) |
| **Quaternius – LowPoly RPG Characters** | 캐릭터 6종, FBX/OBJ/Blend | CC0 | 낮음: 등신이 높은 일반 저폴리 비율 | [OpenGameArt](https://opengameart.org/content/lowpoly-rpg-characters) |

## 3. 권장안

> ⚠ 기획자 요구(체격 조정·복잡한 얼굴·화려한 의상, [아트 03](03_character_customization.md))에 따라 수정함.
> 완성된 캐릭터 팩은 메시가 고정되어 있을 가능성이 높아 **캐릭터 본체로는 쓰지 않는다.**

1. **기본 몸(마네킹): 직접 제작.** 공용 뼈대와 모프 타깃을 포함해야 하므로 외부 완제품으로 대체하기 어렵다.
2. **Kenney Mini Characters**: 비율·애니메이션 **참고 자료**와 개발 초기 **임시 모델**로 사용.
3. **KayKit Adventurers**: 무기·장신구 같은 **소품 부품**으로 사용 (의상 슬롯의 일부).
4. 내려받은 뒤 파일 구조(메시 분리 여부, 리그, 모프 유무)를 확인하고 이 문서를 고친다.

## 4. 도입 절차

1. 배포 페이지에서 **라이선스 문구를 다시 확인**하고, 내려받은 압축 파일의 `License.txt`를 함께 보관한다.
2. 원본은 `ThirdParty/<제작자>/<팩 이름>/`에 압축을 푼 상태로 둔다.
3. Unreal에서 FBX를 `Content/ThirdParty/<제작자>/<팩 이름>/`으로 가져온다.
4. 저장소 루트의 `CREDITS.md`에 팩 이름, 제작자, 라이선스, 출처 URL, 내려받은 날짜를 기록한다.

## 5. 저장소 관리 (결정 필요)

- [ ] `.uasset`, `.umap`, `.fbx` 같은 바이너리 파일은 용량이 커서 **Git LFS** 사용을 권장한다.
      LFS를 쓰면 모든 작업자 PC에 `git lfs`가 설치되어 있어야 하고, GitHub LFS 용량 한도가 적용된다.
