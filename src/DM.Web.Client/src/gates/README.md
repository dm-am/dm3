# Гейты клиента

Проверки, которые читают дерево клиента целиком, а не одну единицу: конвенции всего дерева и сквозные круги вроде правки автора. Спек одной единицы лежит рядом с ней ([PATTERNS.md](../../../../docs/conventions/PATTERNS.md)), сюда приходит только правило для всех.

Отдельно гейты идут командой `npm run test:conventions` из `src/DM.Web.Client`: на пуше ветки ее зовут хук и `scripts/gates.sh`, а CI гоняет их в составе `npm run test:coverage` ([TESTING.md](../../../../docs/guides/TESTING.md)). Пути к исходникам гейт считает от своей папки, поэтому исходники клиента для него на уровень выше.

Новый гейт приходит со строкой в этот реестр, удаленный уносит свою. Реестр с папкой в обе стороны сверяет [gateRegister.spec.ts](gateRegister.spec.ts). В колонке "что ловит" то, что должно упасть, когда правило нарушено, подробности в шапке файла.

## Архитектура и дерево

| Гейт | Что ловит |
|------|-----------|
| [fsdBoundaries.spec.ts](fsdBoundaries.spec.ts) | правила слоев из PATTERNS.md держит не линтер, конфиг линтера разошелся со списком слоев или типы общих компонентов для vue-tsc разошлись с их регистрацией |
| [namedFiles.spec.ts](namedFiles.spec.ts) | комментарий называет файл, которого нет |
| [sharedHelperBypass.spec.ts](sharedHelperBypass.spec.ts) | общий помощник написан заново вместо вызова |
| [gateRegister.spec.ts](gateRegister.spec.ts) | файл этой папки без строки в реестре или строка реестра без файла |
| [scriptCallers.spec.ts](scriptCallers.spec.ts) | файл в `scripts/` или `docker/scripts/`, которого никто не зовет и не называет: ни скрипт, ни сборка, ни CI, ни документ. Перечень [scripts/README.md](../../../../scripts/README.md) не в счет |
| [scriptsReadme.spec.ts](scriptsReadme.spec.ts) | файл в `scripts/`, `docker/scripts/` или оболочечный файл в `docker/` без строки в перечне [scripts/README.md](../../../../scripts/README.md), или строка перечня без файла |

## Размер и повторы

| Гейт | Что ловит |
|------|-----------|
| [duplicationRatchet.spec.ts](duplicationRatchet.spec.ts) | число повторяющихся строк в одной из групп (клиент, спеки клиента, e2e, сервер, тесты сервера) выше потолка |
| [sassSizeRatchet.spec.ts](sassSizeRatchet.spec.ts) | таблица стилей длиннее 400 строк |
| [sizeRatchet.spec.ts](sizeRatchet.spec.ts) | реестр размеров sizeRatchet.js разошелся с деревом, или линтер с реестром |
| [sizeRatchet.js](sizeRatchet.js) | не гейт, а его реестр: лимиты размера файла, блока SFC и функции клиента и то, что было больше в день введения. Читают eslint.config.js и sizeRatchet.spec.ts |

## Сборка и доставка

| Гейт | Что ловит |
|------|-----------|
| [buildChunks.spec.ts](buildChunks.spec.ts) | в начальный чанк попадает стор зоны игры или блога: его импортирует модуль входа или он делит файл со стором списка |
| [csp.spec.ts](csp.spec.ts) | script-src допускает inline-скрипт, или хеш единственного inline-скрипта разошелся с файлом |
| [documentHead.spec.ts](documentHead.spec.ts) | статичная голова документа (название, описание, превью ссылки) не задает лицо сайта для тех, кто не запускает бандл |
| [fontDelivery.spec.ts](fontDelivery.spec.ts) | то, от чего зависит ожидание шрифта до первой отрисовки (@font-face, голова документа, два прыжка nginx), вышло за бандл неправильно |

## API и данные

