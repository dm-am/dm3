<script setup lang="ts">
/**
 * ChatEventsVariantsPage — DEV-ONLY mockup catalog for the pending decision on
 * the global-chat events strip (the owner picks by number, then the page and
 * its helpers are removed):
 *   1. One line in flow — what the strip says and how it is written.
 *   2. One line plus a disclosure — where the description opens.
 *   3. Rebuilt frame heading — two rows, or a chip.
 *   4. The event inside the feed instead of above it.
 *   5. The event out of the chat frame entirely.
 *   6. Where an action would sit, if events ever get one.
 *
 * Nothing in the production strip is touched: every variant is drawn by this
 * page's own markup, so the site keeps its current strip until a number is
 * picked. Fixtures are local, there is not a single request.
 *
 * The four switches at the top feed every variant at once, which is the whole
 * point: one click shows all of them in the same state, including the ones
 * that move the feed. The width switch narrows the frame instead of the
 * window, so a narrow strip is read next to a wide one, and the ruler is a
 * fixed line the first message crosses when a variant costs height.
 *
 * The disclosures are registered in the site's expandable registry, so
 * "Развернуть все" opens them together — that is a property of the disclosure
 * variants and it should be visible here rather than promised.
 */
import { computed, ref } from "vue";
import { SegmentedControl } from "@/shared/ui/SegmentedControl";
import { SvgIcon } from "@/shared/ui/Icon";
import { DashSeparator } from "@/shared/ui/DashSeparator";
import { useExpandable } from "@/shared/lib/composables/useExpandable";
import ChatFrameReplica from "./ChatFrameReplica.vue";
import ChatEventLine from "./ChatEventLine.vue";
import ChatEventRun from "./ChatEventRun.vue";
import ChatNavRun from "./ChatNavRun.vue";
import ChatEventCard from "./ChatEventCard.vue";
import {
  actionFor,
  actionNoteFor,
  RULER_OPTIONS,
  SCENARIO_OPTIONS,
  SCENARIOS,
  STATE_WORD,
  VIEWER_OPTIONS,
  WIDTH_OPTIONS,
  type FrameWidth,
  type RulerMode,
  type ScenarioId,
  type ViewerId,
} from "./chatEventsMock";

const scenario = ref<ScenarioId>("live-plus-two");
const width = ref<FrameWidth>("full");
const viewer = ref<ViewerId>("user");
const rulerMode = ref<RulerMode>("off");

const ruler = computed(() => rulerMode.value === "on");

const events = computed(() => SCENARIOS[scenario.value]);
const live = computed(
  () => events.value.find((e) => e.status === "Live") ?? null,
);
const ended = computed(
  () => events.value.find((e) => e.status === "Ended") ?? null,
);
const upcoming = computed(() =>
  events.value.filter((e) => e.status === "Scheduled"),
);

/**
 * The focal event, derived and not stored: whatever is running wins, then the
 * one that just ended, then the nearest upcoming. Nothing to desync when an
 * event starts or ends.
 */
const focal = computed(
  () => live.value ?? ended.value ?? upcoming.value[0] ?? null,
);

/** Everything not on the line: all upcoming, minus the focal one if it is one. */
const rest = computed(() =>
  focal.value?.status === "Scheduled"
    ? upcoming.value.slice(1)
    : upcoming.value,
);
const restCount = computed(() => rest.value.length);

/** The "ближайший" tail only makes sense while the focal event is not it. */
const nearestShort = computed(() =>
  focal.value?.status === "Scheduled"
    ? ""
    : (upcoming.value[0]?.startsShort ?? ""),
);

const isLive = computed(() => focal.value?.status === "Live");

/** The in-feed mark for the variant that writes the event into the log. */
const feedMarkLabel = computed(() => {
  const ev = focal.value;
  if (!ev || ev.status === "Scheduled") return "";
  const word = ev.status === "Live" ? "Начался" : "Закончился";
  return `${word} эвент: ${ev.title}`;
});

/** Feed rows sent during the event, for the variant that marks them. */
const markFrom = computed(() => (isLive.value ? 2 : -1));

const action = computed(() => actionFor(viewer.value, focal.value));
const actionNote = computed(() => actionNoteFor(viewer.value, focal.value));
const actionButton = computed(() => {
  const label = action.value;
  return label ? label[0].toUpperCase() + label.slice(1) : "";
});

/**
 * One registry entry for the whole catalog: the site's "Развернуть все" then
 * opens every mockup disclosure at once, which is exactly what it would do to
 * the real strip.
 */
const REVEAL_IDS = ["v11", "v21", "v22", "v23", "v33"] as const;
const reveal = useExpandable({ multiple: true, ids: REVEAL_IDS });

/** Local state of the view switcher variant, so it can actually be clicked. */
const tabView = ref<"chat" | "events">("chat");
</script>

