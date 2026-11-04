# Այբ — Armenian from zero (for Russian speakers)

A free Unity 6 mobile app (Android + iOS) that teaches reading and first phrases in
Eastern Armenian the way the Armenian 1st-grade primer (Այբբենարան) does: a few
letters a day, and from day 1 you read real words made only of the letters you know
(ա ս ր → սա, ասա, արա, սար). The UI and all explanations are in Russian.

## How it works
- **One lesson per day, review first.** The next lesson opens the day after the current one
  is passed, and only after today's review is done. (Debug builds show a switch to lift the
  day limit for testing; release builds don't.)
- **Spaced review (Leitner boxes).** Every learned letter and word comes back after 1, 3, 7,
  14 and 30 days; a mistake sends it back to "tomorrow". At most 12 items per day.
- **Each lesson:** new letter cards (Russian sound, tips, a «Как запомнить» shape association
  for every letter, small and capital — e.g. ր looks like a handwritten Russian «р» — and an
  optional «Подробнее» deep explanation with English comparisons, e.g. թ = տ + հ = English
  "t" in "top") → find the letter → match capital ↔ small letters (new ones plus two older
  ones) → **each new word is shown with its meaning first** → read a word (sometimes
  capitalised) and pick its meaning → build it from letter tiles → listen and choose (only
  when audio exists) → read short phrases aloud. A question answered wrongly comes back
  again at the end of the lesson. «‹» goes back one step; the Android back button works too.
- **Alphabet:** learned letters open their full explanation any time; others show the day
  they will be learned.
- **Dictionary (Словарь):** all words, phrases and verses from completed days.
- **Daily reminders:** local notifications (no server) at 9, 13, 19 or 21 h, or off.
- Progress is saved on the device (`progress.json` in `Application.persistentDataPath`).

## Project layout
| Path | What |
|---|---|
| `Assets/Resources/Data/letters.json` | alphabet: glyphs, name, Russian sound, `look` (shape association), tip, warning, `more` (deep explanation) |
| `Assets/Resources/Data/lessons.json` | day-by-day lessons: new letters, words, phrases, verses |
| `Assets/Resources/Audio/` | `l_<letterId>.mp3` and `w_<wordId>.mp3` clips (optional) |
| `Assets/Resources/Fonts/` | Noto Sans, Noto Sans Armenian, Symbols 2, Noto Emoji (SIL OFL) |
| `Assets/Resources/UI/App.uss` | all styles |
| `Assets/Scripts/` | `Data/` course model, `Lesson/` exercise builder and session, `Progress/` unlocking and saving, `UI/` screens |
| `Tools/validate_course.py` | checks that every word uses only letters learned by that day |
| `Tools/gen_audio.py` | generates missing audio clips |

The app builds its UI at startup (`AppRoot`), so the scene needs no setup.

## Screens and platforms

The UI is laid out for 1280 units of height. Phones (portrait) scale by width; landscape
windows (PC, web, tablets) scale by height. The root element gets a `portrait` or `landscape`
class, and the landscape rules at the end of `App.uss` add a centred column, two-column lesson
list and the alphabet with the chosen letter beside the grid. The window can be resized at any
time. Padding from `Screen.safeArea` keeps content clear of notches and the home indicator.
The Android back button and Escape on PC both go back one step. Reminders exist only on phones.

## Adding a lesson
1. Add an entry to `lessons.json` (next `day`, `newLetters` by letter `id`, `words`, `phrases`).
2. Run `python3 Tools/validate_course.py`. It must report 0 errors.

## Audio
Without audio the app still works; listening exercises are just skipped.
Two free ways to add it (files with the same name are never overwritten):
- **Record it yourself** (best for authentic pronunciation): save `l_a.mp3`, `w_asa.mp3`, …
  into `Assets/Resources/Audio/`. `python3 Tools/gen_audio.py --dry-run` lists what is missing.
- **Azure Speech free tier** (500k characters/month free, real Armenian voices
  `hy-AM-AnahitNeural` / `hy-AM-HaykNeural`): create a free Speech resource, then
  `AZURE_SPEECH_KEY=… AZURE_SPEECH_REGION=… python3 Tools/gen_audio.py`.
  The key is only used on your computer; it is not shipped in the app.

## Tests
Unity Test Runner → EditMode (course data, exercise builder, lesson session, day unlocking), or:
```
Unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults results.xml
```

## Roadmap
1. ✅ Reading MVP: days 1–7 (16 letters), spaced review, dictionary, reminders.
2. Full alphabet (~20 lessons), verses, record-and-compare speaking practice.
3. Optional AI tutor (Google Gemini free tier behind a free Cloudflare Worker) that explains
   grammar in Russian using only the letters you have learned.