| Гейт | Что ловит |
|------|-----------|
| [apiEnvelopeContract.spec.ts](apiEnvelopeContract.spec.ts) | тип ответа метода API-клиента (Envelope, ListEnvelope, CursorEnvelope) не совпадает с тем, что шлет сервер |
| [authorEditSource.spec.ts](authorEditSource.spec.ts) | исходник автора на правку прошел через htmlToBbcode вне редактора, и разметка автора меняется при сохранении |
| [catalogFreshness.spec.ts](catalogFreshness.spec.ts) | каталог после правки читается из кеша, а не с сервера |
| [mutationOutcome.spec.ts](mutationOutcome.spec.ts) | вызов Api (он не бросает исключений) без проверки результата принят за выполненное действие |
| [pagingVocabulary.spec.ts](pagingVocabulary.spec.ts) | запрос листает именем, которое API снял (number, size, page, offset), вместо skip и take |
| [realtimeBroadcastMirror.spec.ts](realtimeBroadcastMirror.spec.ts) | события глобального чата идут не всем соединениям (гостям тоже) или сохраняют уведомления |
| [refusalCopy.spec.ts](refusalCopy.spec.ts) | клиент сочиняет формулировку отказа, которая принадлежит серверу |
| [refusalFlagSeams.spec.ts](refusalFlagSeams.spec.ts) | окно, печатающее отказ у себя, не шлет ownsRefusal и ownsRateLimit, и перехватчик дублирует плашку |
| [ticketBody.spec.ts](ticketBody.spec.ts) | экран, читающий обращение, показывает тело и переписку как набрано, а не как отрисовал сервер |

## Поведение форм

| Гейт | Что ловит |
|------|-----------|
| [composerDrafts.spec.ts](composerDrafts.spec.ts) | clear() редактора вызван раньше, чем пришел ответ на запрос, которому он принадлежит |
| [draftKeys.spec.ts](draftKeys.spec.ts) | ключ черновика написан литералом, а не построен помощником draftKey.ts |
| [navigationTargets.spec.ts](navigationTargets.spec.ts) | именованное место, куда страница уходит после действия, не разрешается по настоящей таблице маршрутов |
| [removedAuthorSignature.spec.ts](removedAuthorSignature.spec.ts) | удаленный аккаунт подписан не как "Аккаунт удален", или подпись не ведет в его профиль |
| [supportLinks.spec.ts](supportLinks.spec.ts) | вход в форму поддержки не называет причину, или форма не открывается на ней |
| [unsavedChangesGuard.spec.ts](unsavedChangesGuard.spec.ts) | форма с данными, которые жалко потерять, не подключает охранника ухода со страницы |

## Текст и копирайт

| Гейт | Что ловит |
|------|-----------|
| [codeStyle.spec.ts](codeStyle.spec.ts) | многоточие в тексте интерфейса написано не тремя точками |
| [commentLanguage.spec.ts](commentLanguage.spec.ts) | код и комментарии не на английском (исключение только операторские файлы в docker) |
| [confirmCopy.spec.ts](confirmCopy.spec.ts) | подтверждения на сайте сформулированы не одним образом |
| [countedNouns.spec.ts](countedNouns.spec.ts) | число рядом с существительным не согласовано (1 игра, 2 игры, 5 игр) |
| [onelineCopy.spec.ts](onelineCopy.spec.ts) | составное, читающееся одной строкой, копируется в несколько |
| [sentencePeriods.spec.ts](sentencePeriods.spec.ts) | точка в конце одного предложения в пункте списка или во всплывающем |

## Доступность

| Гейт | Что ловит |
|------|-----------|
| [accessibleNames.spec.ts](accessibleNames.spec.ts) | кнопка, ссылка или router-link в шаблоне без доступного имени (aria-label, текст, alt) |
| [fillContrast.spec.ts](fillContrast.spec.ts) | подпись на акцентной заливке нечитаема |
| [focusRing.spec.ts](focusRing.spec.ts) | контрол с фокусом клавиатуры его не показывает или показывает иначе, чем остальные |
| [linkDestinations.spec.ts](linkDestinations.spec.ts) | якорь в шаблоне без назначения |
| [pageControls.spec.ts](pageControls.spec.ts) | цель нажатия меньше нормы (WCAG 2.5.8, 24x24) или контрол страницы вне владельческого стандарта |
| [pseudoContent.spec.ts](pseudoContent.spec.ts) | смысловой символ живет в ::before или ::after и не попадает в буфер |
| [textSelection.spec.ts](textSelection.spec.ts) | текст интерфейса невыделяем (user-select: none допустим только у DashSeparator) |

## Вид контролов

| Гейт | Что ловит |
|------|-----------|
| [busyControls.spec.ts](busyControls.spec.ts) | ожидающий контрол показывает это словом в подписи, а не в одном общем месте |
| [datePickers.spec.ts](datePickers.spec.ts) | в клиенте появились родные поля даты, времени, месяца или недели браузера |
| [disabledHatch.spec.ts](disabledHatch.spec.ts) | недоступный контрол выглядит иначе, чем везде: полупрозрачность, указатель текста, косая штриховка |
| [disclosureMarker.spec.ts](disclosureMarker.spec.ts) | треугольник перед раскрывающейся строкой сделан символом, а не рисунком |
| [roundedCorners.spec.ts](roundedCorners.spec.ts) | карточка скруглена, или контрол скруглен не единым радиусом сайта, или радиус вне реестра |

