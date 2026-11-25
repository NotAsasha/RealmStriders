# Steam Audio

## Поточна інтеграція

Проєкт використовує Steam Audio з Unity audio та CPU ray tracer. У Project
Settings → Audio має бути збережено **Spatializer Plugin = Steam Audio Spatializer**.
Прямий звук використовує HRTF, Occlusion і частотно-залежну Transmission;
Pathing додає напрямок доступного проходу. Reflections, Steam Audio reverb та
Air Absorption у поточній інтеграції вимкнені.

Casino, Lobby, Library і Field уже мають `SceneAcoustics` із призначеним Probe
Batch. Налаштовуй кожне нове джерело окремо.

`SceneAcoustics` реєструє призначений в Inspector batch за scene handle. Для постійних
гравців об’єм визначається за позицією.

Pathing потребує покриття джерела й слухача та достатньої дальності. Поза
зареєстрованим об'ємом залишається прямий просторовий звук. Серверні правила
AI hearing, `SoundProducer`, capture та voice transport від акустики не залежать.
Мережевий контракт описаний у [Networking](NETWORKING.md#voice-footsteps-and-gameplay-noise).

## Джерела гравця

У `Assets/Player/Player.prefab` компонент **Player Audio** зберігає Footstep
Clips, Footstep Source і чотири явні пари Audio Source / Acoustic Source.
Кожна пара має окремий GameObject.

| Джерело | Де знайти | Min / Max Distance | Volume |
| --- | --- | --- | --- |
| Кроки | Audio → Footsteps | 1 / 30 м, Linear | 0.5 |
| Голос | mixamorig:Head → VoiceAudio | 2 / 18 м, Linear | 0.5 |
| Смерть | mixamorig:Head → DeathAudio | 1 / 30 м, Linear | 0.5 |
| Струм | mixamorig:Head → ShockAudio | 1 / 10 м, Custom | 1 |

Джерела на кістці голови рухаються з анімацією, присіданням і ragdoll.
Volume, дистанцію та Rolloff змінюй на відповідному AudioSource у prefab поза
Play Mode. VoiceAudio: Loop увімкнено, Clip порожній, Play On Awake вимкнено;
`VoiceChat` створює та звільняє власний mono streaming clip. Кроки змінюють pitch
лише свого джерела.

Власні кроки, смерть і струм — 2D feedback; SteamAudioSource власника вимикається
на spawn. Власний голос не прослуховується. Для інших гравців діє просторова
обробка. Pathing Probe Batch у prefab залиш порожнім: його призначає PlayerAudio.

Темп кроків задає PlayerMovement. Якщо змінюєш темп, тримай Minimum Network Step
Interval у PlayerAudio нижчим за найкоротший інтервал між кроками.

## Вороги та нові джерела

Розділення кроків, голосу й музики описано в [звуках ворогів](ENEMIES.md#sounds).

Для нового звуку ворога:

1. Створи окремий GameObject з одним AudioSource та SteamAudioSource.
2. Признач джерело явно у Enemy / Audio Source або EnemySounds / Vocal Audio Source.
3. Застосуй [базові налаштування](#налаштування-звучання).
4. Додай пару у EnemyAcoustics / Emitters; batch у prefab залиш порожнім.
5. Перевір spawn/despawn, іншу сцену та вихід із покриття з host і client.

EnemySounds керує дальністю голосів залежно від idle/chase/death/jumpscare;
дальність кроків задає Enemy / Step Sound Radius. Не дублюй ці значення вручну
на AudioSource. Для спорядження використовуй
[ItemAcoustics](#предмети-та-переносні-ефекти).

## Lobby

Вентилятори під `/Base/Decorations/Server*` використовують окремі дочірні
**FanAudio**. Признач AudioSource, SteamAudioSource та Lobby SceneAcoustics у
SpatialLoopEmitter.

- AudioSource: Loop, Volume 1, Min Distance 1 м, Max Distance 7 м, Linear,
  Doppler 0, Spatialize увімкнено, Play On Awake вимкнено.
- Задай різні Start Offset Seconds, щоб петлі не стартували синфазно.
- Компонент завантажує кліп і запускає його на enable, незалежно від першого
  наближення слухача. Нечутні петлі продовжують відтворення, зберігаючи фазу.
- Без owner AudioListener або за Max Distance джерело заглушене. Усередині
  радіуса працює Linear fade; Pathing додатково перевіряє покриття обох кінців.
  Disable зупиняє петлю та очищає batch.

Дальність і гучність змінюй на FanAudio / AudioSource, фазу — у Spatial Loop
Emitter. Mute та Pathing у Play Mode контролює компонент.

## Налаштування звучання

| Параметр | Базове значення | Вплив |
| --- | --- | --- |
| HRTF / Occlusion / Transmission | Увімкнено, Bilinear HRTF | Просторова обробка та ослаблення крізь перешкоди |
| Pathing Mix Level | 0.8 | Внесок звуку через коридор |
| Direct Mix Level | 1 | Прямий канал, включно з приглушенням крізь стіну |
| Distance Attenuation | Curve Driven | Крива та дальність AudioSource |
| Path Validation / Find Alternate Paths | Увімкнено | Перевірка шляху при рухомих перешкодах |
| Apply HRTF To Pathing | Увімкнено | Напрямок маршрутного звуку в навушниках |
| Normalize Pathing EQ | Вимкнено | Поточне налаштування обробки маршруту |

Volume, дистанція та mix не потребують нового Bake. Великий Path Range не
робить джерело чутним поза його кривою згасання.

## Геометрія та повторне запікання

В акустичний експорт включай лише перевірені стіни, підлогу, основну конструкцію
та рухомі частини воріт. Меблі й декор не включай. На позначених мешах залиш
**Export All Children** вимкненим.

На **Steam Audio Casino Pathing** використовуй
`Assets/Scenes/Casino/Audio/CasinoSpeakerPathing.asset` для Speaker, ворогів і
гравців. Генеруй probes із кроком 3 м і Height Above Floor 1.8 м та додатковим
покриттям сходів.
Після зміни об'єму перевіряй також високі джерела та рівень ніг/голови.

1. Зупини Play Mode та відкрий лише сцену, яку готуєш.
2. Після зміни статичної геометрії або акустичних матеріалів виконай
   **Steam Audio → Export Active Scene**.
3. Вибери Probe Batch, перевір об'єм/крок/висоту та натисни **Generate Probes**.
   Це скидає попередній Bake; переміщення рамки не пересуває вже створені probes.
4. Залиш batch вибраним і виконай **Tools → Audio → Fill Selected Probe Gaps**.
   Команда потребує актуального NavMesh та єдиної відкритої сцени. Її код:
   `Assets/Scripts/Editor/ProbeCoverageAuthoring.cs`.
5. Натисни **Bake**, дочекайся завершення і збережи сцену та активи.

Після зміни лише baking visibility settings потрібен Bake, повторна генерація
не обов'язкова. Після зміни мешів/матеріалів воріт повторно експортуй їхні
Dynamic Objects; звичайне відкривання під час гри нового Bake не потребує.

| Спільні Steam Audio Settings | Значення |
| --- | --- |
| Baking Visibility Samples | 4 |
| Baking Visibility Radius | 0.35 м |
| Baking Visibility Threshold | 0.5 |
| Baking Visibility Range | 12 м |
| Baking Path Range | 120 м |
| Baking CPU cores | 50% |

## Локальні зміни плагіна

У `Assets/Plugins/SteamAudio/Scripts/Runtime/SteamAudioSource.cs` збережи дві
локальні правки під час оновлення плагіна:

- `Awake` створює Curve Driven attenuation callback, навіть якщо Pathing і
  Reflections спочатку вимкнені: компоненти можуть увімкнути Pathing пізніше.
- `SetInputs` оновлює кеш attenuation callback і позначає його dirty після
  зміни дальності/Rolloff, щоб маршрутний звук не використовував старий радіус.

## Предмети та переносні ефекти

На кожному AudioSource предмета використовується окремий SteamAudioSource та
`ItemAcoustics`. У ньому явно призначені Audio Source і Acoustic Source.
Компонент вибирає зареєстрований SceneAcoustics за світовою позицією, тому
предмет може переходити між Lobby та місією, зберігаючи свою мережеву сцену.
Налаштування виконується локально.

Гучність, кліп та Min/Max Distance налаштовуй на AudioSource. Для предметів
Spatial Blend = 1, Spatialize і Spatialize Post Effects увімкнені, Doppler = 0.
Для згасання до нуля на Max Distance використовуй Linear або відповідну криву
Custom Rolloff. Скрипти предметів керують мережевими подіями, відтворенням,
циклами й inventory mute.

Телепортери використовують `ItemAcoustics.PlayAt`: незалежне джерело залишається
в точці відправлення, бере гучність/дальність/мікшер із призначеного
teleportAudio, проходить через Steam Audio і видаляється після кліпу. Обидва
посилання teleportSound і teleportAudio мають бути призначені.

Для нового предмета додай ці компоненти на той самий GameObject, признач
посилання ItemAcoustics та додай AudioSource до Item → Inventory Audio Sources,
якщо звук повинен приглушуватись у схованому слоті. Один GameObject має містити
лише один AudioSource для однозначного зв'язку зі SteamAudioSource. Застосуй
[базові налаштування](#налаштування-звучання).
Початковий Pathing вимкнений, Probe Batch не призначений: їх задає ItemAcoustics.

Перевір перенесення між сценами, схований/активний слот, викидання предмета,
вихід із покриття і видалення середовища. Для телепортера окремо перевір звук
у точці відправлення з позиції іншого гравця.

## Межі інтеграції

У Tutorial немає SceneAcoustics і запечених probes. Просторові джерела там
використовують прямий звук. Для решти сцен перевіряй і prefab, і його екземпляр,
а також джерела, створені через AddComponent або PlayClipAtPoint. Якщо один
GameObject містить кілька AudioSource, спочатку розділи їх і перенеси посилання
та фільтри.

UIAudio, меню, касетний перехід Tutorial, особистий StartAnimation і глобальний
ambient мають залишатися локальними 2D-ефектами. Чутність MissionWarningAudio,
тривог бази й переходу порталом потрібно визначати за їхнім призначенням перед
додаванням оклюзії. Не застосовуй налаштування фізичного джерела до всіх них
одним масовим проходом.

## Перевірка

З host і другим клієнтом послухай кроки, голос і ворога поруч, за стіною та
за поворотом. Власний крок має звучати один раз; голос не повинен перериватися
чи змінювати pitch через кроки. Перевір перехід Lobby ↔ mission, вихід із
покриття та despawn. Для вентиляторів перевір перше наближення, близький рух
і вихід за радіус; відрізняй їх від LoadingAudio та інших пристроїв.

Для порівняння тимчасово заглуши Speaker або зміни Pathing Mix з 0.8 на 0,
після чого віднови значення. Якщо за поворотом тиша, перевір дальність,
покриття джерела/слухача та наявність запеченого Pathing. Loopback і Editor
перевірки не замінюють реальний Steam microphone capture, standalone build,
прослуховування та профілювання на цільовому пристрої.
