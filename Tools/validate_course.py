#!/usr/bin/env python3
"""Validate the course data.

Checks that every word and phrase of a lesson uses only letters taught on that
day or earlier, that ids are unique and that each letter is introduced once.

    python3 Tools/validate_course.py                 # audio missing -> warning
    python3 Tools/validate_course.py --require-audio # audio missing -> error
"""
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
DATA = ROOT / "Assets" / "Resources" / "Data"
AUDIO = ROOT / "Assets" / "Resources" / "Audio"
AUDIO_EXTS = (".mp3", ".ogg", ".wav")


def tokenize(text):
    """Split Armenian text into letters; "ու" and "և" count as one letter."""
    text = text.lower()
    tokens, i = [], 0
    while i < len(text):
        if text.startswith("ու", i):
            tokens.append("ու")
            i += 2
            continue
        ch = text[i]
        if "ա" <= ch <= "և":  # Armenian lowercase block, incl. և
            tokens.append(ch)
        i += 1
    return tokens


def has_audio(audio_id):
    return any((AUDIO / (audio_id + ext)).exists() for ext in AUDIO_EXTS)


def main():
    require_audio = "--require-audio" in sys.argv
    letters = json.loads((DATA / "letters.json").read_text(encoding="utf-8"))["letters"]
    lessons = json.loads((DATA / "lessons.json").read_text(encoding="utf-8"))["lessons"]

    by_id = {l["id"]: l for l in letters}
    errors, warnings = [], []
    known, seen_ids, introduced = set(), set(), set()

    for lesson in sorted(lessons, key=lambda l: l["day"]):
        day = lesson["day"]
        for lid in lesson["newLetters"]:
            if lid not in by_id:
                errors.append(f"day {day}: unknown letter id '{lid}'")
                continue
            if lid in introduced:
                errors.append(f"day {day}: letter '{lid}' already introduced")
            introduced.add(lid)
            known.add(by_id[lid]["lower"])
            if not has_audio("l_" + lid):
                warnings.append(f"day {day}: no audio for letter l_{lid}")

        for item in lesson["words"] + lesson.get("phrases", []) + lesson.get("verses", []):
            if item["id"] in seen_ids:
                errors.append(f"day {day}: duplicate id '{item['id']}'")
            seen_ids.add(item["id"])
            unknown = sorted({t for t in tokenize(item["hy"]) if t not in known})
            if unknown:
                errors.append(f"day {day}: '{item['hy']}' uses unlearned letters {unknown}")
            if not has_audio("w_" + item["id"]):
                warnings.append(f"day {day}: no audio for w_{item['id']}")

    for w in warnings:
        print("WARN ", w)
    for e in errors:
        print("ERROR", e)
    missing_audio = require_audio and warnings
    print(f"{len(lessons)} lessons, {len(introduced)}/{len(letters)} letters, "
          f"{len(errors)} errors, {len(warnings)} warnings")
    return 1 if errors or missing_audio else 0


if __name__ == "__main__":
    sys.exit(main())
