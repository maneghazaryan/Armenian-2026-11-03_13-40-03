# Այբ — Armenian from zero (for Russian speakers)

A free Unity 6 mobile app (Android + iOS) that teaches reading and first phrases in
Eastern Armenian the way the Armenian 1st-grade primer (Այբբենարան) does: a few
letters a day, and from day 1 you read real words made only of the letters you know
(ա ս ր → սա, ասա, արա, սար). The UI and all explanations are in Russian.

## How it works
- **One lesson per day — or several, the learner chooses.** With «Один урок в день» on (the
  default) the next lesson opens the day after the current one is passed; switched off on the
  home screen, the next lesson opens right away. Either way today's review comes first.
- **The whole alphabet in 17 days:** ս ա ր · մ ի · ն ե · ո տ · ու լ · կ վ է · բ և · պ դ ·
  գ զ · շ ժ · հ յ · խ ղ · թ ք փ · ծ ց · ճ չ ջ · ռ ձ ը · օ ֆ.
- **After the alphabet: very small topics** (`words.json`, 83 topics of 1–3 new words, a few grammar
  topics with none). Learners forget fast, so information comes in tiny portions and comes back a lot.
  The words people use most come first: հա/չէ, then the polite այո/ոչ; Բարև ձեզ; Ո՞նց ես — Լավ եմ;
  Հաջողություն; մենք/դուք; քո; «есть» (եմ, ես, է, then ենք, եք, են — the auxiliary verb); word
  order; questions and «не»; կներեք/ներողություն; մերսի/խնդրեմ; где/куда; health; family; people;
  ջան; the shop (տոպրակ, քարտով); numbers… The colloquial word is taught first and its polite or book
  form beside it, with a note why.
  **Obligatory and optional.** 57 topics are obligatory; 26 (`"optional": true`: colours, weather,
  clothes, school, animals…) say «по желанию» and can be skipped («Пропустить тему») and brought back
  («Вернуть»). Two topics are open at a time and the learner picks one (a started topic stays open).
  With «Один урок в день» no topic opens on the day one was finished.
  **Endings** have their own tiny topics, each right after the words it uses: -ություն
  (հաջողություն, առողջություն…), plural -եր/-ներ, -ը/-ն («этот»), -ս/-դ («мой/твой»), -ում («в»)
  and -ից («из»: Ես Ռուսաստանից եմ). The learner fills in the missing ending (`gapOptions` adds wrong
  choices).
  **Phrases from learned words.** Part 3 of a topic builds each of its short phrases from shuffled words.
  **Repetition.** Lessons only teach; reminding happens when the app opens: once a day «С возвращением!»
  offers a short recall (6 words and one phrase to build), which can be skipped. It takes today's due
  items first, then picks at random, with hard and weakly known words much likelier than well-known ones.
  A mistake brings the word back tomorrow. New words are read, spelled, recalled from their meaning and
  listened to in all three parts of a topic.
  A topic can show words of earlier days again (`review`: «Вы уже знаете это слово»), explain
  something (`explain`: cards in Russian with Armenian examples), drill sentences with a missing word
  (`gaps`: «Մենք լավ [ենք]։») and have sentences built from shuffled words (`order`).
- **Words you don't want.** «Не учить это слово» on a new word's card, or «Убрать» in the dictionary,
  takes a word out of lessons and review; «Убранные слова» in the dictionary bring it back.
- **Spaced review (Leitner boxes).** Every learned letter and word comes back after 1, 2, 4,
  7, 14 and 30 days; a mistake sends it back to "tomorrow". At most 12 items per day.
  «Повторение» is always on the home screen once something is learned: today's due items, or,
  when nothing is due, a free practice of hard and least remembered items (correct answers
  don't move review dates; a mistake marks the item hard and brings it back tomorrow).
- **Reading (Чтение).** 26 interesting facts (Armenia, animals, space, nature and people),
  checked against sources, in Russian with every sound the learner already knows written in
  Armenian letters. The more letters learned, the more Armenian the text shows; «Показать
  по-русски» switches to plain Russian. Texts live in `Assets/Resources/Data/reading.json`.
- **Armenian texts (after the alphabet).** «Чтение» → «Армянские тексты»: 22 texts. Three short
  stories written for the app; Tumanyan's tales almost as he wrote them (Ծիտը, Պոչատ աղվեսը, Անխելք
  մարդը, Կիկոսի մահը, Անբան Հուռին, Սուտասանը, Կացին ախպեր, Քաջ Նազարը, Ձախորդ Փանոսը and the tale in verse Շունն ու
  կատուն): the same sentences, songs and rhymes, only dialect words replaced by standard ones (էս →
  այս, ձեն → ձայն, հեր → հայր…) where the rhythm allows; Aghayan's Անահիտ and Արեգնազան shortened to
  about 3,600 words from his own sentences, dialogues and verses. Poems: Tumanyan's quatrains and
  «Ա՜խ, ես երանի…», the anthem, Charents's «Ես իմ անուշ Հայաստանի», Nalbandyan's «Ազատություն» and
  Teryan's «Էստոնական երգ», and Khnko Aper's fable in verse «Մկների ժողովը» as he wrote it. The best-known and best-loved ones (`"famous": true`) have a gold frame
  and «⭐ Любимая сказка армян». All are in the public domain; sources are on Armenian Wikisource.
  Any word can be tapped: how it reads, what it means, its dictionary form, and «Учить» adds it to
  the daily review. Words the learner doesn't know yet are red. The list is sorted by the number of
  unknown words (words of passed days and words added from texts count as known) and grouped by
  that number: up to 25, up to 60, more.