<template>
  <page-title>Мокапы: эвенты чата</page-title>

  <div class="switches">
    <div class="switch">
      <span class="switch-label">Сценарий</span>
      <SegmentedControl
        v-model="scenario"
        :options="SCENARIO_OPTIONS"
        ariaLabel="Сценарий эвентов"
      />
    </div>
    <div class="switch">
      <span class="switch-label">Ширина кадра</span>
      <SegmentedControl
        v-model="width"
        :options="WIDTH_OPTIONS"
        ariaLabel="Ширина кадра"
      />
    </div>
    <div class="switch">
      <span class="switch-label">Зритель</span>
      <SegmentedControl
        v-model="viewer"
        :options="VIEWER_OPTIONS"
        ariaLabel="Зритель"
      />
    </div>
    <div class="switch">
      <span class="switch-label">Линейка</span>
      <SegmentedControl
        v-model="rulerMode"
        :options="RULER_OPTIONS"
        ariaLabel="Линейка прыжка верстки"
      />
    </div>
  </div>

  <p class="section-intro">
    Каталог оформления полосы эвентов над лентой глобального чата. Боевая полоса
    не тронута: каждый вариант нарисован разметкой этой страницы, и сайт живет
    на текущем оформлении, пока не выбран номер. Переключатели кормят все
    варианты сразу, одно нажатие показывает каждый из них в одном и том же
    состоянии. Ширина меняется у рамки, а не у окна, поэтому узкая полоса стоит
    рядом с широкой на одном экране. Линейка это неподвижная черта на постоянном
    расстоянии от верха рамки: если вариант двигает ленту, первое сообщение
    переезжает через нее.
  </p>
  <p class="section-intro">
    Состояние "закончился" на сайте сегодня не существует, сервер отдает только
    идущие и запланированные эвенты. Оно показано здесь потому, что решение о
    нем часть выбора, а состояние нельзя оценить вслепую. Времена в макетах
    литеральные, чтобы каталог показывал одно и то же в любой день. Зритель
    меняет только секцию 6, действий у эвентов сегодня нет ни одного.
  </p>

  <!-- ================================================================ -->
  <BlockTitle>Секция 1. Одна строка в потоке</BlockTitle>
  <p class="section-intro">
    Сегодня полоса это одна строка высотой 37px над лентой, девять процентов
    кадра чата. Варианты этой секции остаются одной строкой и спорят только о
    том, что в ней написано и как это написано.
  </p>

  <div class="variant">
    <p class="variant-label">
      <strong>1.1.</strong> Текущая полоса, базовая линия
    </p>
    <p class="variant-note">
      Плюс: минимум места, три эвента схлопываются в ту же одну строку, слои
      плавающие, лента не дергается. Минус: у запланированного нет слова
      состояния и он читается как идущий, разделитель приклеен к заголовку, а на
      375 заголовок сжимается в ноль и счетчик ложится поверх правых контролов.
    </p>
    <ChatFrameReplica :width="width" :ruler="ruler">
      <div class="strip">
        <div class="strip-row">
          <div class="v11-main">
            <template v-if="focal">
              <span class="v11-text"
                ><ChatEventLine :event="focal" :word="isLive" /></span
              ><button
                type="button"
                class="v11-chevron"
                :class="{ open: reveal.isExpanded('v11') }"
                :aria-expanded="reveal.isExpanded('v11')"
                aria-label="Описание"
                @click="reveal.toggle('v11')"
              >
                <SvgIcon name="chevronDown" /></button
              ><span v-if="restCount > 0" class="v11-more"
                ><span class="st-sep" aria-hidden="true">|{{ " " }}</span
                ><button type="button" class="st-item">
                  +{{ restCount }} запланировано</button
                ><span v-if="nearestShort" class="st-fact"
                  >, ближайший {{ nearestShort }}</span
                ></span
              >
            </template>
            <span v-else class="st-none">Нет запланированных эвентов</span>
          </div>
          <div class="v11-aside">
            <button type="button" class="st-item">
              <SvgIcon name="search" class="st-icon" />Поиск
            </button>
            <span class="st-sep" aria-hidden="true">|</span>
            <button type="button" class="st-item">
              <SvgIcon name="calendar" class="st-icon" />К дате
            </button>
          </div>
        </div>
        <div
          class="reveal-float"
          :class="{ open: reveal.isExpanded('v11') }"
          :inert="!reveal.isExpanded('v11')"
        >
          <div class="reveal-surface">
            <ChatEventCard v-if="focal" :event="focal" />
          </div>
        </div>
      </div>
    </ChatFrameReplica>
  </div>

  <div class="variant">
    <p class="variant-label">
      <strong>1.2.</strong> Слово состояния и одна копируемая строка
    </p>
    <p class="variant-note">
      Плюс: три дефекта базовой линии снимаются разом, высота не меняется ни на
      пиксель, идиома полос проекта соблюдена целиком, строка выделяется и
      копируется как текст. Минус: строка по-прежнему одна на все, про описание
      и участников она не говорит ничего.
    </p>
    <ChatFrameReplica :width="width" :ruler="ruler">
      <div class="strip">
        <div class="strip-row">
          <div class="run">
            <ChatEventRun
              :event="focal"
              :rest-count="restCount"
              :nearest="nearestShort"
            />
          </div>
          <div class="run-aside"><ChatNavRun /></div>
        </div>
      </div>
    </ChatFrameReplica>
  </div>

  <div class="variant">
    <p class="variant-label">
      <strong>1.3.</strong> Полоса пунктами: состояние, название, время,
      участники, счетчик
    </p>
    <p class="variant-note">
      Плюс: видно все, что вообще есть в списке эвентов, без единого раскрытия.
      Минус: пункты полосы по правилу проекта ссылки, а здесь половина из них
      факты, и на узкой ширине строка обрывается многоточием раньше всех
      остальных вариантов.
    </p>
    <ChatFrameReplica :width="width" :ruler="ruler">
      <div class="strip">
        <div class="strip-row">
          <div class="run">
            <template v-if="focal"
              ><span class="st-state">{{ STATE_WORD[focal.status] }}</span
              ><span class="st-sep" aria-hidden="true">{{ " | " }}</span
              ><button type="button" class="st-item st-item-strong">
                {{ focal.title }}</button
              ><span class="st-sep" aria-hidden="true">{{ " | " }}</span
              ><span class="st-fact">{{ focal.timeText }}</span
              ><template v-if="!focal.isOpen"
                ><span class="st-sep" aria-hidden="true">{{ " | " }}</span
                ><span class="st-fact">закрытый</span></template
              ><span class="st-sep" aria-hidden="true">{{ " | " }}</span
              ><span class="st-fact"
                >участников: {{ focal.participantCount }}</span
              ><template v-if="restCount > 0"
                ><span class="st-sep" aria-hidden="true">{{ " | " }}</span
                ><button type="button" class="st-item">
                  еще {{ restCount }}
                </button></template
              ></template
            >
            <span v-else class="st-none">Нет запланированных эвентов</span>
          </div>
          <div class="run-aside"><ChatNavRun /></div>
        </div>
      </div>
    </ChatFrameReplica>
  </div>

  <div class="variant">
    <p class="variant-label">
      <strong>1.4.</strong> Строка 1.2 плюс планка состояния у левого края
    </p>
    <p class="variant-note">
      Плюс: состояние ловится боковым зрением, слово при этом остается на месте,
      поэтому цвет никогда не единственный носитель смысла. Минус: третий
      цветовой акцент в кадре, где уже зеленым горят имена тех, кто в сети.
    </p>
    <ChatFrameReplica :width="width" :ruler="ruler">
      <div
        class="strip v14"
        :class="{ 'is-live': isLive, 'is-ended': focal?.status === 'Ended' }"
      >
        <div class="strip-row">
          <div class="run">
            <ChatEventRun
              :event="focal"
              :rest-count="restCount"
              :nearest="nearestShort"
            />
          </div>
          <div class="run-aside"><ChatNavRun /></div>
        </div>
      </div>
    </ChatFrameReplica>
  </div>

  <div class="variant">
    <p class="variant-label">
      <strong>1.5.</strong> Строка 1.2 плюс подкраска всей полосы, пока эвент
      идет
    </p>
    <p class="variant-note">
      Плюс: самый заметный из спокойных сигналов, ноль лишних пикселей, оверлей
      темы вместо подобранного цвета. Минус: полоса перестает быть нейтральной
      поверхностью, а на старте эвента подкраска появляется скачком.
    </p>
    <ChatFrameReplica :width="width" :ruler="ruler">
      <div class="strip v15" :class="{ 'is-live': isLive }">
        <div class="strip-row">
          <div class="run">
            <ChatEventRun
              :event="focal"
              :rest-count="restCount"
              :nearest="nearestShort"
            />
          </div>
          <div class="run-aside"><ChatNavRun /></div>
        </div>
      </div>
    </ChatFrameReplica>
  </div>

  <!-- ================================================================ -->
  <BlockTitle>Секция 2. Строка плюс раскрытие</BlockTitle>
  <p class="section-intro">
    Полоса остается одной строкой, но получает раскрытие: описание, организатор
    и участники видны без ухода со страницы. Раскрытия зарегистрированы в общем
    реестре, поэтому кнопка "Развернуть все" в правом нижнем углу открывает их
    разом, как открывала бы боевые.
  </p>

  <div class="variant">
    <p class="variant-label">
      <strong>2.1.</strong> Раскрытие вниз, карточка в потоке
    </p>
    <p class="variant-note">
      Плюс: карточка живет в потоке и ничего не перекрывает, лента остается
      читаемой ниже нее. Минус: лента съезжает вниз ровно на высоту карточки, и
      это тот самый прыжок верстки (включите линейку и нажмите "описание").
    </p>
    <ChatFrameReplica :width="width" :ruler="ruler">
      <div class="strip">
        <div class="strip-row">
          <div class="run">
            <ChatEventRun
              :event="focal"
              :rest-count="restCount"
              :nearest="nearestShort"
            >
              <template #extra>
                <button
                  type="button"
                  class="st-item"
                  :class="{ act: reveal.isExpanded('v21') }"
                  :aria-expanded="reveal.isExpanded('v21')"
                  @click="reveal.toggle('v21')"
                >
                  описание
                </button>
              </template>
            </ChatEventRun>
          </div>
          <div class="run-aside"><ChatNavRun /></div>
        </div>
        <div class="expand-fold" :class="{ open: reveal.isExpanded('v21') }">
          <div class="expand-fold-clip" :inert="!reveal.isExpanded('v21')">
            <div class="fold-body">
              <ChatEventCard v-if="focal" :event="focal" />
            </div>
          </div>
        </div>
      </div>
    </ChatFrameReplica>
  </div>

  <div class="variant">
    <p class="variant-label">
      <strong>2.2.</strong> Раскрытие плавающей карточкой
    </p>
    <p class="variant-note">
      Плюс: лента не двигается совсем, карточка садится поверх нее на общем
      темпе раскрытий. Минус: карточка закрывает первые сообщения, а в узкой
      рамке почти всю видимую ленту.
    </p>
    <ChatFrameReplica :width="width" :ruler="ruler">
      <div class="strip">
        <div class="strip-row">
          <div class="run">
            <ChatEventRun
              :event="focal"
              :rest-count="restCount"
              :nearest="nearestShort"
            >
              <template #extra>
                <button
                  type="button"
                  class="st-item"
                  :class="{ act: reveal.isExpanded('v22') }"
                  :aria-expanded="reveal.isExpanded('v22')"
                  @click="reveal.toggle('v22')"
                >
                  описание
                </button>
              </template>
            </ChatEventRun>
          </div>
          <div class="run-aside"><ChatNavRun /></div>
        </div>
        <div
          class="reveal-float"
          :class="{ open: reveal.isExpanded('v22') }"
          :inert="!reveal.isExpanded('v22')"
        >
          <div class="reveal-surface">
            <ChatEventCard v-if="focal" :event="focal" />
          </div>
        </div>
      </div>
    </ChatFrameReplica>
  </div>

  <div class="variant">
    <p class="variant-label">
      <strong>2.3.</strong> Раскрытие в список всех эвентов, строкой на эвент
    </p>
    <p class="variant-note">
      Плюс: одно раскрытие отвечает на весь вопрос сразу, включая
      запланированные и их время. Минус: описания в нем нет, за ним все равно
      нужен второй шаг.
    </p>
    <ChatFrameReplica :width="width" :ruler="ruler">
      <div class="strip">
        <div class="strip-row">
          <div class="run">
            <ChatEventRun
              :event="focal"
              :rest-count="restCount"
              :nearest="nearestShort"
            >
              <template #extra>
                <button
                  type="button"
                  class="st-item"
                  :class="{ act: reveal.isExpanded('v23') }"
                  :aria-expanded="reveal.isExpanded('v23')"
                  @click="reveal.toggle('v23')"
                >
                  все эвенты
                </button>
              </template>
            </ChatEventRun>
          </div>
          <div class="run-aside"><ChatNavRun /></div>
        </div>
        <div class="expand-fold" :class="{ open: reveal.isExpanded('v23') }">
          <div class="expand-fold-clip" :inert="!reveal.isExpanded('v23')">
            <div class="fold-body">
              <div v-for="ev in events" :key="ev.id" class="list-row">
                <span class="st-state">{{ STATE_WORD[ev.status] }}</span
                ><span class="st-sep" aria-hidden="true">{{ " | " }}</span
                ><span class="list-title">{{ ev.title }}</span
                ><span class="st-sep" aria-hidden="true">{{ " | " }}</span
                ><span class="st-fact">{{ ev.timeText }}</span
                ><span class="st-sep" aria-hidden="true">{{ " | " }}</span
                ><span class="st-fact"
                  >участников: {{ ev.participantCount }}</span
                >
              </div>
              <span v-if="!events.length" class="st-none"
                >Нет запланированных эвентов</span
              >
            </div>
          </div>
        </div>
      </div>
    </ChatFrameReplica>
  </div>

  <!-- ================================================================ -->
  <BlockTitle>Секция 3. Перестроенная шапка кадра</BlockTitle>
  <p class="section-intro">
    Варианты, которые меняют не текст в полосе, а саму ее конструкцию.
  </p>

  <div class="variant">
    <p class="variant-label">
      <strong>3.1.</strong> Две строки: эвент сверху, навигация чата снизу
    </p>
    <p class="variant-note">
      Плюс: эвент и навигация перестают спорить за одну ширину, на 375 целы обе
      строки без единого сокращения. Минус: шапка кадра удваивается по высоте
      навсегда, в том числе когда эвентов нет вовсе.
    </p>
    <ChatFrameReplica :width="width" :ruler="ruler">
      <div class="strip">
        <div class="strip-row">
          <div class="run">
            <ChatEventRun
              :event="focal"
              :rest-count="restCount"
              :nearest="nearestShort"
            />
          </div>
        </div>
        <div class="strip-row v31-nav">
          <div class="run"><ChatNavRun /></div>
        </div>
      </div>
    </ChatFrameReplica>
  </div>

  <div class="variant">
    <p class="variant-label">
      <strong>3.2.</strong> Пилюля состояния плюс название рядом
    </p>
    <p class="variant-note">
      Плюс: состояние читается мгновенно и почти не занимает ширины. Минус:
      скопированная строка теряет двоеточие и читается хуже, а заливка спорит с
      тем, что полоса строго текстовая.
    </p>
    <ChatFrameReplica :width="width" :ruler="ruler">
      <div class="strip">
        <div class="strip-row">
          <div class="run">
            <template v-if="focal"
              ><span
                class="chip"
                :class="{
                  'is-live': isLive,
                  'is-ended': focal?.status === 'Ended',
                }"
                >{{ STATE_WORD[focal.status] }}</span
              ><span class="chip-space">{{ " " }}</span
              ><ChatEventLine :event="focal" :word="false" /><template
                v-if="restCount > 0"
                ><span class="st-sep" aria-hidden="true">{{ " | " }}</span
                ><button type="button" class="st-item">
                  +{{ restCount }} запланировано
                </button></template
              ></template
            >
            <span v-else class="st-none">Нет запланированных эвентов</span>
          </div>
          <div class="run-aside"><ChatNavRun /></div>
        </div>
      </div>
    </ChatFrameReplica>
  </div>

  <div class="variant">
    <p class="variant-label">
      <strong>3.3.</strong> Только пилюля, подробности в поповере
    </p>
    <p class="variant-note">
      Плюс: самое малое пятно из всех, на 375 помещается гарантированно, при
      десяти эвентах ширина не меняется. Минус: название идущего эвента не видно
      без нажатия, а это и есть главная информация.
    </p>
    <ChatFrameReplica :width="width" :ruler="ruler">
      <div class="strip">
        <div class="strip-row">
          <div class="run">
            <template v-if="focal"
              ><button
                type="button"
                class="chip chip-button"
                :class="{
                  'is-live': isLive,
                  'is-ended': focal?.status === 'Ended',
                }"
                :aria-expanded="reveal.isExpanded('v33')"
                @click="reveal.toggle('v33')"
              >
                {{ STATE_WORD[focal.status] }} эвент</button
              ><template v-if="restCount > 0"
                ><span class="st-sep" aria-hidden="true">{{ " | " }}</span
                ><button type="button" class="st-item">
                  +{{ restCount }} запланировано
                </button></template
              ></template
            >
            <span v-else class="st-none">Нет запланированных эвентов</span>
          </div>
          <div class="run-aside"><ChatNavRun /></div>
        </div>
        <div
          class="reveal-float"
          :class="{ open: reveal.isExpanded('v33') }"
          :inert="!reveal.isExpanded('v33')"
        >
          <div class="reveal-surface">
            <ChatEventCard v-if="focal" :event="focal" />
          </div>
        </div>
      </div>
    </ChatFrameReplica>
  </div>

  <!-- ================================================================ -->
  <BlockTitle>Секция 4. Эвент внутри ленты</BlockTitle>
  <p class="section-intro">
    Эвент как часть переписки, а не надпись над ней. Обе формы требуют работы на
    сервере или в клиентском типе сообщения, эта цена названа в подписи.
  </p>

  <div class="variant">
    <p class="variant-label">
      <strong>4.1.</strong> Отметка в ленте в идиоме разделителя
    </p>
    <p class="variant-note">
      Плюс: эвент попадает в историю и виден при перечитывании архива,
      постоянного места в кадре нет. Минус: нужны серверные отметки, иначе при
      загрузке архива их неоткуда взять, разделитель целиком декоративный для
      читалки экрана, а зашедший в середину не узнает про эвент ничего.
    </p>
    <ChatFrameReplica :width="width" :ruler="ruler">
      <div class="strip">
        <div class="strip-row">
          <div class="run">
            <template v-if="focal && isLive"
              ><ChatEventLine :event="focal"
            /></template>
            <span v-else class="st-none">Идущих эвентов нет</span>
          </div>
          <div class="run-aside"><ChatNavRun /></div>
        </div>
      </div>
      <template #in-feed>
        <DashSeparator
          v-if="feedMarkLabel"
          :label="feedMarkLabel"
          spacing="small"
        />
      </template>
    </ChatFrameReplica>
  </div>

  <div class="variant">
    <p class="variant-label">
      <strong>4.2.</strong> Строка 1.2 плюс отбивка сообщений эвента
    </p>
    <p class="variant-note">
      Плюс: видно не только то, что эвент идет, но и что именно в нем написано.
      Минус: клиентский тип сообщения не несет идентификатор эвента и его надо
      добавить (сервер уже присылает), а подкрашенная половина ленты утомляет
      глаз на длинном эвенте.
    </p>
    <ChatFrameReplica :width="width" :ruler="ruler" :mark-from="markFrom">
      <div class="strip">
        <div class="strip-row">
          <div class="run">
            <ChatEventRun
              :event="focal"
              :rest-count="restCount"
              :nearest="nearestShort"
            />
          </div>
          <div class="run-aside"><ChatNavRun /></div>
        </div>
      </div>
    </ChatFrameReplica>
  </div>

  <!-- ================================================================ -->
  <BlockTitle>Секция 5. Эвенты вне кадра чата</BlockTitle>
  <p class="section-intro">
    Обе формы уносят эвенты из кадра и обе оставляют в полосе "Поиск" и "К
    дате", то есть освобождают заметно меньше места, чем кажется на первый
    взгляд.
  </p>

  <div class="variant">
    <p class="variant-label">
      <strong>5.1.</strong> Панель эвентов в правом сайдбаре
    </p>
    <p class="variant-note">
      Плюс: кадр чата освобождается, каждому эвенту хватает места на дату,
      состояние и число участников, три эвента перестают быть проблемой. Минус:
      ниже 1000px сайдбары уходят в drawer и на телефоне эвенты пропадают
      совсем, а полоса все равно остается ради навигации.
    </p>
    <div class="with-sidebar">
      <ChatFrameReplica
        class="with-sidebar-frame"
        :width="width"
        :ruler="ruler"
      >
        <div class="strip">
          <div class="strip-row">
            <div class="run"></div>
            <div class="run-aside"><ChatNavRun /></div>
          </div>
        </div>
      </ChatFrameReplica>
      <div class="sidebar-panel">
        <div class="sidebar-title">Эвенты</div>
        <div v-for="ev in events" :key="ev.id" class="sidebar-row">
          <div class="sidebar-row-title">{{ ev.title }}</div>
          <div class="sidebar-row-meta">
            {{ STATE_WORD[ev.status] }}, {{ ev.timeText }}, участников:
            {{ ev.participantCount }}
          </div>
        </div>
        <div v-if="!events.length" class="st-none">Эвентов нет</div>
      </div>
    </div>
  </div>

  <div class="variant">
    <p class="variant-label">
      <strong>5.2.</strong> Переключатель видов "Чат | Эвенты" над рамкой
    </p>
    <p class="variant-note">
      Плюс: списку эвентов больше не тесно, помещаются время, участники и
      описание, узкая ширина решается сама собой. Минус: пока открыты эвенты,
      чата не видно, а идущий эвент все равно приходится держать в полосе, то
      есть вариант не заменяет ее, а добавляется к ней.
    </p>
    <ChatFrameReplica
      :width="width"
      :ruler="ruler"
      :show-feed="tabView === 'chat'"
    >
      <template #above>
        <div class="views">
          <button
            type="button"
            class="view-link"
            :class="{ active: tabView === 'chat' }"
            @click="tabView = 'chat'"
          >
            Чат</button
          ><span class="st-sep" aria-hidden="true">{{ " | " }}</span
          ><button
            type="button"
            class="view-link"
            :class="{ active: tabView === 'events' }"
            @click="tabView = 'events'"
          >
            Эвенты
          </button>
        </div>
      </template>
      <div class="strip">
        <div class="strip-row">
          <div class="run">
            <template v-if="focal && isLive"
              ><ChatEventLine :event="focal"
            /></template>
            <span v-else class="st-none">Идущих эвентов нет</span>
          </div>
          <div class="run-aside"><ChatNavRun /></div>
        </div>
      </div>
      <template #instead>
        <div class="events-view">
          <div v-for="ev in events" :key="ev.id" class="events-view-row">
            <div class="events-view-title">
              {{ STATE_WORD[ev.status] }}: {{ ev.title }}
            </div>
            <div class="events-view-meta">
              {{ ev.timeText }}, участников: {{ ev.participantCount
              }}<template v-if="!ev.isOpen">, закрытый</template>
            </div>
          </div>
          <div v-if="!events.length" class="st-none">
            Нет запланированных эвентов
          </div>
        </div>
      </template>
    </ChatFrameReplica>
  </div>

  <!-- ================================================================ -->
  <BlockTitle>Секция 6. Где живет действие</BlockTitle>
  <p class="section-intro">
    Действий у эвентов сегодня нет ни одного при полностью готовом клиентском
    API: записаться, выйти, начать и закончить некому вызвать. Секция
    показывает, куда действие встанет, если оно понадобится, и меняется
    переключателем зрителя. Если действий не будет, обе строки вычеркиваются
    вместе с секцией.
  </p>

  <div class="variant">
    <p class="variant-label"><strong>6.1.</strong> Действие пунктом полосы</p>
    <p class="variant-note">
      Плюс: высота не меняется ни на пиксель, действие в той же идиоме, что и
      остальные пункты, на узкой ширине сокращается вместе с ними. Минус:
      действие выглядит ровно как навигация, и промахнуться легко.
    </p>
    <ChatFrameReplica :width="width" :ruler="ruler">
      <div class="strip">
        <div class="strip-row">
          <div class="run">
            <ChatEventRun
              :event="focal"
              :rest-count="restCount"
              :nearest="nearestShort"
            >
              <template v-if="action || actionNote" #extra>
                <button v-if="action" type="button" class="st-item st-item-do">
                  {{ action }}
                </button>
                <span v-else class="st-fact">{{ actionNote }}</span>
              </template>
            </ChatEventRun>
          </div>
          <div class="run-aside"><ChatNavRun /></div>
        </div>
      </div>
    </ChatFrameReplica>
  </div>

  <div class="variant">
    <p class="variant-label">
      <strong>6.2.</strong> Действие кнопкой у правого края
    </p>
    <p class="variant-note">
      Плюс: действие не спутать ни с чем, оно читается как действие. Минус:
      кнопка высотой 38px растит полосу в полтора раза и держит эту высоту
      всегда, даже когда действия нет.
    </p>
    <ChatFrameReplica :width="width" :ruler="ruler">
      <div class="strip">
        <div class="strip-row">
          <div class="run">
            <ChatEventRun
              :event="focal"
              :rest-count="restCount"
              :nearest="nearestShort"
            />
          </div>
          <div class="run-aside run-aside-button">
            <button v-if="actionButton" type="button" class="do-button">
              {{ actionButton }}
            </button>
            <ChatNavRun />
          </div>
        </div>
      </div>
    </ChatFrameReplica>
  </div>

  <!-- ================================================================ -->
  <BlockTitle>Рекомендация</BlockTitle>
  <p class="section-intro">
    1.2 как основа, 2.2 как раскрытие, 1.4 как необязательная надстройка.
  </p>
  <p class="section-intro">
    1.2 снимает все три дефекта текущей полосы (неотличимое запланированное
    состояние, склеенная строка, обвал заголовка на узком экране) и не стоит ни
    одного лишнего пикселя. 2.2 добавляет к ней описание, организатора и
    участников, не двигая ленту: 2.1 честнее по устройству, но платит прыжком
    верстки на каждом открытии, а в кадре высотой 320px это половина видимого
    чата. 1.4 стоит трех пикселей и отдает состояние боковому зрению, но это уже
    вкус, а не дефект.
  </p>
  <p class="section-intro">
    Отклоняю: 1.3 ломает правило "пункты полосы это ссылки" и обрывается на
    узкой ширине первым. 1.5 красит нейтральную поверхность и мигает на старте.
    3.1 удваивает шапку навсегда ради задачи, которую 1.2 решает одним запросом
    ширины. 3.2 и 3.3 прячут название, а именно оно главное. 4.1 и 4.2 требуют
    работы на сервере и в типах и при этом не отвечают на вопрос "что
    запланировано". 5.1 теряет эвенты на телефоне. 5.2 не заменяет полосу, а
    добавляется к ней.
  </p>
  <p class="section-intro">
    Развилки, которые нужно закрыть до реализации: показывать ли состояние
    "закончился" вообще (сегодня сервер его не отдает), нужны ли эвентам
    действия (секция 6) и может ли полоса исчезать, когда эвентов нет, при том
    что в ней живет навигация по чату.
  </p>
