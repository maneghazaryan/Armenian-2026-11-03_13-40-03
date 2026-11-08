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
    word_day = {}

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

        # Topics after the alphabet: words shown again must come from earlier days; gap sentences
        # need one [word]; every sentence uses only known letters.
        for wid in lesson.get("review", []):
            if wid not in word_day or word_day[wid] >= day:
                errors.append(f"day {day}: review word '{wid}' is not a word of an earlier day")
        for item in lesson.get("gaps", []) + lesson.get("order", []):
            unknown = sorted({t for t in tokenize(item["hy"]) if t not in known})
            if unknown:
                errors.append(f"day {day}: '{item['hy']}' uses unlearned letters {unknown}")
            if not item.get("ru"):
                errors.append(f"day {day}: '{item['hy']}' has no translation")
        for item in lesson.get("gaps", []):
            if not re.fullmatch(r"[^\[\]]*\[[^\[\]]+\][^\[\]]*", item["hy"]):
                errors.append(f"day {day}: gap sentence '{item['hy']}' needs exactly one [word]")
        for w in lesson["words"]:
            word_day[w["id"]] = day

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


NOUN_ENDINGS = ["ներից", "երից", "ներին", "երին", "ներով", "երով", "ներում", "երում", "ներին", "ները", "երը", "ների", "երի",
                "ներն", "երն", "ներս", "երս", "ներդ", "երդ", "ներ", "եր", "ումս", "ումդ", "ից", "ով", "ում", "ին", "իս", "իդ",
                "ն", "ը", "ի", "ս", "դ", "ու"]
VERB_ENDINGS = [("ացրեցին", ["ացնել"]), ("ացրեց", ["ացնել"]), ("ացրել", ["ացնել"]), ("ացրին", ["ացնել"]),
                ("եցինք", ["ել", "նել"]), ("եցին", ["ել", "նել"]), ("եցիք", ["ել"]), ("եցեք", ["ել"]),
                ("եցիր", ["ել"]), ("եցի", ["ել", "նել"]), ("եցավ", ["ենալ", "անալ"]), ("եցան", ["ենալ", "անալ"]),
                ("եցել", ["ենալ", "անալ"]), ("եցած", ["ենալ", "անալ", "ել"]), ("եց", ["ել", "նել"]),
                ("ացավ", ["անալ", "ենալ"]), ("ացան", ["անալ", "ենալ"]), ("ացար", ["անալ"]), ("ացա", ["անալ"]),
                ("ացել", ["անալ", "ալ"]), ("ացած", ["անալ", "ալ"]), ("ացին", ["ալ", "անալ"]), ("ացի", ["ալ"]), ("աց", ["ալ", "անալ"]),
                ("ելու", ["ել"]), ("ելիս", ["ել"]), ("ելով", ["ել"]), ("ելուց", ["ել"]), ("ելը", ["ել"]),
                ("ալու", ["ալ"]), ("ալիս", ["ալ"]), ("ալով", ["ալ"]), ("ալուց", ["ալ"]),
                ("ում", ["ել", "ալ", "նել"]), ("ված", ["վել"]), ("ած", ["ել", "ալ", "նել"]), ("ող", ["ել", "ալ"]),
                ("ավ", ["նել", "ալ", "ել"]), ("ան", ["նել", "ալ"]), ("ար", ["նել", "ալ"]), ("ա", ["նել", "ալ"]),
                ("ենք", ["ել", "նել"]), ("եք", ["ել", "նել"]), ("են", ["ել", "նել"]), ("եմ", ["ել", "նել"]), ("ես", ["ել", "նել"]),
                ("իր", ["ել", "նել"]), ("ի", ["ել", "նել"]), ("ել", ["ել"]), ("ալ", ["ալ"])]


def guesses(key):
    """Possible dictionary words of an unlisted form; must match ArmenianDictionary.Guesses."""
    for prefix in ("", "կ", "չ"):
        if not key.startswith(prefix) or len(key) - len(prefix) < 2:
            continue
        rest = key[len(prefix):]
        if prefix:
            yield rest
        for end in NOUN_ENDINGS:
            if len(rest) - len(end) >= 2 and rest.endswith(end):
                yield rest[:-len(end)]
        for end, infs in VERB_ENDINGS:
            if len(rest) - len(end) >= 2 and rest.endswith(end):
                for inf in infs:
                    yield rest[:-len(end)] + inf


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

    def exact(word, text_id):
        return forms.get(text_id, {}).get(word) or forms.get("*", {}).get(word)

    def find(word, text_id):
        return exact(word, text_id) or next((exact(g, text_id) for g in guesses(word) if exact(g, text_id)), None)

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
