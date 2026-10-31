extends SceneTree
## 헤드리스 단위 테스트.
##   godot --headless --import
##   godot --headless -s res://tests/run_tests.gd
## 실패가 하나라도 있으면 종료 코드 1.

const TEST_SAVE := "user://__test_save.json"

var _failures := 0
var _checks := 0


func _init() -> void:
	_test_catalog()
	_test_buy()
	_test_tick_and_jam()
	_test_repair()
	_test_offline()
	_test_tick_rejects_bad_delta()
	_test_serialize_roundtrip()
	_test_deserialize_rejects_invalid_atomically()
	_test_save_load_and_tamper()
	_test_session_offline_reward()
	print("%d checks, %d failures" % [_checks, _failures])
	quit(1 if _failures > 0 else 0)


func _check(cond: bool, label: String) -> void:
	_checks += 1
	if not cond:
		_failures += 1
		printerr("FAIL: " + label)


func _rich_model() -> FactoryModel:
	var m := FactoryModel.new()
	m.credits = 1.0e9
	return m


func _test_catalog() -> void:
	_check(MachineCatalog.ids().size() == MachineCatalog.MACHINES.size(), "catalog ids")
	_check(MachineCatalog.has("press"), "has press")
	_check(not MachineCatalog.has("nope"), "no nope")
	_check(MachineCatalog.get_def("nope").is_empty(), "get_def unknown empty")


func _test_buy() -> void:
	var m := FactoryModel.new()
	_check(m.buy("press") == FactoryModel.BuyResult.INSUFFICIENT_FUNDS, "buy w/o funds")
	_check(m.buy("nope") == FactoryModel.BuyResult.UNKNOWN_MACHINE, "buy unknown")
	m.credits = 15.0
	_check(m.buy("press") == FactoryModel.BuyResult.OK, "buy press")
	_check(m.get_level("press") == 1 and is_equal_approx(m.credits, 0.0), "level/credits after buy")
	_check(is_equal_approx(m.next_cost("press"), 15.0 * 1.15), "cost grows")
	_check(m.next_cost("nope") == -1.0, "cost unknown")


func _test_tick_and_jam() -> void:
	var m := _rich_model()
	m.buy("press")
	var before := m.credits
	var gained := m.tick(10.0)
	_check(is_equal_approx(gained, 5.0), "10s * 0.5/s = 5")
	_check(is_equal_approx(m.credits, before + 5.0), "credits increased")
	# 고장 시점(30초)을 넘기는 tick: 남은 20초만 생산하고 고장.
	gained = m.tick(100.0)
	_check(is_equal_approx(gained, 10.0), "only 20s productive before jam")
	_check(m.is_jammed("press"), "jammed")
	_check(m.tick(50.0) == 0.0, "jammed produces nothing")


func _test_repair() -> void:
	var m := _rich_model()
	m.buy("press")
	_check(not m.repair("press"), "cannot repair healthy")
	m.tick(31.0)
	_check(m.repair("press"), "repair jammed")
	_check(not m.is_jammed("press") and is_equal_approx(m.get_jam_left("press"), 30.0), "timer reset")
	_check(not m.repair("nope"), "repair unknown")


func _test_offline() -> void:
	var m := _rich_model()
	m.buy("press")
	var gained := m.apply_offline(3600.0)
	_check(is_equal_approx(gained, 15.0), "offline limited by jam (30s*0.5)")
	_check(FactoryModel.clamp_offline_seconds(-5.0) == 0.0, "clamp negative")
	_check(FactoryModel.clamp_offline_seconds(INF) == 0.0, "clamp inf")
	_check(FactoryModel.clamp_offline_seconds(1.0e9) == FactoryModel.MAX_OFFLINE_SECONDS, "clamp max")


func _test_tick_rejects_bad_delta() -> void:
	var m := _rich_model()
	m.buy("press")
	var c := m.credits
	_check(m.tick(-1.0) == 0.0 and m.tick(NAN) == 0.0 and m.tick(INF) == 0.0, "bad delta ignored")
	_check(m.credits == c, "credits unchanged by bad delta")


