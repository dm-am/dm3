<template>
  <section id="notifications" ref="root" class="section">
    <!-- The notifications page links here, "/account#notifications". -->
    <h2 class="section-title">Уведомления</h2>

    <!-- A grey panel, as "Сессии" is, with a framed row a channel: the name and its state, the press beside them, and under them what it brings or the steps that link it. -->
    <div class="notifications-content">
      <!-- A row per channel in its own shape: "Не подключен" is said only by an answer. -->
      <AccountSectionSkeleton
        v-if="loading"
        shape="rows"
        :count="CHANNELS.length"
      />

      <ErrorState
        v-else-if="loadError"
        :message="loadError"
        :retry="loadPreferences"
      />

      <div v-else class="channels">
        <div
          v-for="channel in CHANNELS"
          :key="channel.id"
          class="channel-item"
          :data-channel="channel.id"
        >
          <div class="channel-head">
            <div class="channel-info">
              <h3 class="channel-name">{{ channel.name }}</h3>
              <p class="channel-state">
                {{ connected(channel.id) ? "Подключен" : "Не подключен" }}
              </p>
            </div>
            <Button
              v-if="connected(channel.id)"
              type="button"
              class="disconnect-btn"
              :loading="disconnecting === channel.id"
              @click="disconnect(channel.id)"
            >
              Отключить
            </Button>
            <Button
              v-else-if="linking[channel.id]"
              type="button"
              class="cancel-btn"
              @click="cancelLinking(channel.id)"
            >
              Отмена
            </Button>
            <Button
              v-else
              type="button"
              class="connect-btn"
              :loading="generating === channel.id"
              @click="startLinking(channel.id)"
            >
              Подключить
            </Button>
          </div>

          <!-- A linked channel's switch and the column of what it sends, under a caption of its own. -->
          <div v-if="connected(channel.id)" class="channel-settings">
            <label class="setting-item">
              <input
                type="checkbox"
                v-model="enabled[channel.id]"
                :aria-busy="saving || undefined"
                @click="holdWhileSaving"
                @change="updateEnabled(channel.id)"
              />
              <span>Включить уведомления</span>
            </label>

            <template v-if="enabled[channel.id]">
              <p :id="categoriesId(channel.id)" class="categories-caption">
                Что присылать
              </p>
              <div
                class="categories-list"
                role="group"
                :aria-labelledby="categoriesId(channel.id)"
              >
                <label
                  v-for="cat in allCategories"
                  :key="cat.value"
                  class="setting-item"
                >
                  <input
                    type="checkbox"
                    :checked="categories[channel.id].includes(cat.value)"
                    :aria-busy="saving || undefined"
                    @click="holdWhileSaving"
                    @change="toggleCategory(channel.id, cat.value)"
                  />
                  <span>{{ cat.label }}</span>
                </label>
              </div>
            </template>
          </div>

          <!-- The steps of linking, under their row; focus comes here when they open. A
               bot that takes the code in its start link is opened by that link, the other
               is sent the command as words of the line. -->
          <div
            v-else-if="linking[channel.id]"
            class="link-process"
            tabindex="-1"
          >
            <template v-if="channel.startsWithCode">
              <p class="link-step">
                1. Откройте бота по ссылке
                <a
                  :href="startHref(channel.bot.href, linking[channel.id]?.code)"
                  target="_blank"
                  class="bot-link start-link"
                  >{{
                    startLabel(channel.bot.href, linking[channel.id]?.code)
                  }}</a
                >
              </p>
              <p class="link-step">2. Нажмите в нем "Запустить"</p>
            </template>
            <template v-else>
              <p class="link-step">
                1. Откройте бота
                <a :href="channel.bot.href" target="_blank" class="bot-link">{{
                  channel.bot.label
                }}</a>
              </p>
              <p class="link-step">
                2. Отправьте боту команду
                <code class="command"
                  >/connect {{ linking[channel.id]?.code }}</code
                >
              </p>
            </template>
            <p class="timer">
              <span class="timer-label">Код действителен:</span>{{ " "
              }}<span class="timer-value">{{ timeLeft(channel.id) }}</span>
            </p>
          </div>

          <ActionResult :result="results[channel.id]" class="channel-refusal" />
        </div>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { nextTick, ref, reactive, onMounted, onUnmounted, useId } from "vue";