</template>

<style scoped lang="sass">
@import "@/assets/styles/Inputs"
@import "@/assets/styles/ZIndex"
@import "@/assets/styles/Animations"

// ─────────────────────────────────────────────────────────────
// Catalog chrome
// ─────────────────────────────────────────────────────────────
.switches
  position: sticky
  top: 0
  z-index: $z-sticky
  display: flex
  flex-wrap: wrap
  align-items: center
  gap: $small $medium
  padding: $small 0
  margin-bottom: $medium
  background-color: $bg-page
  border-bottom: 1px dashed $border

.switch
  display: flex
  align-items: center
  gap: $small

.switch-label
  font-size: $secondary-font-size
  color: $text-muted

.section-intro
  margin: 0 0 $medium
  line-height: 1.5

.variant
  margin-bottom: $big

.variant-label
  margin: 0 0 $tiny
  line-height: 1.5

.variant-note
  margin: 0 0 $small
  font-size: $secondary-font-size
  color: $text-muted
  line-height: 1.5

// ─────────────────────────────────────────────────────────────
// The strip surface every variant starts from — the geometry of the current
// panel, so the catalog compares content and not padding.
// ─────────────────────────────────────────────────────────────
.strip
  position: relative
  flex-shrink: 0
  background-color: $bg-element
  border-bottom: 1px dashed $border
  font-size: $secondary-font-size

