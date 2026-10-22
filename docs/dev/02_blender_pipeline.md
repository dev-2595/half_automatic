# 개발 02: Blender → Unreal 제작 흐름

> 상태: 초안 (v0.4, 기획자가 Blender 설치 완료, 마네킹 v3: 몸이 하나의 표면) · 출처: 기획자 제안 ("Blender로 오브젝트를 만든 뒤 Unreal에 넣어 보자")
> 아래 설정은 **커뮤니티·업체 문서 기준**이며 Epic·Blender 공식 문서로 검증하지 않았다.
> 이 환경에는 Blender와 Unreal이 없어서 **실제로 내보내거나 불러와 보지는 못했다.** 처음 한 번은 반드시 시험한다.

## 1. 왜 이 방식이 맞는가

[아트 03](../art/03_character_customization.md)의 요구는 공용 뼈대 + 모프 타깃 + 부품 조합이다.
기본 몸(마네킹)은 외부 완제품으로 대체하기 어려워 **직접 제작**해야 하므로, Blender로 만드는 것이 자연스럽다.

| 필요한 것 | Blender | Unreal |
|---|---|---|
| 체격 슬라이더 | Shape Key | Morph Target |
| 공용 뼈대 | Armature | Skeleton |
| 의상 부품 | 같은 Armature에 묶은 별도 메시 | Skeletal Mesh (Leader Pose) |
| 소품·장신구 | 일반 메시 | Static Mesh + Socket |

## 2. 내보내기 설정 (제안, 출처: §5)

| 항목 | 권장 | 이유 |
|---|---|---|
| Apply Modifiers | **끈다** | 켜면 Shape Key가 내보내지지 않는다 (둘을 함께 쓸 수 없음) |
| Add Leaf Bones | **끈다** | 손·발 끝에 불필요한 뼈(`_end`)가 생겨 리타깃팅을 방해한다 |
| 아마추어 루트 이름 | `Armature` 대신 다른 이름 | 루트가 `Armature`면 Unreal이 버릴 수 있다 |
| 스케일 | 아래 §3 중 **하나를 정해 고정** | 문서마다 권장이 다르다 |
| Unreal 가져오기 | Skeletal Mesh, **Import Morph Targets 켬** | 켜지 않으면 Shape Key가 사라진다 |

## 3. 스케일: 출처마다 권장이 다르다

| 방법 | 설명 |
|---|---|
| A | Blender 단위 스케일을 0.01로 두고 Unreal 크기로 모델링 |
| B | 단위 스케일 1.0 유지 + 내보낼 때 `Apply Scalings: FBX All` |
| C | 내보낼 때 `FBX Units Scale`로 설정하고 `Apply Unit`은 끔, Unreal에서 `Convert Scene Unit` 켬 |

**결정 방법:** 높이 1m짜리 기준 큐브를 하나 만들어 위 방법으로 각각 내보내 보고, Unreal에서 100cm로 나오는 방법을 **프로젝트 표준**으로 정한다. 정한 뒤 이 문서를 고친다.

## 4. 저장소 규칙 (제안)

```
Art/Blender/    원본 .blend (Git LFS 권장)
Art/Export/     내보낸 .fbx (Unreal이 읽는 파일)
```

- 이름 규칙: `SK_`(스켈레탈 메시), `SM_`(스태틱 메시), `SKEL_`(스켈레톤), `T_`(텍스처), `M_`/`MI_`(머티리얼). Unreal 커뮤니티에서 널리 쓰는 접두사 규칙이다.
- **모든 캐릭터는 같은 Skeleton을 쓴다.** 새 캐릭터를 만들 때 뼈대를 새로 만들지 않고 기본 몸의 Armature를 복제한다.
- `.blend1`(Blender 자동 백업)은 저장소에 올리지 않는다 (`.gitignore`에 추가함).
- 외부 에셋을 섞을 때는 [CREDITS.md](../../CREDITS.md)에 기록한다.

## 5. 도구와 출처

