class_name GameSession
extends RefCounted
## 모델과 저장소를 엮는 조정자. 시작 시 불러오기 + 오프라인 보상, 주기 저장을 담당한다.

var model := FactoryModel.new()

## start() 결과 (UI가 안내 문구를 만들 때 사용)
var offline_seconds: float = 0.0
var offline_gain: float = 0.0
var load_error: Error = OK  # ERR_FILE_NOT_FOUND(새 게임)은 오류로 기록하지 않는다.

var _store: SaveService


func _init(store: SaveService) -> void:
	_store = store


func start(now_unix: float) -> void:
	var result := _store.load_state()
	var err: Error = result["error"]
	if err == ERR_FILE_NOT_FOUND:
		return
	if err != OK:
		load_error = err
		_store.quarantine()
		return

	var loaded := FactoryModel.new()
	if not loaded.deserialize(result["state"]):
		load_error = ERR_INVALID_DATA
		_store.quarantine()
		return

	model = loaded
	# 시계가 과거로 돌아간 경우(음수)는 0초로 취급한다.
	var elapsed := maxf(0.0, now_unix - float(result["saved_at"]))
	offline_seconds = FactoryModel.clamp_offline_seconds(elapsed)
	offline_gain = model.apply_offline(elapsed)


func save(now_unix: float) -> Error:
	return _store.save_state(model.serialize(), now_unix)