- **Russian «р».** In mixed texts ր is used only for the soft «р» before и е ё ю я ь («сирена»,
  «пример»). Every hard «р» («рама», «красиво», «размер», «терраса») is ռ and stays Russian until
  ռ is learned. Names written with ր in Armenian («Сахара», unlike «сахар») are listed as exceptions.
- **Other sounds in mixed texts.** я ё ю are written յա յո յու, and «е» after a vowel յե. A soft «т»
  is ծ and a soft «д» is ձ («мать» — Մածь, «Дима» — Ձիմա), with no յ after them («тётя» — ծոծա).
  «ч» is չ, which is exactly the Russian sound.
- **Each day has three parts**, opened one after another:
  1. *Lesson* — new letter cards (Russian sound, Russian letter name, a «Как запомнить»
     association that builds on letters already learned, e.g. ի is ր with the stem up, ա is
     ս plus a stick and a tail; an optional «Подробнее» explanation) → **trace the letter**,
     small then capital, over a faint sample → find the letter →
     match capital ↔ small → **each new word is shown with its meaning first** → read a word
     and pick its meaning → build it from letter tiles.
  2. *Practice* — the same words from other angles: meaning → Armenian word, reading,
     spelling, listening (only when audio exists), capitals, and **each new letter written
     from memory**.
  3. *Reading* — familiar Russian sentences in which the known sounds are written in Armenian
     letters («Սиրենա зաпелա…»), then short Armenian phrases.

  The same kind of exercise, or the same word, never comes twice in a row. A question
  answered wrongly comes back at the end of the part. After the first meeting, the meaning
  and pronunciation of an Armenian word are hidden behind «Подсказка» / a tap. «‹» goes back
  one step; the Android back button and Escape work too.
- **Writing letters.** The pad accepts a finger or the mouse. A drawing is compared with a
  64×64 mask of the letter: most of the ink must lie near the letter and most of the letter
  must be drawn over. Tracing compares in place. From memory the drawing may be anywhere and
  any size; it is fitted onto the letter first, and only near-twins (գ/զ, ծ/ճ) can be mistaken
  for each other. A wrong drawing from memory (or «Не помню») counts as a mistake, then the
  sample appears to trace. Tracing is practice and never counts as a mistake. Letters in
  review are also sometimes written from memory.
- **Armenian vs Russian at a glance:** Armenian text is red; in mixed text Armenian letters
  are red and bold, Russian ones plain.
- **Explanations never mention letters the learner hasn't met yet** — checked by
  `Tools/validate_course.py` and the tests.
- **Alphabet:** learned letters open their full explanation any time; others show the day
  they will be learned.
- **Dictionary (Словарь):** all words, phrases and verses from completed days; read first, tap
  to see the meaning. Words with mistakes are highlighted until two reviews in a row go well
  (the alphabet marks such letters too).
- **Start over:** «Сбросить прогресс» on the home screen (tap twice) forgets all days, reviews,
  read texts, skipped topics and removed words; the settings stay. «Открыть все уроки» (tap twice) is for testers: it marks
  every day as passed.
- **Daily reminders:** local notifications (no server) at 9, 13, 19 or 21 h, or off.
- Progress is saved on the device (`progress.json` in `Application.persistentDataPath`).