- Epic의 공식 Blender 애드온 **Send to Unreal**이 있다. 다만 확인한 자료는 **2020년 출시 시점** 것이어서
  현재 Blender·Unreal 버전과 호환되는지는 [공식 저장소](https://github.com/EpicGamesExt/BlenderTools)에서 확인해야 한다.
- UE 5.4부터 FBX 가져오기가 **Interchange**로 바뀌어 대화상자 모양이 이전 버전과 다를 수 있다.
- Blender를 스크립트로 제어하는 `bpy`는 PyPI에서 설치할 수 있다 (이 환경에서 5.x 버전이 조회됨).
  마네킹을 스크립트로 만들면 모델링 과정이 **텍스트로 남아 변경 이력과 재현이 쉽다.**

출처:
- [Blender to UE5: the complete export pipeline](https://sarahhyperdense.substack.com/p/blender-to-ue5-the-complete-export)
- [Blender FBX export to UE (Ready Player Me)](https://docs.readyplayer.me/ready-player-me/integration-guides/unreal-sdk/blender-to-unreal-export)
- [Blender to Unreal export checklist (Cinevva)](https://app.cinevva.com/guides/blender-to-unreal-export-checklist)
- [Epic Games releases official Blender to Unreal Engine tools (CG Channel)](https://www.cgchannel.com/?p=117307)

## 6. 첫 시험 체크리스트

- [ ] 1m 기준 큐브가 Unreal에서 100cm로 나오는가 (스케일 방법 확정)
- [ ] Shape Key 하나(예: 머리 크기)가 Morph Target으로 들어오는가
- [ ] 같은 Skeleton을 쓰는 의상 메시가 몸과 함께 움직이는가
- [ ] 손·발 끝에 불필요한 뼈가 없는가

## 7. 기본 마네킹 빌드 스크립트 (v3)

마네킹을 **스크립트로 생성**한다. 모델링 과정이 텍스트로 남아 수정·재현이 쉽다.

| 파일 | 역할 |
|---|---|
| `tools/blender/build_mannequin.py` | 마네킹 생성 → `.blend` 저장 → `.fbx` 내보내기 → 미리보기 렌더 |
| `tools/blender/verify_mannequin.py` | 산출물이 문서의 요구를 지키는지 검사 (기대값은 빌드 스크립트와 별도로 적음) |
| `Art/Blender/Mannequin.blend` | 생성된 원본 |
| `Art/Export/SK_Mannequin.fbx` | Unreal 로 가져올 파일 |
| `docs/art/reference/mannequin_preview.png` | 앞 / 옆 / 뒤 |
| `docs/art/reference/mannequin_morphs.png` | 체격 모프 타깃 9컷 |

### 실행

```bash
# 방법 1: Blender 가 설치된 경우 (Blender 내장 Python 에 scikit-image 와 scipy 가 없으면 방법 2 를 쓴다)
blender -b -P tools/blender/build_mannequin.py -- --out-root .
blender -b -P tools/blender/verify_mannequin.py -- --root .

# 방법 2: bpy 모듈 (Python 3.13). numpy, scipy, scikit-image 도 함께 설치된다
pip install -r tools/blender/requirements.txt
python3 tools/blender/build_mannequin.py --out-root .
python3 tools/blender/verify_mannequin.py --root .
```

### 만들어지는 것

- **메시 2개**: `SK_Mannequin_Body`(몸통·다리·팔), `SK_Mannequin_Head`(머리). 얼굴을 몸과 분리된 부품으로 두는 방향([아트 03](../art/03_character_customization.md))에 맞춰 머리를 따로 뒀다.
- **몸은 하나로 이어진 단일 표면**이다 (정점 약 11,000개, 삼각형 약 22,000개). 팔과 다리가 몸통에 "붙어 있는" 별개 덩어리가 아니라, 접합부가 부드럽게 섞여 이음새가 없다.
  - 만드는 방법: 몸통·다리·팔을 각각 **거리 함수(SDF)** 로 정의하고, 접합부를 부드럽게 섞은 합집합(smooth union)으로 합친 뒤 marching cubes 로 한 장의 메시로 뽑는다. 두 다리끼리는 섞지 않아 가랑이가 갈라진 채 유지된다.
  - 뼈 가중치와 체격 모프 타깃도 **같은 거리 함수로 부위(몸통·다리·팔) 소속도를 구해** 접합부에서 부드럽게 넘어가게 계산한다. 정점당 영향 뼈는 4개 이하이고, 1% 미만의 영향은 버린다.
- **뼈대 1개** `mannequin_rig`: root, pelvis, spine_01, head, 좌우 upperarm·lowerarm·hand·thigh·calf·foot (16개). 두 메시가 같은 뼈대를 쓴다.
- **체격 모프 타깃**: 값 0~1, 기본값 0

| 메시 | 모프 타깃 |
|---|---|
| Head | `head_big`, `head_small`, `legs_long`, `legs_short` |
| Body | `torso_wide`, `torso_slim`, `legs_long`, `legs_short`, `arms_long`, `arms_short` |

`legs_long`/`legs_short`는 **두 메시에 같은 이름**으로 있다. 키 변화는 몸 전체가 함께 올라가야 하므로, 게임 코드는 두 컴포넌트에 같은 이름·같은 값을 넣어야 한다.

### 검증한 것 / 하지 못한 것

| 구분 | 내용 |
|---|---|
| 검증함 (`verify_mannequin.py`) | 객체·뼈 구성, 정점 가중치 합 1, 모프 타깃 이름·기본값 0·실제 변형, **몸이 하나로 이어진 닫힌 표면**(조각 1개, 구멍 없음, 삼각형 3만 개 이하), 정점당 영향 뼈 4개 이하, 키 1.0 m, 머리:몸 ≈ 1.17:1, 머리 가로/세로 ≈ 1.15, 팔 끝~끝 폭 0.92, 몸통 폭 0.50, 팔 뼈를 돌렸을 때 팔만 움직임, **FBX 를 다시 읽었을 때** 메시·모프 타깃·뼈가 남아 있고 끝 뼈(`_end`)가 없음 |
| 검증 못 함 | **Unreal 에서 가져와 보지 못했다.** 스케일(§3 방법 B `Apply Scalings: FBX All` 로 내보냄), 모프 타깃 인식, 뼈 방향은 §6 체크리스트로 직접 확인해야 한다 |

### 알려진 한계

- `legs_long`, `arms_long` 등 **길이 모프 타깃은 메시만 바꾸고 뼈는 그대로**다. 값이 작을 때는 괜찮지만 크게 쓰면 관절이 어긋나 보일 수 있다. 큰 체격 변화는 Unreal 에서 뼈 스케일로 보완하는 방법을 검토한다.
- **UV 가 없다.** 몸 메시를 수식으로 뽑았기 때문에 텍스처를 입힐 수 없고, 지금은 **단색 머티리얼만** 쓸 수 있다. 몸에 무늬·텍스처가 필요하면 UV 전개를 추가해야 한다. (의상·얼굴은 별도 부품이라 영향 없음)
- 몸이 한 장의 표면이라 **팔·다리를 따로 교체할 수는 없다.** 의상·장신구는 이 몸 위에 얹는 별도 메시로 만든다.
- 삼각형이 불규칙한 격자 모양이다. 애니메이션에는 문제없지만, 손으로 모델링한 메시처럼 에지 흐름이 정돈되어 있지는 않다. 필요하면 Blender 에서 리토폴로지 한다.
- 얼굴·손가락·옷은 없다 (마네킹이므로 의도된 것).
- 미리보기 렌더의 색·조명은 확인용이며 게임 머티리얼이 아니다.
- [스케치 v2](../art/01_character_style.md)를 픽셀로 측정한 값(머리 가로/세로 1.15, 팔 끝~끝 0.92, 몸통 최대 폭 0.50 등)을 기준으로 한다. 모양을 바꾸려면 `build_mannequin.py` 상단의 치수 상수를 고치고 다시 실행한다.