## Окна и плашки

| Гейт | Что ловит |
|------|-----------|
| [dialogButtons.spec.ts](dialogButtons.spec.ts) | ряд кнопок окна не две равные половины по 52, с серой подложкой или с красным не у отказа |
| [dialogFields.spec.ts](dialogFields.spec.ts) | поле формы в окне не высотой 52, без жирной подписи 15, без нейтральной рамки при отказе или со строкой под полем, занимающей место |
| [dialogSurface.spec.ts](dialogSurface.spec.ts) | общая поверхность окна (карточка, крестик, заголовок, поля, кнопки) у окна с формой или у подтверждения идет мимо _DialogSurface.sass |
| [dialogTier.spec.ts](dialogTier.spec.ts) | модальное окно ведет себя не через useDialogShell (фокус внутрь, Tab не уходит, Escape закрывает) |
| [formButtonPair.spec.ts](formButtonPair.spec.ts) | две кнопки в конце формы (главная и Отмена) читаются парой, где неясно, какая отправляет |
| [toastRegistry.spec.ts](toastRegistry.spec.ts) | плашка сайта не записана в реестр с причиной, почему страница не могла сказать это на месте нажатия |
| [toastWidth.spec.ts](toastWidth.spec.ts) | тост не в одну строку, или коробка шире строки |

## Единые компоненты

| Гейт | Что ловит |
|------|-----------|
| [chatParity.spec.ts](chatParity.spec.ts) | глобальный чат и личная переписка рисуются не одной лентой widgets/chat-feed, а копиями ее частей |
| [discussionUnification.spec.ts](discussionUnification.spec.ts) | обсуждение у топика, игры, блога или публикации живет своей копией раздела |
| [infoTable.spec.ts](infoTable.spec.ts) | таблица фактов под заголовком сущности нарисована заново на странице, а не общим дизайном |
| [messageToolbarStyles.spec.ts](messageToolbarStyles.spec.ts) | панель действий сообщения нарисована не из одного файла |
| [profileTiles.spec.ts](profileTiles.spec.ts) | плитки наград и достижений нарисованы не в четырех металлах и нейтральном тексте |
| [sharedLike.spec.ts](sharedLike.spec.ts) | лайк нарисован не shared/ui/LikeBadge, красным или с меняющимся сердцем |

## Вид страниц

| Гейт | Что ловит |
|------|-----------|
| [emptyStateAlignment.spec.ts](emptyStateAlignment.spec.ts) | пустое состояние, отказ или приглашение войти читается не от левого края |
| [gameSlotsCounter.spec.ts](gameSlotsCounter.spec.ts) | счетчик игроков [0/∞] переносится внутри скобок |
| [markupLook.spec.ts](markupLook.spec.ts) | личный текст, [code] и картинка выглядят не как на DM2 там, где страница рисует разметку |
| [pictures.spec.ts](pictures.spec.ts) | картинка, ее рамка или заглушка скруглены (круглыми остаются только логотипы партнеров) |
| [styleSystem.spec.ts](styleSystem.spec.ts) | компонент переопределяет значение общего слоя стилей числом вместо токена (z-index и родня) или раскладывает страницу по ширине окна или виду указателя |
| [textCut.spec.ts](textCut.spec.ts) | текст обрезан многоточием или ограничен числом строк |

## Гостевой вид

| Гейт | Что ловит |
|------|-----------|
| [guestFields.spec.ts](guestFields.spec.ts) | поле гостевой карточки разошлось с прототипом (полоса под полем, код второго фактора, глаз пароля, строка проверки) |
| [guestSurface.spec.ts](guestSurface.spec.ts) | гостевая поверхность разошлась с прототипом, с которого нарисована |
| [guestTitleFits.spec.ts](guestTitleFits.spec.ts) | заголовок гостевой поверхности переносится на вторую строку |

## e2e

| Гейт | Что ловит |
|------|-----------|
| [e2eDiscipline.spec.ts](e2eDiscipline.spec.ts) | прогон корпуса Playwright зеленый потому, что тест не шел (test.skip по условию и родня) |
| [e2eSelectors.spec.ts](e2eSelectors.spec.ts) | селектор e2e называет то, чего клиент не рисует |
