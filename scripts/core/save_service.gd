class_name SaveService
extends RefCounted
## 저장 파일 I/O와 무결성(체크섬) 검사만 담당한다. 게임 규칙은 모른다.
##
## 파일 형식: {"version": int, "checksum": sha256(payload), "payload": String(JSON)}
## payload를 문자열로 한 번 더 감싸 체크섬이 숫자 표기 차이(1 vs 1.0)에 흔들리지 않게 한다.
## 쓰기는 임시 파일에 먼저 쓰고 rename 하므로, 쓰는 도중 중단돼도 기존 저장본은 보존된다.

const SAVE_VERSION := 1

var _path: String


func _init(path: String = "user://savegame.json") -> void:
	_path = path


func save_state(state: Dictionary, saved_at_unix: float) -> Error:
	var payload_json := JSON.stringify({"state": state, "saved_at": saved_at_unix}, "", true, true)
	var envelope := {
		"version": SAVE_VERSION,
		"checksum": payload_json.sha256_text(),
		"payload": payload_json,
	}
	var tmp_path := _path + ".tmp"
	var file := FileAccess.open(tmp_path, FileAccess.WRITE)
	if file == null:
		return FileAccess.get_open_error()
	file.store_string(JSON.stringify(envelope))
	var write_err := file.get_error()
	file.close()
	if write_err != OK:
		DirAccess.remove_absolute(tmp_path)
		return write_err
	var rename_err := DirAccess.rename_absolute(tmp_path, _path)
	if rename_err != OK:
		DirAccess.remove_absolute(tmp_path)
	return rename_err


## 반환: {"error": Error, "state": Dictionary, "saved_at": float}
## 파일이 없으면 ERR_FILE_NOT_FOUND, 손상/위변조면 ERR_FILE_CORRUPT,
## 지원하지 않는 버전이면 ERR_FILE_UNRECOGNIZED.
func load_state() -> Dictionary:
	if not FileAccess.file_exists(_path):
		return _fail(ERR_FILE_NOT_FOUND)
	var file := FileAccess.open(_path, FileAccess.READ)
	if file == null:
		return _fail(FileAccess.get_open_error())
	var text := file.get_as_text()
	file.close()

	var envelope: Variant = JSON.parse_string(text)
	if typeof(envelope) != TYPE_DICTIONARY:
		return _fail(ERR_FILE_CORRUPT)
	for key in ["version", "checksum", "payload"]:
		if not envelope.has(key):
			return _fail(ERR_FILE_CORRUPT)
	if typeof(envelope["payload"]) != TYPE_STRING or typeof(envelope["checksum"]) != TYPE_STRING:
		return _fail(ERR_FILE_CORRUPT)
	if typeof(envelope["version"]) not in [TYPE_INT, TYPE_FLOAT] or int(envelope["version"]) != SAVE_VERSION:
		return _fail(ERR_FILE_UNRECOGNIZED)

	var payload_json: String = envelope["payload"]
	if payload_json.sha256_text() != envelope["checksum"]:
		return _fail(ERR_FILE_CORRUPT)

	var payload: Variant = JSON.parse_string(payload_json)
	if typeof(payload) != TYPE_DICTIONARY or typeof(payload.get("state")) != TYPE_DICTIONARY:
		return _fail(ERR_FILE_CORRUPT)
	var saved_at: Variant = payload.get("saved_at")
	if typeof(saved_at) not in [TYPE_INT, TYPE_FLOAT] or not is_finite(float(saved_at)):
		return _fail(ERR_FILE_CORRUPT)
	return {"error": OK, "state": payload["state"], "saved_at": float(saved_at)}


## 손상된 저장본을 덮어쓰지 않도록 .corrupt로 치워 둔다.
func quarantine() -> Error:
	if not FileAccess.file_exists(_path):
		return OK
	return DirAccess.rename_absolute(_path, _path + ".corrupt")


func _fail(err: Error) -> Dictionary:
	return {"error": err, "state": {}, "saved_at": 0.0}
