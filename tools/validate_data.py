"""Unreal 없이 Data/ 의 원본 데이터를 검사한다.

검사 항목
  1. Deities.csv 형식: UTF-8 BOM, 헤더, 중복/빈 ID
  2. Rank/Subject 값이 C++ UENUM(JisikTypes.h)에 실제로 존재하는지
  3. 세계관 규칙 (FDeityTableValidator 와 동일)
  4. CSV 의 신 ID 집합이 세계관 문서(docs/lore)의 ID 집합과 일치하는지

사용법: python3 tools/validate_data.py   (성공 시 종료 코드 0)
"""

from __future__ import annotations

import csv
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
CSV_PATH = ROOT / "Data" / "Deities.csv"
TYPES_HEADER = ROOT / "Source" / "Jisik" / "Public" / "Data" / "JisikTypes.h"
LORE_DOC = ROOT / "docs" / "lore" / "01_creation_myth.md"

EXPECTED_HEADER = ["Name", "DisplayName", "Rank", "Subject"]
UTF8_BOM = b"\xef\xbb\xbf"


def parse_enum(header_text: str, enum_name: str) -> list[str]:
    """`enum class <name> : uint8 { ... };` 본문에서 Hidden 이 아닌 항목 이름을 순서대로 반환."""
    match = re.search(rf"enum\s+class\s+{enum_name}\b[^{{]*\{{(.*?)\}};", header_text, re.S)
    if not match:
        raise ValueError(f"enum {enum_name} not found in {TYPES_HEADER.name}")
    body = re.sub(r"/\*.*?\*/|//[^\n]*", "", match.group(1), flags=re.S)
    names = []
    for entry in body.split(","):
        entry = entry.strip()
        if not entry or "UMETA(Hidden)" in entry.replace(" ", ""):
            continue
        names.append(re.match(r"\w+", entry).group(0))
    return names


def lore_deity_ids(lore_text: str) -> set[str]:
    return set(re.findall(r"`(GOD_[A-Z_]+)`", lore_text))


def validate() -> list[str]:
    errors: list[str] = []

    raw = CSV_PATH.read_bytes()
    if not raw.startswith(UTF8_BOM):
        errors.append(f"{CSV_PATH.name}: must start with UTF-8 BOM (Unreal may misread Korean otherwise).")

    rows = list(csv.reader(raw.decode("utf-8-sig").splitlines()))
    if not rows or rows[0] != EXPECTED_HEADER:
        return errors + [f"{CSV_PATH.name}: header must be {EXPECTED_HEADER}, got {rows[0] if rows else None}."]

    header_text = TYPES_HEADER.read_text(encoding="utf-8")
    ranks = set(parse_enum(header_text, "EDeityRank"))
    subjects = parse_enum(header_text, "ESubject")
    real_subjects = [s for s in subjects if s != "None"]

    seen_ids: set[str] = set()
    supreme_count = 0
    subject_counts = {s: 0 for s in real_subjects}

    for line_no, row in enumerate(rows[1:], start=2):
        where = f"{CSV_PATH.name}:{line_no}"
        if len(row) != len(EXPECTED_HEADER):
            errors.append(f"{where}: expected {len(EXPECTED_HEADER)} columns, got {len(row)}.")
            continue
        deity_id, display_name, rank, subject = (cell.strip() for cell in row)

        if not deity_id:
            errors.append(f"{where}: empty Name.")
        elif deity_id in seen_ids:
            errors.append(f"{where}: duplicate Name {deity_id}.")
        seen_ids.add(deity_id)

        if not display_name:
            errors.append(f"{where}: empty DisplayName.")
        if rank not in ranks:
            errors.append(f"{where}: Rank '{rank}' is not in EDeityRank {sorted(ranks)}.")
            continue
        if subject not in subjects:
            errors.append(f"{where}: Subject '{subject}' is not in ESubject {subjects}.")
            continue

        if rank == "Supreme":
            supreme_count += 1
            if subject != "None":
                errors.append(f"{where}: supreme deity must have Subject None.")
        elif subject == "None":
            errors.append(f"{where}: kin deity must have a subject.")
        else:
            subject_counts[subject] += 1

    if supreme_count != 1:
        errors.append(f"Expected exactly 1 supreme deity, found {supreme_count}.")
    for subject, count in subject_counts.items():
        if count != 1:
            errors.append(f"Subject {subject} must have exactly 1 kin deity, found {count}.")

    lore_ids = lore_deity_ids(LORE_DOC.read_text(encoding="utf-8"))
    if seen_ids != lore_ids:
        errors.append(
            "Deity IDs differ from lore doc: "
            f"only in CSV={sorted(seen_ids - lore_ids)}, only in doc={sorted(lore_ids - seen_ids)}."
        )

    return errors


def main() -> int:
    try:
        errors = validate()
    except (OSError, ValueError, UnicodeDecodeError) as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 2

    if errors:
        for error in errors:
            print(f"FAIL: {error}", file=sys.stderr)
        return 1
    print("OK: Data/Deities.csv is valid.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