.strip-row
  position: relative
  display: flex
  align-items: center
  justify-content: space-between
  gap: $medium
  padding: $small $medium
  line-height: 1.4
  white-space: nowrap

// One inline-flow run: no flex children and no gaps inside it, so a selection
// copies the whole composite as a single line of text.
.run
  flex: 0 1 auto
  min-width: 0
  overflow: hidden
  text-overflow: ellipsis
  white-space: nowrap

.run-aside
  flex: none

.st-sep
  color: $text-muted

.st-fact
  color: $text-muted

.st-state
  color: $text
  font-weight: 600

.st-none
  color: $text-muted

.st-item
  +inline-link-button
  &
    font-size: $secondary-font-size
    white-space: nowrap
    color: $text-muted
  &:hover:not(:disabled)
    color: $link
  &.act
    color: $link
    font-weight: 600
  &:focus:not(:focus-visible)
    outline: none
  &:focus-visible
    outline: 2px solid $border-focus
    outline-offset: 2px

.st-item-strong
  font-weight: 600
  color: $text

.st-item-do
  color: $link

.st-icon
  width: 14px
  height: 14px
  vertical-align: -2px
  margin-right: $minor

// ─────────────────────────────────────────────────────────────
// Reveals — the site's two idioms, side by side for comparison: the floating
// layer (feed stays put) and the in-flow fold (feed moves down).
// ─────────────────────────────────────────────────────────────
.reveal-float
  position: absolute
  top: 100%
  left: 0
  right: 0
  z-index: $z-dropdown
  opacity: 0
  transform: translateY(-$small)
  pointer-events: none
  transition: opacity $expand-duration $expand-easing, transform $expand-duration $expand-easing

  &.open
    opacity: 1
    transform: translateY(0)
    pointer-events: auto