## Project layout
| Path | What |
|---|---|
| `Assets/Resources/Data/letters.json` | alphabet: glyphs, name, Russian sound, `look` (shape association), `key` (Russian keyword, sound part in braces: `{а}рка`) and `pic` (its emoji) for the primer-style letter card, tip, warning, `more` (deep explanation) |
| `Assets/Resources/Data/lessons.json` | day-by-day lessons: new letters, words, phrases, verses |
| `Assets/Resources/Data/words.json` | topics after the alphabet (same format, no `newLetters`; optional `review`, `explain`, `gaps`, `order`, `optional`) |
| `Assets/Resources/Data/texts_hy.json` | Armenian stories, poems and songs (`id`, `kind`, `title`, `author`, `note`, `emoji`, `famous`, `text`; empty line = new paragraph or stanza) |
| `Assets/Resources/Data/dictionary.txt` | every word of the Armenian texts: `слово = значение \| форма = значение формы; форма`; lines under `[text-id]` apply only to that text (dialect). Forms that aren't listed are found through common endings (‑ը, ‑ին, ‑ից, ‑ներ, ‑եց, ‑ում, կ‑/չ‑…): «կսպանի» → «սպանել» |
| `Assets/Resources/Audio/` | `l_<letterId>.mp3` and `w_<wordId>.mp3` clips (optional) |
| `Assets/Resources/Fonts/` | Noto Sans, Noto Sans Armenian, Symbols 2, Noto Emoji (SIL OFL) |
| `Assets/Resources/UI/App.uss` | all styles |
| `Assets/Scripts/` | `Data/` course model, `Lesson/` exercise builder and session, `Progress/` unlocking and saving, `UI/` screens |
| `Tools/validate_course.py` | checks that every word uses only letters learned by that day, and that the dictionary covers every Armenian text |
| `Tools/gen_icon.py` | renders the app icon and the web favicons |
| `Tools/gen_glyphs.py` | renders the letter samples and masks for the drawing exercises into `Assets/Resources/Glyphs/` (needs Pillow; rerun after changing the font or `letters.json`) |
| `Tools/gen_audio.py` | generates missing audio clips |

The app builds its UI at startup (`AppRoot`), so the scene needs no setup.

## Screens and platforms

The UI is laid out for 1280 units of height. Phones (portrait) scale by width; landscape
windows (PC, web, tablets) scale by height. The root element gets a `portrait` or `landscape`
class, and the landscape rules at the end of `App.uss` add a centred column, two-column lesson
list and the alphabet with the chosen letter beside the grid. The window can be resized at any
time. Padding from `Screen.safeArea` keeps content clear of notches and the home indicator.
The Android back button and Escape on PC both go back one step. Reminders exist only on phones.

## Web version

Live: https://maneghazaryan.github.io/ayb/ (repository `maneghazaryan/ayb` holds only the build).

Build: menu **Ayb → Build Web**, or with the editor closed

    Unity -batchmode -quit -projectPath . -buildTarget WebGL -executeMethod Aib.Editor.WebBuild.Build

The build goes to `Build/Web`. It uses the full-window template in `Assets/WebGLTemplates/Ayb`
and gzip with decompression fallback, which GitHub Pages needs. Progress is saved in the
browser's IndexedDB (`Assets/Plugins/WebGL/AibStorage.jslib`).

Update the site: copy `Build/Web` into a clone of `maneghazaryan/ayb` (keep `.nojekyll` and
`README.md`), commit and push. Pages republishes within a minute or two.

## Adding a lesson
1. Add an entry to `lessons.json` (next `day`, `newLetters` by letter `id`, `words` with an optional `note`, `phrases`, and Russian `texts` for the reading part — the app writes their known sounds in Armenian letters itself).
2. Run `python3 Tools/validate_course.py`. It must report 0 errors.

## Adding an Armenian text
1. Add it to `texts_hy.json` (`"famous": true` marks a best-loved one). Poems by authors who died
   less than 70 years ago (Sevak, Kaputikyan, Shiraz, Sahyan…) are still under copyright: add
   them only with permission from the rights holders.
2. Run `python3 Tools/validate_course.py`: it lists the words missing from `dictionary.txt`.
   Add them (a new form goes after «|» of its dictionary word), until there are 0 errors.

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
2. ✅ Full alphabet: days 8–17 (39 letters).
3. ✅ Small word topics after the alphabet (choose, skip, remove words), Armenian texts with a tap-to-translate dictionary.
4. Audio, record-and-compare speaking practice; a native speaker's check of the texts' glosses.
5. Optional AI tutor (Google Gemini free tier behind a free Cloudflare Worker) that explains
   grammar in Russian using only the letters you have learned.