import { accountApi, type BotType } from "@/entities/user";
import type {
  NotificationSettings,
  NotificationCategory,
} from "@/shared/api/models/account";
import { useActionResult } from "@/shared/lib/composables/useActionResult";
import { ActionResult } from "@/shared/ui/ActionResult";
import Button from "@/shared/ui/Button/Button.vue";
import { ErrorState } from "@/shared/ui/ErrorState";
import AccountSectionSkeleton from "./AccountSectionSkeleton.vue";
import { OWNED_BY_CARD } from "./loadFailure";

const LOAD_FAILED = "Не удалось загрузить настройки уведомлений";

/**
 * The two channels, in the order of the rows; a row is drawn from one of
 * these. The bots are named as the server is configured. `startsWithCode`: the
 * bot takes the code in its start link, which Telegram hands it as "/start <code>".
 */
const CHANNELS: readonly {
  id: BotType;
  name: string;
  bot: { href: string; label: string };
  startsWithCode?: boolean;
}[] = [
  {
    id: "telegram",
    name: "Telegram",
    bot: { href: "https://t.me/DM3NotifyBot", label: "@DM3NotifyBot" },
    startsWithCode: true,
  },
  {
    id: "discord",
    name: "Discord",
    bot: { href: "https://discord.com/", label: "Dungeon Master Bot" },
  },
];

/** The address of the bot with the code in its start parameter. */
function startHref(botHref: string, code: string | undefined): string {
  return `${botHref}?start=${encodeURIComponent(code ?? "")}`;
}