.reveal-surface
  padding: $small $medium
  max-height: 220px
  overflow-y: auto
  background-color: $bg-element
  border: 1px solid $border
  box-shadow: 0 2px 8px $shadow-color

.fold-body
  padding: $small $medium
  border-top: 1px dashed $border

.list-row
  line-height: 1.4

.list-title
  color: $text

// ─────────────────────────────────────────────────────────────
// 1.1 — the current strip, reproduced with its own defects: flex children with
// gaps instead of one text run, a separator glued to the title, and not a
// single narrow rule.
// ─────────────────────────────────────────────────────────────
.v11-main
  display: flex
  align-items: center
  gap: $minor
  flex: 1 1 auto
  min-width: 0

.v11-text
  flex: 0 1 auto
  min-width: 0
  overflow: hidden
  text-overflow: ellipsis
  white-space: nowrap

.v11-more
  flex: 0 0 auto
  color: $text-muted

.v11-aside
  flex: none
  display: flex
  align-items: center
  gap: $small

.v11-chevron
  flex: 0 0 auto
  display: inline-flex
  align-items: center
  justify-content: center
  width: 20px
  height: 20px
  padding: 0
  border: none
  background: none
  color: $text-muted
  line-height: 0
  cursor: pointer

  svg
    width: 14px
    height: 14px
    transition: transform $expand-duration $expand-easing

  &.open
    color: $text
    svg
      transform: rotate(180deg)

  &:hover
    color: $text

  &:focus:not(:focus-visible)
    outline: none

  &:focus-visible
    outline: 2px solid $border-focus
    outline-offset: 2px

