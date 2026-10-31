# half_automatic

반자동 공장 방치형 게임 (Godot 4.4, GDScript).

기계는 자동으로 크레딧을 생산하지만 일정 시간이 지나면 **고장**나 멈춥니다.
직접 **수리**해야 다시 가동되므로, 자리를 비우면 고장 시점까지만 오프라인 보상을 받습니다.

## 실행
Godot 4.4+ 에서 이 폴더를 프로젝트로 열고 실행(F5)합니다.

## 테스트 (헤드리스)
```bash
godot --headless --import
godot --headless -s res://tests/run_tests.gd
```

## 구조
| 파일 | 책임 |
|---|---|
| `scripts/core/machine_catalog.gd` | 기계 데이터 정의 |
| `scripts/core/factory_model.gd` | 순수 게임 로직(구매/생산/고장/수리/오프라인/직렬화·검증) |
| `scripts/core/save_service.gd` | 저장 I/O, 체크섬, 원자적 쓰기 |
| `scripts/core/game_session.gd` | 모델+저장소 조정, 오프라인 보상 |
| `scripts/core/number_format.gd` | 숫자 표기(만/억/조/경) |
| `scripts/ui/main.gd` | UI |
