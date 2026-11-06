#!/usr/bin/env python3
"""Validate the course data.

Checks that every word and phrase of a lesson uses only letters taught on that
day or earlier, that intros and letter explanations mention only letters already
learned, that ids are unique and that each letter is introduced once. The word
days after the alphabet (words.json) are checked the same way. For the Armenian
texts (texts_hy.json), every word must be in dictionary.txt.

    python3 Tools/validate_course.py                 # audio missing -> warning
    python3 Tools/validate_course.py --require-audio # audio missing -> error
"""
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
DATA = ROOT / "Assets" / "Resources" / "Data"
AUDIO = ROOT / "Assets" / "Resources" / "Audio"
AUDIO_EXTS = (".mp3", ".ogg", ".wav")


def tokenize(text):
    """Split Armenian text into letters; "ու" and "և" count as one letter."""
    text = text.lower().replace("՛", "").replace("՜", "").replace("՞", "")  # marks inside words
    tokens, i = [], 0
    while i < len(text):
        if text.startswith("ու", i):
            tokens.append("ու")
            i += 2
            continue
        ch = text[i]
        if "ա" <= ch <= "և":  # Armenian lowercase block, incl. և and a lone ւ
            tokens.append(ch)
        i += 1
    return tokens


def glyphs(letter):
    """Tokens a learned letter makes readable: ու also teaches its hook ւ."""
    return {letter["lower"]} | ({"ւ"} if letter["lower"] == "ու" else set())


def has_audio(audio_id):
    return any((AUDIO / (audio_id + ext)).exists() for ext in AUDIO_EXTS)


def main():
    require_audio = "--require-audio" in sys.argv
    letters = json.loads((DATA / "letters.json").read_text(encoding="utf-8"))["letters"]
    lessons = json.loads((DATA / "lessons.json").read_text(encoding="utf-8"))["lessons"]
    lessons += json.loads((DATA / "words.json").read_text(encoding="utf-8"))["lessons"]

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
            known.update(glyphs(by_id[lid]))
            if not has_audio("l_" + lid):
                warnings.append(f"day {day}: no audio for letter l_{lid}")

        # Intros and letter explanations may only show letters known by then
        # (letters of the same day count once they have been introduced).
        unknown = sorted({t for t in tokenize(lesson["intro"]) if t not in known})
        if unknown:
            errors.append(f"day {day}: intro uses unlearned letters {unknown}")
        seen_today = set(known) - {g for i in lesson["newLetters"] if i in by_id for g in glyphs(by_id[i])}
        for lid in lesson["newLetters"]:
            if lid not in by_id:
                continue
            seen_today.update(glyphs(by_id[lid]))
            for field in ("hint", "look", "warn", "more"):
                unknown = sorted({t for t in tokenize(by_id[lid].get(field, "")) if t not in seen_today})
                if unknown:
                    errors.append(f"day {day}: letter {lid} {field} mentions unlearned letters {unknown}")

        for item in lesson["words"] + lesson.get("phrases", []) + lesson.get("verses", []):
            if item["id"] in seen_ids:
                errors.append(f"day {day}: duplicate id '{item['id']}'")
            seen_ids.add(item["id"])
            unknown = sorted({t for t in tokenize(item["hy"]) if t not in known})
            if unknown:
                errors.append(f"day {day}: '{item['hy']}' uses unlearned letters {unknown}")
            if not has_audio("w_" + item["id"]):
                warnings.append(f"day {day}: no audio for w_{item['id']}")

    errors += check_dictionary()

    for w in warnings:
        print("WARN ", w)
    for e in errors:
        print("ERROR", e)
    missing_audio = require_audio and warnings
    print(f"{len(lessons)} lessons, {len(introduced)}/{len(letters)} letters, "
          f"{len(errors)} errors, {len(warnings)} warnings")
    return 1 if errors or missing_audio else 0


def normalize(word):
    """Lowercase, letters and inner hyphens only; must match ArmenianDictionary.Normalize."""
    return "".join(c for c in word.lower() if c.isalpha() or c == "-").strip("-")


def check_dictionary():
    """Every word of every Armenian text has an entry; no form means two things in one section."""
    texts = json.loads((DATA / "texts_hy.json").read_text(encoding="utf-8"))["texts"]
    errors, forms, section = [], {}, "*"
    lines = (DATA / "dictionary.txt").read_text(encoding="utf-8").split("\n")
    for n, raw in enumerate(lines, 1):
        line = raw.strip()
        if not line or line.startswith("#"):
            continue
        if line.startswith("[") and line.endswith("]"):
            section = line[1:-1].strip()
            continue
        halves = line.split("|")
        head = halves[0].split(" = ", 1)
        if len(head) != 2 or len(halves) > 2:
            errors.append(f"dictionary line {n}: expected «слово = значение | формы»")
            continue
        hy = head[0].strip()
        items = [(hy, None)]
        if len(halves) == 2:
            # «;» before an Armenian word separates forms; inside a Russian note it does not.
            for part in re.split(r";\s*(?=[\u0531-\u0587])", halves[1]):
                if part.strip():
                    f = part.split(" = ", 1)
                    items.append((f[0].strip(), f[1].strip() if len(f) == 2 else None))
        for form, note in items:
            key, known = normalize(form), forms.setdefault(section, {})
            if " " in form:
                errors.append(f"dictionary line {n}: form «{form}» has a space")
            elif key in known and known[key] != (hy, note):
                errors.append(f"dictionary line {n}: «{form}» already means «{known[key][0]}»")
            known.setdefault(key, (hy, note))

    def find(word, text_id):
        return forms.get(text_id, {}).get(word) or forms.get("*", {}).get(word)

    for text in texts:
        missing = []
        for chunk in text["text"].split():
            word = normalize(chunk)
            if not word or find(word, text["id"]):
                continue
            for part in word.split("-"):
                if part and not find(part, text["id"]) and part not in missing:
                    missing.append(part)
        if missing:
            errors.append(f"text {text['id']}: not in dictionary: {' '.join(missing)}")
    return errors


if __name__ == "__main__":
    sys.exit(main())
