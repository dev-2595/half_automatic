# half_automatic

교육형 RPG **「지식」** (Unreal Engine 5) 저장소.

## 문서

- [세계관 01: 창세 신화](docs/lore/01_creation_myth.md)
- [아트 01: 캐릭터 그림체](docs/art/01_character_style.md)
- [아트 02: 외부 에셋 선정](docs/art/02_asset_sourcing.md)
- [아트 03: 캐릭터 커스터마이징 요구사항](docs/art/03_character_customization.md)
- [개발 01: Unreal 프로젝트 설정](docs/dev/01_unreal_setup.md)

## 구조

```
Jisik.uproject          Unreal 프로젝트
Source/Jisik/           게임 C++ 모듈
Config/                 프로젝트 설정
Data/                   원본 데이터 (CSV → DataTable)
tools/validate_data.py  데이터 검사 (Unreal 불필요)
docs/                   기획 문서
```
