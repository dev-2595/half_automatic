# 개발 02: Blender → Unreal 제작 흐름

> 상태: 초안 (v0.2, 기획자가 Blender 설치 완료) · 출처: 기획자 제안 ("Blender로 오브젝트를 만든 뒤 Unreal에 넣어 보자")
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