// ─────────────────────────────────────────────────────────────
// 1.4 / 1.5 — state carried by colour, always next to the word that carries it
// in text. Both take the tint from theme variables, never a picked value.
// ─────────────────────────────────────────────────────────────
.v14
  border-left: 3px solid $border

  &.is-live
    border-left-color: $accent-green

  &.is-ended
    border-left-color: $text-muted

.v15.is-live::before
  content: ""
  position: absolute
  inset: 0
  pointer-events: none
  +tint($accent-green, 12%)

// 3.1 — the navigation gets its own row, so nothing competes for the width.
.v31-nav
  padding-top: $tiny
  padding-bottom: $tiny
  border-top: 1px dashed $border

// ─────────────────────────────────────────────────────────────
// 3.2 / 3.3 — the state as a chip.
// ─────────────────────────────────────────────────────────────
.chip
  display: inline-block
  padding: 0 $small
  border-radius: $border-radius
  font-size: $tertiary-font-size
  font-weight: 600
  color: $text
  background-color: $bg-element-accent
  vertical-align: 1px

  &.is-live
    +tint($accent-green, 20%)

  &.is-ended
    +tint($text-muted, 18%)

.chip-space
  display: inline-block
  width: $minor
  white-space: pre

.chip-button
  border: none
  font-family: inherit
  cursor: pointer

