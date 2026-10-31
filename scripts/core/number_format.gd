class_name NumberFormat
extends RefCounted
## 큰 수를 한국식 단위(만/억/조/경)로 표기한다.

const _UNITS := [
	{"threshold": 1.0e16, "suffix": "경"},
	{"threshold": 1.0e12, "suffix": "조"},
	{"threshold": 1.0e8, "suffix": "억"},
	{"threshold": 1.0e4, "suffix": "만"},
]


static func compact(value: float) -> String:
	if is_nan(value):
		return "NaN"
	if is_inf(value):
		return "∞" if value > 0.0 else "-∞"
	var sign_str := "-" if value < 0.0 else ""
	var abs_value := absf(value)
	for unit: Dictionary in _UNITS:
		if abs_value >= unit["threshold"]:
			return "%s%.2f%s" % [sign_str, abs_value / unit["threshold"], unit["suffix"]]
	if abs_value < 100.0:
		return "%s%.1f" % [sign_str, abs_value]
	return "%s%d" % [sign_str, int(abs_value)]
