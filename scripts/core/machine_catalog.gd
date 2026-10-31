class_name MachineCatalog
extends RefCounted
## 기계 정의 데이터. 로직을 갖지 않는 순수 데이터 저장소.
##
## base_cost      : 1레벨 구매 비용
## cost_growth    : 레벨당 비용 배율 (cost = base_cost * cost_growth^level)
## output_per_sec : 레벨 1당 초당 생산량
## jam_interval   : 수리 후 고장 나기까지 걸리는 시간(초)

const MACHINES := [
	{"id": "press", "name": "프레스기", "base_cost": 15.0, "cost_growth": 1.15, "output_per_sec": 0.5, "jam_interval": 30.0},
	{"id": "conveyor", "name": "컨베이어", "base_cost": 120.0, "cost_growth": 1.17, "output_per_sec": 4.0, "jam_interval": 45.0},
	{"id": "robot_arm", "name": "로봇팔", "base_cost": 1400.0, "cost_growth": 1.19, "output_per_sec": 30.0, "jam_interval": 60.0},
	{"id": "smelter", "name": "제련로", "base_cost": 18000.0, "cost_growth": 1.21, "output_per_sec": 220.0, "jam_interval": 90.0},
]


static func ids() -> PackedStringArray:
	var result := PackedStringArray()
	for def: Dictionary in MACHINES:
		result.append(def["id"])
	return result


static func has(id: String) -> bool:
	return not get_def(id).is_empty()


## 존재하지 않는 id면 빈 Dictionary를 반환한다.
static func get_def(id: String) -> Dictionary:
	for def: Dictionary in MACHINES:
		if def["id"] == id:
			return def
	return {}
