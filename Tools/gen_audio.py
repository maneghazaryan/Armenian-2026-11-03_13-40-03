#!/usr/bin/env python3
"""Generate pronunciation audio for letters, words and phrases.

Output goes to Assets/Resources/Audio as l_<letterId>.mp3 and w_<itemId>.mp3.
Existing files are never overwritten, so clips recorded by a native speaker
(same names, .mp3/.ogg/.wav) always win over generated ones.

Backend: Azure Speech free tier (F0, 500k characters/month free), voices
hy-AM-AnahitNeural / hy-AM-HaykNeural. The key is used only here at build
time and is never shipped inside the app.

    export AZURE_SPEECH_KEY=...  AZURE_SPEECH_REGION=westeurope
    python3 Tools/gen_audio.py [--voice hy-AM-HaykNeural] [--dry-run]
"""
import json
import os
import sys
import urllib.request
from xml.sax.saxutils import escape

from validate_course import AUDIO, DATA, has_audio

DEFAULT_VOICE = "hy-AM-AnahitNeural"


def items_to_speak():
    letters = json.loads((DATA / "letters.json").read_text(encoding="utf-8"))["letters"]
    lessons = json.loads((DATA / "lessons.json").read_text(encoding="utf-8"))["lessons"]
    for letter in letters:
        yield "l_" + letter["id"], letter["name"]
    for lesson in lessons:
        for item in lesson["words"] + lesson.get("phrases", []) + lesson.get("verses", []):
            yield "w_" + item["id"], item["hy"]


def azure_tts(text, voice, key, region):
    ssml = (
        "<speak version='1.0' xml:lang='hy-AM'>"
        f"<voice name='{voice}'><prosody rate='-15%'>{escape(text)}</prosody></voice>"
        "</speak>"
    )
    req = urllib.request.Request(
        f"https://{region}.tts.speech.microsoft.com/cognitiveservices/v1",
        data=ssml.encode("utf-8"),
        headers={
            "Ocp-Apim-Subscription-Key": key,
            "Content-Type": "application/ssml+xml",
            "X-Microsoft-OutputFormat": "audio-24khz-48kbitrate-mono-mp3",
            "User-Agent": "aib-gen-audio",
        },
    )
    with urllib.request.urlopen(req, timeout=30) as resp:
        return resp.read()


def main():
    args = sys.argv[1:]
    dry_run = "--dry-run" in args
    voice = args[args.index("--voice") + 1] if "--voice" in args else DEFAULT_VOICE
    todo = [(aid, text) for aid, text in items_to_speak() if not has_audio(aid)]
    print(f"{len(todo)} clips missing")
    if dry_run or not todo:
        for aid, text in todo:
            print(f"  {aid}: {text}")
        return 0

    key = os.environ.get("AZURE_SPEECH_KEY")
    region = os.environ.get("AZURE_SPEECH_REGION")
    if not key or not region:
        print("Set AZURE_SPEECH_KEY and AZURE_SPEECH_REGION (see docstring).")
        return 1

    AUDIO.mkdir(parents=True, exist_ok=True)
    for aid, text in todo:
        (AUDIO / (aid + ".mp3")).write_bytes(azure_tts(text, voice, key, region))
        print(f"  {aid}: {text}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