// ─────────────────────────────────────────────────────────────
// 5.1 — the sidebar panel next to the freed frame.
// ─────────────────────────────────────────────────────────────
.with-sidebar
  display: flex
  align-items: flex-start
  gap: $medium

.with-sidebar-frame
  flex: 1 1 auto
  min-width: 0

.sidebar-panel
  +card($small)
  flex: none
  width: 220px
  font-size: $secondary-font-size

.sidebar-title
  margin-bottom: $small
  font-weight: 600
  color: $text

.sidebar-row
  margin-bottom: $small
  line-height: 1.4

  &:last-child
    margin-bottom: 0

.sidebar-row-title
  color: $text

.sidebar-row-meta
  color: $text-muted

// ─────────────────────────────────────────────────────────────
// 5.2 — the view switcher above the frame, in the navigation-strip idiom.
// ─────────────────────────────────────────────────────────────
.views
  font-size: $secondary-font-size

.view-link
  +inline-link-button
  &
    font-size: $secondary-font-size
    color: $link
  &.active
    font-weight: 600
  &:focus:not(:focus-visible)
    outline: none
  &:focus-visible
    outline: 2px solid $border-focus
    outline-offset: 2px

.events-view
  padding: $small $medium

.events-view-row
  margin-bottom: $small
  line-height: 1.4

.events-view-title
  color: $text
  font-weight: 500

.events-view-meta
  font-size: $secondary-font-size
  color: $text-muted

// ─────────────────────────────────────────────────────────────
// 6.2 — a real control in the strip, at the height a real control has.
// ─────────────────────────────────────────────────────────────
.run-aside-button
  display: flex
  align-items: center
  gap: $medium

.do-button
  +button
  &
    font-size: $secondary-font-size
</style>
