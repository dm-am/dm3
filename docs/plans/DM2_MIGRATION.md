# Миграция со старого сайта

## Имена пользователей

Правила валидации: [USERNAME_POLICY.md](../conventions/USERNAME_POLICY.md)

### Статистика

| Параметр | Значение |
|----------|----------|
| Всего пользователей | 13,027 |
| Соответствуют новым правилам | ~12,953 (99.4%) |
| Требуют переименования | ~74 (0.6%) |

### Группа A: Слишком короткие (4)

| # | Текущее | Результат |
|---|---------|-----------|
| 1 | `0` | `0_` |
| 2 | `f` | `f_` |
| 3 | `V` | `V_` |
| 4 | `き` | `き_` |

### Группа B: Слишком длинные (57)

| Текущее | Длина | Результат |
|---------|-------|-----------|
| `7разОтмерь1разПроверь` | 21 | `7разОтмерь1Проверь` |
| `Alexander Wallerstein` | 21 | `AlexanderWallerstein` |
| `AndyTheMessengerRobot` | 21 | `AndyTheMessengerRobo` |
| `fingolfin_insipinorri` | 21 | `FingolfinInsipinorri` |
| `Gestalt Conscionsness` | 21 | `GestaltConscionsnes` |
| `Gestalt Consciousness` | 21 | `GestaltConsciousnes` |
| `I Wanna See You Bleed` | 21 | `Wanna See You Bleed` |
| `pasta macaroni cheese` | 21 | `PastaMacaroniCheese` |
| `Pope_Of_Crispy_Humans` | 21 | `PopeOfCrispyHumans` |
| `TheManWhoSoldTheWorld` | 21 | `ManWhoSoldTheWorld` |
| `Зеленый обротен Адлет` | 21 | `ЗеленыйОбротенАдлет` |
| `Канализационная Крыса` | 21 | `КанализационнаяКрыса` |
| `Boss Donatello 69 Rape` | 22 | `Boss Donatello 69` |
| `Definition_of_a_BANDIT` | 22 | `BANDIT_Definition` |
| `ItsTooNormalToBeNormal` | 22 | `TooNormalToBeNormal` |
| `Looking_for_Austrinica` | 22 | `LookingForAustrinica` |
| `Nymphetamine Lacrimosa` | 22 | `NymphetamineLacrimos` |
| `Tall Handsome Stranger` | 22 | `TallHandsomeStranger` |
| `Unknown_from_Mandalore` | 22 | `UnknownFromMandalore` |
| `Всадник Вакханаликсиса` | 22 | `ВсадникВакханаликсис` |
| `Раб номер двадцать три` | 22 | `Раб номер 23` |
| `Harmatoe Aphroditida342` | 23 | `Harmatoe Aphroditida` |
| `Антропоморфическое Бюро` | 23 | `Антропоморф Бюро` |
| `Сиреневенький Император` | 23 | `Сиреневый Император` |
| `Albrecht von Ballenstedt` | 24 | `Albrecht von Ballens` |
| `Nuclear Launch Detective` | 24 | `Nuclear L Detective` |
| `бот ярика для злых целей` | 24 | `бот ярика` |
| `Еврейская девочка из ада` | 24 | `Еврейская девочка` |
| `Призрак Экзистенциализма` | 24 | `Призрак Экзистенциал` |
| `baarsik_xxx_nagibator_xxx` | 25 | `baarsik_nagibator` |
| `Trusty Reliable Man Thing` | 25 | `Reliable Man Thing` |
| `Админ Сайта Dungeonmaster` | 25 | `Не Админ Сайта` |
| `Бродяга пидор пошел нахуй` | 25 | `Бродяга пошел` |
| `Красная Карета Царя Химок` | 25 | `Карета Царя Химок` |
| `ТапорищщеВиталийСергеевич` | 25 | `ТапорищщеВиталий` |
| `Arvin aranel ataru an a tu` | 26 | `Arvin aranel ataru` |
| `Барон_Гумберт_фон_Киннеген` | 26 | `Барон_Гумберт` |
| `Nispanonamimatamimanonapsin` | 27 | `Nispanonamimatamiman` |
| `бот ярика для злых замыслов` | 27 | `бот ярика 2` |
| `Бродяга пидор и пошел нахyй` | 27 | `Бродяга пошел 2` |
| `Бродяга пидор и пошел нахуй` | 27 | `Бродяга пошел 3` |
| `ТестовыйЮзерВторогоПоколения` | 28 | `ТестовыйЮзер V2` |
| `JLyKAIIIEHKO_IIInuON_3uM6A6BE` | 29 | `JLyKAIIIEHKO_IIInuON` |
| `бот ярика для злых замыслов 2` | 29 | `бот ярика 3` |
| `Коротко о себе 25 сантиметров` | 29 | `25 сантиметров` |
| `Нас Не Забанят Нас Не забанят` | 29 | `Нас Не Забанят` |
| `I steel want to see your bleed` | 30 | `Wanna See You Bleed2` |
| `не ну вы видели стремность какая` | 32 | `не ну вы видели` |
| `Избранный Изгнать Дракона Из Чата` | 33 | `Изгнать Дракона` |
| `Left My Heart In The Island Of Decay` | 36 | `Left My Heart` |
| `Cheilognatopalatouranostaphyloschisis` | 37 | `Cheilognatopalatour` |
| `Я устала придумывать оригинальные имена` | 39 | `Я устала придумывать` |
| `Хозяин_Темного_Дупла_Владыка_Поленьев_и_Щепок` | 45 | `Хозяин_Темного_Дупла` |
| `МЕЦКАН НИКОЛАЙ ВИТАЛЬЕВИЧ ЭТО БРОДЯГА ПО ЖИЗНИ` | 46 | `ЭТО БРОДЯГА ПО ЖИЗНИ` |
| `Пирожки домашние вкусные с начинкой любимой твоей` | 49 | `Пирожки домашние` |
| `ПОЧЕМУ МЕНЯ ДВА РАЗА ЗАБАНИЛИ ВОПРОСИТЕЛЬНЫЙ ЗНАК` | 49 | `ПОЧЕМУ МЕНЯ` |
| `Boss Mojo 69 rape Master_kryrptonite Donatello Xavier` | 53 | `Boss Mojo 69` |