func _test_serialize_roundtrip() -> void:
	var a := _rich_model()
	a.buy("press")
	a.buy("conveyor")
	a.tick(35.0)
	var b := FactoryModel.new()
	_check(b.deserialize(a.serialize()), "deserialize ok")
	_check(b.get_level("press") == 1 and b.get_level("conveyor") == 1, "levels restored")
	_check(b.is_jammed("press") == a.is_jammed("press"), "jam restored")
	_check(is_equal_approx(b.credits, a.credits), "credits restored")


func _test_deserialize_rejects_invalid_atomically() -> void:
	var m := _rich_model()
	m.buy("press")
	var snapshot := m.serialize()
	var bad_cases := [
		{},
		{"credits": -1.0, "machines": {}},
		{"credits": "x", "machines": {}},
		{"credits": 1.0, "machines": []},
		{"credits": 1.0, "machines": {"press": {"level": 1.5, "jammed": false, "jam_left": 1.0}}},
		{"credits": 1.0, "machines": {"press": {"level": -1, "jammed": false, "jam_left": 1.0}}},
		{"credits": 1.0, "machines": {"press": {"level": 1, "jammed": "no", "jam_left": 1.0}}},
		{"credits": 1.0, "machines": {"press": {"level": 1, "jammed": false, "jam_left": 9999.0}}},
		{"credits": 1.0, "machines": {"press": {"level": 1, "jammed": false, "jam_left": INF}}},
		{"credits": INF, "machines": {}},
	]
	for i in bad_cases.size():
		_check(not m.deserialize(bad_cases[i]), "reject bad case %d" % i)
	_check(m.serialize() == snapshot, "state untouched after rejected loads")


func _test_save_load_and_tamper() -> void:
	DirAccess.remove_absolute(TEST_SAVE)
	var store := SaveService.new(TEST_SAVE)
	_check(store.load_state()["error"] == ERR_FILE_NOT_FOUND, "missing file")
	var m := _rich_model()
	m.buy("press")
	_check(store.save_state(m.serialize(), 1000.0) == OK, "save ok")
	var loaded := store.load_state()
	_check(loaded["error"] == OK and loaded["saved_at"] == 1000.0, "load ok")
	_check(FactoryModel.new().deserialize(loaded["state"]), "loaded state valid")

	# 변조: payload 안의 값을 바꾸면 체크섬 불일치.
	var f := FileAccess.open(TEST_SAVE, FileAccess.READ)
	var text := f.get_as_text()
	f.close()
	var tampered := text.replace("1000", "9999")
	var w := FileAccess.open(TEST_SAVE, FileAccess.WRITE)
	w.store_string(tampered)
	w.close()
	_check(store.load_state()["error"] == ERR_FILE_CORRUPT, "tamper detected")

	# 깨진 JSON
	w = FileAccess.open(TEST_SAVE, FileAccess.WRITE)
	w.store_string("{not json")
	w.close()
	_check(store.load_state()["error"] == ERR_FILE_CORRUPT, "garbage detected")
	_check(store.quarantine() == OK and not FileAccess.file_exists(TEST_SAVE), "quarantined")
	DirAccess.remove_absolute(TEST_SAVE + ".corrupt")


func _test_session_offline_reward() -> void:
	DirAccess.remove_absolute(TEST_SAVE)
	var store := SaveService.new(TEST_SAVE)
	var first := GameSession.new(store)
	first.start(0.0)
	_check(first.load_error == OK, "new game has no error")
	first.model.credits = 1.0e6
	first.model.buy("press")
	_check(first.save(1000.0) == OK, "session save")

	var second := GameSession.new(store)
	second.start(1000.0 + 3600.0)
	_check(second.load_error == OK, "session load ok")
	_check(is_equal_approx(second.offline_gain, 15.0), "offline reward applied")
	_check(second.model.is_jammed("press"), "machine jammed after long absence")

	# 시계가 과거로 돌아가면 보상 0.
	var third := GameSession.new(store)
	third.start(500.0)
	_check(third.offline_gain == 0.0 and third.offline_seconds == 0.0, "clock rollback gives nothing")
	DirAccess.remove_absolute(TEST_SAVE)
