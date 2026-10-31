class_name FactoryModel
extends RefCounted
## 공장의 순수 게임 로직. UI/파일 I/O/엔진 시간에 의존하지 않는다.
##
## 규칙
##  - 기계는 자동으로 생산하지만 jam_interval이 지나면 고장(jammed)난다.
##  - 고장 난 기계는 repair()로 직접 수리해야 다시 생산한다. (반자동)
##  - tick()은 임의의 delta에 대해 해석적으로 계산하므로 오프라인 보상에도 그대로 쓴다.

enum BuyResult { OK, UNKNOWN_MACHINE, MAX_LEVEL, INSUFFICIENT_FUNDS }

const MAX_OFFLINE_SECONDS := 8.0 * 3600.0
const MAX_LEVEL := 100000
const CLICK_POWER := 1.0

var credits: float = 0.0

var _levels: Dictionary = {}    # id -> int
var _jammed: Dictionary = {}    # id -> bool
var _jam_left: Dictionary = {}  # id -> float (고장까지 남은 초)


func _init() -> void:
	for id in MachineCatalog.ids():
		_levels[id] = 0
		_jammed[id] = false
		_jam_left[id] = _jam_interval(id)


# ---------------------------------------------------------------- 조회

func get_level(id: String) -> int:
	return _levels.get(id, 0)


func is_jammed(id: String) -> bool:
	return _jammed.get(id, false)


func get_jam_left(id: String) -> float:
	return _jam_left.get(id, 0.0)


## 다음 1레벨 구매 비용. 알 수 없는 id면 -1.
func next_cost(id: String) -> float:
	var def := MachineCatalog.get_def(id)
	if def.is_empty():
		return -1.0
	return float(def["base_cost"]) * pow(float(def["cost_growth"]), get_level(id))


## 기계 하나의 현재 초당 생산량(고장 여부와 무관한 정격 값).
func rated_output(id: String) -> float:
	var def := MachineCatalog.get_def(id)
	if def.is_empty():
		return 0.0
	return float(def["output_per_sec"]) * get_level(id)


## 지금 실제로 벌어들이는 초당 생산량(고장난 기계 제외).
func current_output() -> float:
	var total := 0.0
	for id in MachineCatalog.ids():
		if not is_jammed(id):
			total += rated_output(id)
	return total


# ---------------------------------------------------------------- 행동

func manual_work() -> void:
	credits += CLICK_POWER


func buy(id: String) -> BuyResult:
	if not MachineCatalog.has(id):
		return BuyResult.UNKNOWN_MACHINE
	if get_level(id) >= MAX_LEVEL:
		return BuyResult.MAX_LEVEL
	var cost := next_cost(id)
	if credits < cost:
		return BuyResult.INSUFFICIENT_FUNDS
	credits -= cost
	if get_level(id) == 0:
		_jam_left[id] = _jam_interval(id)
		_jammed[id] = false
	_levels[id] = get_level(id) + 1
	return BuyResult.OK


## 고장 난 기계를 수리한다. 수리할 필요가 없거나 id가 잘못되면 false.
func repair(id: String) -> bool:
	if not MachineCatalog.has(id) or not is_jammed(id):
		return false
	_jammed[id] = false
	_jam_left[id] = _jam_interval(id)
	return true


# ---------------------------------------------------------------- 시간 진행

## delta초만큼 진행하고 이번에 번 크레딧을 반환한다.
## 잘못된 delta(음수/NaN/무한대)는 무시한다.
func tick(delta: float) -> float:
	if not is_finite(delta) or delta <= 0.0:
		return 0.0
	var gained := 0.0
	for id in MachineCatalog.ids():
		var level := get_level(id)
		if level <= 0 or is_jammed(id):
			continue
		var productive := minf(delta, get_jam_left(id))
		gained += rated_output(id) * productive
		var left := get_jam_left(id) - delta
		if left <= 0.0:
			_jammed[id] = true
			_jam_left[id] = 0.0
		else:
			_jam_left[id] = left
	credits += gained
	return gained


static func clamp_offline_seconds(seconds: float) -> float:
	if not is_finite(seconds):
		return 0.0
	return clampf(seconds, 0.0, MAX_OFFLINE_SECONDS)


## 오프라인 경과 시간을 적용한다. 상한(MAX_OFFLINE_SECONDS)을 넘는 시간은 버린다.
func apply_offline(seconds: float) -> float:
	return tick(clamp_offline_seconds(seconds))


# ---------------------------------------------------------------- 직렬화

func serialize() -> Dictionary:
	var machines := {}
	for id in MachineCatalog.ids():
		machines[id] = {
			"level": get_level(id),
			"jammed": is_jammed(id),
			"jam_left": get_jam_left(id),
		}
	return {"credits": credits, "machines": machines}


## 전부 검증한 뒤에만 상태를 반영한다(원자성). 하나라도 잘못되면 false이고 상태는 그대로다.
func deserialize(data: Dictionary) -> bool:
	if not data.has("credits") or not _is_valid_number(data["credits"]):
		return false
	var new_credits := float(data["credits"])
	if new_credits < 0.0:
		return false
	if not data.has("machines") or typeof(data["machines"]) != TYPE_DICTIONARY:
		return false

	var machines: Dictionary = data["machines"]
	var new_levels := {}
	var new_jammed := {}
	var new_jam_left := {}
	for id in MachineCatalog.ids():
		# 저장본에 없는 기계(새로 추가된 기계)는 기본값. 알 수 없는 id는 무시.
		if not machines.has(id):
			new_levels[id] = 0
			new_jammed[id] = false
			new_jam_left[id] = _jam_interval(id)
			continue
		var entry: Variant = machines[id]
		if typeof(entry) != TYPE_DICTIONARY:
			return false
		var parsed := _parse_machine(id, entry)
		if parsed.is_empty():
			return false
		new_levels[id] = parsed["level"]
		new_jammed[id] = parsed["jammed"]
		new_jam_left[id] = parsed["jam_left"]

	credits = new_credits
	_levels = new_levels
	_jammed = new_jammed
	_jam_left = new_jam_left
	return true


# ---------------------------------------------------------------- 내부

func _jam_interval(id: String) -> float:
	return float(MachineCatalog.get_def(id)["jam_interval"])


## 유효하면 {level, jammed, jam_left}, 아니면 빈 Dictionary.
func _parse_machine(id: String, entry: Dictionary) -> Dictionary:
	if not entry.has("level") or not _is_valid_number(entry["level"]):
		return {}
	var level_f := float(entry["level"])
	if level_f != floorf(level_f) or level_f < 0.0 or level_f > float(MAX_LEVEL):
		return {}
	if not entry.has("jammed") or typeof(entry["jammed"]) != TYPE_BOOL:
		return {}
	if not entry.has("jam_left") or not _is_valid_number(entry["jam_left"]):
		return {}
	var jam_left := float(entry["jam_left"])
	if jam_left < 0.0 or jam_left > _jam_interval(id):
		return {}
	return {"level": int(level_f), "jammed": entry["jammed"], "jam_left": jam_left}


static func _is_valid_number(value: Variant) -> bool:
	var t := typeof(value)
	if t != TYPE_INT and t != TYPE_FLOAT:
		return false
	return is_finite(float(value))
