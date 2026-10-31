extends Control
## UI 전담. 게임 규칙은 FactoryModel, 저장은 GameSession/SaveService에 위임한다.

const AUTOSAVE_INTERVAL := 10.0

var _session: GameSession
var _autosave_left := AUTOSAVE_INTERVAL

var _credits_label: Label
var _rate_label: Label
var _status_label: Label
var _rows: Dictionary = {}  # id -> {"info": Label, "buy": Button, "repair": Button}


func _ready() -> void:
	_session = GameSession.new(SaveService.new())
	_session.start(Time.get_unix_time_from_system())
	_build_ui()
	_show_start_message()
	_refresh()


func _process(delta: float) -> void:
	_session.model.tick(delta)
	_autosave_left -= delta
	if _autosave_left <= 0.0:
		_autosave_left = AUTOSAVE_INTERVAL
		_save()
	_refresh()


func _notification(what: int) -> void:
	if what == NOTIFICATION_WM_CLOSE_REQUEST or what == NOTIFICATION_APPLICATION_PAUSED:
		if _session != null:
			_save()


# ---------------------------------------------------------------- UI 구성

func _build_ui() -> void:
	var margin := MarginContainer.new()
	margin.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	for side in ["left", "right", "top", "bottom"]:
		margin.add_theme_constant_override("margin_" + side, 16)
	add_child(margin)

	var root := VBoxContainer.new()
	root.add_theme_constant_override("separation", 10)
	margin.add_child(root)

	_credits_label = Label.new()
	_credits_label.add_theme_font_size_override("font_size", 28)
	root.add_child(_credits_label)

	_rate_label = Label.new()
	root.add_child(_rate_label)

	var work := Button.new()
	work.text = "직접 작업 (+%d)" % int(FactoryModel.CLICK_POWER)
	work.custom_minimum_size = Vector2(0, 56)
	work.pressed.connect(_on_work_pressed)
	root.add_child(work)

	for id in MachineCatalog.ids():
		root.add_child(_build_row(id))

	_status_label = Label.new()
	_status_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	root.add_child(_status_label)


func _build_row(id: String) -> Control:
	var box := VBoxContainer.new()
	var info := Label.new()
	box.add_child(info)

	var buttons := HBoxContainer.new()
	var buy := Button.new()
	buy.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	buy.pressed.connect(_on_buy_pressed.bind(id))
	buttons.add_child(buy)
	var repair := Button.new()
	repair.text = "수리"
	repair.pressed.connect(_on_repair_pressed.bind(id))
	buttons.add_child(repair)
	box.add_child(buttons)

	_rows[id] = {"info": info, "buy": buy, "repair": repair}
	return box


# ---------------------------------------------------------------- 갱신

func _refresh() -> void:
	var model := _session.model
	_credits_label.text = "크레딧 %s" % NumberFormat.compact(model.credits)
	_rate_label.text = "초당 %s" % NumberFormat.compact(model.current_output())

	for id in MachineCatalog.ids():
		var row: Dictionary = _rows[id]
		var def := MachineCatalog.get_def(id)
		var state := "고장!" if model.is_jammed(id) else "가동 중 (고장까지 %d초)" % ceili(model.get_jam_left(id))
		if model.get_level(id) == 0:
			state = "미구매"
		(row["info"] as Label).text = "%s Lv.%d — %s" % [def["name"], model.get_level(id), state]
		var buy := row["buy"] as Button
		buy.text = "구매 (%s)" % NumberFormat.compact(model.next_cost(id))
		buy.disabled = model.get_level(id) >= FactoryModel.MAX_LEVEL or model.credits < model.next_cost(id)
		(row["repair"] as Button).disabled = not model.is_jammed(id)


func _show_start_message() -> void:
	if _session.load_error != OK:
		_status_label.text = "저장 파일을 읽지 못해 새로 시작합니다 (%s). 기존 파일은 .corrupt로 보관했어요." % error_string(_session.load_error)
	elif _session.offline_gain > 0.0:
		_status_label.text = "자리를 비운 %d분 동안 %s 크레딧을 벌었어요." % [
			int(_session.offline_seconds / 60.0), NumberFormat.compact(_session.offline_gain)]


func _save() -> void:
	var err := _session.save(Time.get_unix_time_from_system())
	if err != OK:
		_status_label.text = "저장 실패: %s" % error_string(err)


# ---------------------------------------------------------------- 입력

func _on_work_pressed() -> void:
	_session.model.manual_work()


func _on_buy_pressed(id: String) -> void:
	var result := _session.model.buy(id)
	if result != FactoryModel.BuyResult.OK:
		_status_label.text = "구매 실패: %s" % FactoryModel.BuyResult.keys()[result]


func _on_repair_pressed(id: String) -> void:
	if not _session.model.repair(id):
		_status_label.text = "수리할 수 없는 상태입니다."