### Группа C: Запрещённые символы (17)

| # | Текущее | Проблема | Результат |
|---|---------|----------|-----------|
| 1 | `K@rTes` | `@` | `KarTes` |
| 2 | `MoS@Rt` | `@` | `MoSaRt` |
| 3 | `Г@е4К@` | `@` | `Гае4Ка` |
| 4 | `Р@доэль` | `@` | `Радоэль` |
| 5 | `[A.i. m.o.z.g.]` | `[]` | `A.i. m.o.z.g.` |
| 6 | `[dark]fLamer` | `[]` | `dark_fLamer` |
| 7 | `Death_Knight[S]` | `[]` | `Death_Knight_S` |
| 8 | `Ve[by` | `[` | `Veby` |
| 9 | `tuсhibo (ярик)` | `()` | `tuсhibo ярик` |
| 10 | `-=BIG_MeK=-` | `=` | `BIG_MeK_2` |
| 11 | `=Dark_Master_Nik=` | `=` | `Dark_Master_Nik` |
| 12 | `~Pchel~` | `~` | `Pchel` |
| 13 | `~Samurai~` | `~` | `Samurai` |
| 14 | `~Strike~` | `~` | `Strike` |
| 15 | `NuKу_MaN'а` | `'` | `NuKу_MaNа` |
| 16 | `Ayron zi`Gamerton` | `` ` `` | `Ayron Gamerton` |
| 17 | `Tia`ra` | `` ` `` | `Tiara` |

### Группа D: Проблемы с пробелами (3)

| # | Текущее | Проблема | Результат |
|---|---------|----------|-----------|
| 1 | ` Tazik` | Пробел в начале | `Tazik` |
| 2 | ` Tazikkk` | Пробел в начале | `Tazikkk` |
| 3 | `Kaelin  Morra` | Двойной пробел | `Kaelin Morra` |

---

## Уведомление пользователей

Письма не отправляются пользователям в перманентном бане.

### Шаблон письма

```
Тема: Ваше имя пользователя было изменено

Здравствуйте!

В связи с обновлением системы, ваше имя пользователя было изменено:

Было:  [старое_имя]
Стало: [новое_имя]

Причина: [причина]

Вы можете однократно выбрать новое имя по ссылке:
[ссылка_на_смену_ника]

Ссылка действительна 30 дней.

С уважением,
Команда DM
```

---

## Checklist

- [ ] Переименовать пользователей в БД
- [ ] Отправить уведомления