/** The same address as the words of the link: without the scheme. */
function startLabel(botHref: string, code: string | undefined): string {
  return startHref(botHref, code).replace(/^https?:\/\//, "");
}

const loading = ref(true);
const loadError = ref<string | null>(null);
const saving = ref(false);
const disconnecting = ref<BotType | null>(null);
const generating = ref<BotType | null>(null);

const preferences = reactive<NotificationSettings>({
  telegram: undefined,
  discord: undefined,
});

const allCategories: { value: NotificationCategory; label: string }[] = [
  { value: "Messages", label: "Личные сообщения" },
  { value: "Forum", label: "Форум" },
  { value: "Games", label: "Игры" },
  { value: "Subscriptions", label: "Подписки" },
  { value: "Security", label: "Безопасность" },
  { value: "Moderation", label: "Модерация" },
  { value: "Blog", label: "Блоги" },
];

const enabled = reactive<Record<BotType, boolean>>({
  telegram: false,
  discord: false,
});
const categories = reactive<Record<BotType, NotificationCategory[]>>({
  telegram: [],
  discord: [],
});

const connected = (id: BotType) => preferences[id]?.connected === true;

// The ids of the captions over the channels' categories.
const baseId = useId();
const categoriesId = (id: BotType) => `${baseId}-${id}-categories`;

onMounted(async () => {
  await loadPreferences();
});

/** Takes one channel's preferences as the server holds them, boxes included. */
function takeChannel(id: BotType, data: NotificationSettings) {
  preferences[id] = data[id];
  enabled[id] = data[id]?.enabled ?? false;
  categories[id] = [...(data[id]?.enabledCategories ?? [])];
}

async function loadPreferences() {
  loading.value = true;
  const { data, error } =
    await accountApi.getNotificationPreferences(OWNED_BY_CARD);
  loading.value = false;

  loadError.value = !error && data ? null : LOAD_FAILED;
  if (!error && data) {
    for (const { id } of CHANNELS) takeChannel(id, data);
  }
}

// A setting stays drawn as it is while it is saved, and a press meanwhile is
// refused before it toggles: disabled, a ticked box would contradict itself.
function holdWhileSaving(event: Event) {
  if (saving.value) event.preventDefault();
}

// A refused press, at the foot of its row: the box goes back and the line says why.
const results: Record<BotType, ReturnType<typeof useActionResult>> = {
  telegram: useActionResult(),
  discord: useActionResult(),
};

async function updateEnabled(id: BotType) {
  saving.value = true;
  results[id].clear();
  const { error } = await accountApi.updateNotificationPreferences({
    [id]: { enabled: enabled[id] },
  });
  saving.value = false;

  if (error) {
    results[id].refuse(error, "Не удалось сохранить настройки уведомлений");
    enabled[id] = !enabled[id];
  }
}

async function toggleCategory(id: BotType, category: NotificationCategory) {
  const list = categories[id];
  const idx = list.indexOf(category);
  if (idx >= 0) {
    list.splice(idx, 1);
  } else {
    list.push(category);
  }

  saving.value = true;
  results[id].clear();
  const { error } = await accountApi.updateNotificationPreferences({
    [id]: { enabledCategories: [...list] },
  });
  saving.value = false;

  if (error) {
    results[id].refuse(error, "Не удалось сохранить настройки уведомлений");
    if (idx >= 0) {
      list.push(category);
    } else {
      list.splice(list.indexOf(category), 1);
    }
  }
}

async function disconnect(id: BotType) {
  disconnecting.value = id;
  results[id].clear();
  const { error } = await accountApi.disconnectBot(id);
  disconnecting.value = null;

  if (error) {
    const name = CHANNELS.find((channel) => channel.id === id)?.name;
    results[id].refuse(error, `Не удалось отключить ${name}`);
  } else {
    // No toast: the row redraws itself as not linked.
    preferences[id] = undefined;
    void focusIn(id, ".connect-btn");
  }
}

/** A code handed out for one channel, and the time it has left. */
interface Linking {
  code: string;
  secondsLeft: number;
}

const linking = reactive<Record<BotType, Linking | null>>({
  telegram: null,
  discord: null,
});
const timers: Partial<Record<BotType, ReturnType<typeof setInterval>>> = {};

async function startLinking(id: BotType) {
  generating.value = id;
  results[id].clear();
  const { data, error } = await accountApi.generateBotCode(id);
  generating.value = null;

  if (error) {
    results[id].refuse(error, "Не удалось сгенерировать код");
    return;
  }
  if (!data) return;

  const secondsLeft = Math.max(
    0,
    Math.floor((new Date(data.expiresUtc).getTime() - Date.now()) / 1000),
  );
  linking[id] = { code: data.code, secondsLeft };
  void focusIn(id, ".link-process");

  // Running out puts the row back with "Подключить".
  stopTimer(id);
  timers[id] = setInterval(() => {
    const open = linking[id];
    if (!open) return stopTimer(id);
    open.secondsLeft--;
    if (open.secondsLeft <= 0) stopLinking(id);
  }, 1000);
}

function stopTimer(id: BotType) {
  const timer = timers[id];
  if (timer) clearInterval(timer);
  delete timers[id];
}

/** Where focus goes when a press takes away the control it was on. */
const root = ref<HTMLElement | null>(null);

async function focusIn(id: BotType, selector: string) {
  await nextTick();
  root.value
    ?.querySelector<HTMLElement>(`[data-channel="${id}"] ${selector}`)
    ?.focus();
}

/** "Отмена": the steps go, and the press that opens them again takes focus. */
function cancelLinking(id: BotType) {
  stopLinking(id);
  void focusIn(id, ".connect-btn");
}

function stopLinking(id: BotType) {
  linking[id] = null;
  stopTimer(id);
}

/** The time a code has left, as "13:58". */
function timeLeft(id: BotType): string {
  const seconds = linking[id]?.secondsLeft ?? 0;
  const mins = Math.floor(seconds / 60);
  const secs = seconds % 60;
  return `${mins}:${secs.toString().padStart(2, "0")}`;
}

/**
 * The command is sent from the bot, away from this page: coming back to the
 * page while a row shows its steps reads the channels again, and a linked
 * channel trades the steps for its settings.
 */
let checking = false;

async function checkLinked() {
  if (checking || document.visibilityState === "hidden") return;
  const open = CHANNELS.filter(({ id }) => linking[id]);
  if (!open.length) return;

  checking = true;
  const { data, error } =
    await accountApi.getNotificationPreferences(OWNED_BY_CARD);
  checking = false;
  if (error || !data) return;

  for (const { id } of open) {
    if (!linking[id] || !data[id]?.connected) continue;
    takeChannel(id, data);
    stopLinking(id);
  }
}

onMounted(() => {
  document.addEventListener("visibilitychange", checkLinked);
  window.addEventListener("focus", checkLinked);
});

/**
 * The router scrolls to "#notifications" while the blocks above still load,
 * and their growth pushes this section past the window; so while the
 * address names it, the section brings itself back into view as the page
 * grows, until the reader takes the page or it settles.
 */
const READER_INPUTS = ["wheel", "keydown", "pointerdown"];
const SETTLE_MS = 5000;

let letPageGo: (() => void) | null = null;

function keepInViewWhileThePageLoads() {
  const section = root.value;
  const page = section?.parentElement;
  if (window.location.hash !== "#notifications" || !section || !page) return;

  const observer = new ResizeObserver(() =>
    section.scrollIntoView({ block: "start" }),
  );
  const letGo = () => {
    observer.disconnect();
    clearTimeout(timer);
    for (const input of READER_INPUTS)
      window.removeEventListener(input, letGo, true);
    letPageGo = null;
  };
  const timer = setTimeout(letGo, SETTLE_MS);
  for (const input of READER_INPUTS)
    window.addEventListener(input, letGo, { capture: true, passive: true });
  letPageGo = letGo;
  // An observer reports its starting size: the first scroll.
  observer.observe(page);
}

onMounted(keepInViewWhileThePageLoads);

onUnmounted(() => {
  document.removeEventListener("visibilitychange", checkLinked);
  window.removeEventListener("focus", checkLinked);
  for (const { id } of CHANNELS) stopTimer(id);
  letPageGo?.();
});
</script>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *
@use "../AccountPage.styles" as *

// The grey panel, as "Сессии" has it.
.notifications-content
  padding: $medium
  background-color: $bg-element

.channels
  display: flex
  flex-direction: column
  gap: $small

// A framed row of the panel, as a row of "Сессии".
.channel-item
  display: flex
  flex-direction: column
  gap: $small
  padding: $small $medium
  background-color: $bg-element
  border: 1px solid $border

// The name and its state on the left, the press level with them on the right, at every width.
.channel-head
  display: grid
  grid-template-columns: minmax(0, 1fr) auto
  column-gap: $medium
  align-items: center

.channel-info
  display: flex
  flex-direction: column
  gap: $tiny
  min-width: 0

// The main word of the row, as the device of a session.
.channel-name
  margin: 0
  font-size: $font-size
  font-weight: 400
  color: $text

// The line under the name, as under the device of a session.
.channel-state
  margin: 0
  font-size: $secondary-font-size
  color: $text-muted

.channel-settings
  display: flex
  flex-direction: column
  gap: $small

// A box and its words.
.setting-item
  display: flex
  align-items: center
  gap: $small
  cursor: pointer
  color: $text

// The caption of a group of boxes, as the blacklist's.
.categories-caption
  margin: $small 0 0
  +field-caption

.categories-list
  display: flex
  flex-direction: column
  gap: $small

.link-process
  display: flex
  flex-direction: column
  gap: $small

.link-step
  margin: 0
  color: $text

// The command in the site's code font, selected whole by a tap, never broken.
.command
  font-family: $code-font
  font-size: $font-size
  white-space: nowrap
  user-select: all

// The start link is never broken either: split at its "?" it reads as two.
.start-link
  white-space: nowrap

.timer
  margin: 0
  font-size: $secondary-font-size
  color: $text-muted

.timer-value
  font-size: $font-size
  font-weight: 400
  color: $text

// Spaced by the gap of the row.
.channel-refusal
  --action-result-margin: 0
</style>
