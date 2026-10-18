<template>
  <section id="notifications" ref="root" class="section">
    <!-- The notifications page links here: "/account#notifications". -->
    <h2 class="section-title">Уведомления</h2>

    <!-- One grey panel, as "Сессии" above it is, and in it a framed row a
         channel: its name with its state under it on the left, the one press
         it offers on the right, and under them what the channel brings or
         the steps that link it. -->
    <div class="notifications-content">
      <div v-if="loading" class="loading-state">Загрузка...</div>

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

          <!-- What a linked channel brings, under it: the switch of the whole
               channel, and the column of what it sends under a caption of its
               own, as "Что делать с внесенными" of the blacklist stands. -->
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

          <!-- The steps of linking, under the row they link. Focus comes here
               when they open in place of the button that opened them, so the
               reader of the screen hears them. A bot that takes the code in
               its start link is opened by that link, a plain one whose words
               are the address; the other is sent the command, text of the
               line, whole and selectable. Either is copied as any other words
               of the page. -->
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
import { accountApi } from "@/entities/user";
import type {
  NotificationPreferences,
  NotificationCategory,
} from "@/shared/api/models/account";
import { useActionResult } from "@/shared/lib/composables/useActionResult";
import { ActionResult } from "@/shared/ui/ActionResult";
import Button from "@/shared/ui/Button/Button.vue";

type ChannelId = "telegram" | "discord";

/**
 * The two channels, in the order of the rows. Everything the rows do is the
 * same for both, so a row is drawn from one of these and the state below is
 * kept per channel rather than written out twice.
 *
 * The bots are named the way the server is configured (should match backend
 * config); the Discord address is a placeholder until the bot has an invite.
 *
 * `startsWithCode`: the bot takes the code in its start link,
 * t.me/<bot>?start=<code>, which Telegram hands it as "/start <code>" when the
 * reader presses Start in the chat it opens, so the steps give that link in
 * place of the command.
 */
const CHANNELS: readonly {
  id: ChannelId;
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
const saving = ref(false);
const disconnecting = ref<ChannelId | null>(null);
const generating = ref<ChannelId | null>(null);

const preferences = reactive<NotificationPreferences>({
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

// Local state of the boxes, per channel.
const enabled = reactive<Record<ChannelId, boolean>>({
  telegram: false,
  discord: false,
});
const categories = reactive<Record<ChannelId, NotificationCategory[]>>({
  telegram: [],
  discord: [],
});

const connected = (id: ChannelId) => preferences[id]?.connected === true;

// The caption that names the column of a channel's categories.
const baseId = useId();
const categoriesId = (id: ChannelId) => `${baseId}-${id}-categories`;

onMounted(async () => {
  await loadPreferences();
});

/** Takes one channel's preferences as the server holds them, boxes included. */
function takeChannel(id: ChannelId, data: NotificationPreferences) {
  preferences[id] = data[id];
  enabled[id] = data[id]?.enabled ?? false;
  categories[id] = [...(data[id]?.enabledCategories ?? [])];
}

async function loadPreferences() {
  loading.value = true;
  const { data, error } = await accountApi.getNotificationPreferences();
  loading.value = false;

  if (!error && data) {
    for (const { id } of CHANNELS) takeChannel(id, data);
  }
}

// A setting stays drawn as it is while its save is in flight. Disabled, a
// ticked box would show a tick on a control that cannot be ticked, and the
// site's disabled state says "not on offer", which a setting being saved is
// not. What the disabled attribute also did is kept: a press during the save
// is refused before it toggles anything, so it never becomes a second request
// racing the first.
function holdWhileSaving(event: Event) {
  if (saving.value) event.preventDefault();
}

// A refused press, at the foot of the row it was made in: the box that was
// ticked goes back, and the line says why. The two rows stand one under the
// other and each answers for its own press.
const results: Record<ChannelId, ReturnType<typeof useActionResult>> = {
  telegram: useActionResult(),
  discord: useActionResult(),
};

async function updateEnabled(id: ChannelId) {
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

async function toggleCategory(id: ChannelId, category: NotificationCategory) {
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
    // Revert
    if (idx >= 0) {
      list.push(category);
    } else {
      list.splice(list.indexOf(category), 1);
    }
  }
}

async function disconnect(id: ChannelId) {
  disconnecting.value = id;
  results[id].clear();
  const { error } = await accountApi.disconnectBot(id);
  disconnecting.value = null;

  if (error) {
    const name = CHANNELS.find((channel) => channel.id === id)?.name;
    results[id].refuse(error, `Не удалось отключить ${name}`);
  } else {
    // No toast: the row redraws itself - "Не подключен" under the name, and
    // the button that links it in place of the one that was pressed.
    preferences[id] = undefined;
    void focusIn(id, ".connect-btn");
  }
}

// ---- Linking

/** A code handed out for one channel, and the time it has left. */
interface Linking {
  code: string;
  secondsLeft: number;
}

const linking = reactive<Record<ChannelId, Linking | null>>({
  telegram: null,
  discord: null,
});
const timers: Partial<Record<ChannelId, ReturnType<typeof setInterval>>> = {};

async function startLinking(id: ChannelId) {
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

  // Running out says it itself: the code, the timer and "Отмена" go, and the
  // row comes back with "Подключить" on it.
  stopTimer(id);
  timers[id] = setInterval(() => {
    const open = linking[id];
    if (!open) return stopTimer(id);
    open.secondsLeft--;
    if (open.secondsLeft <= 0) stopLinking(id);
  }, 1000);
}

function stopTimer(id: ChannelId) {
  const timer = timers[id];
  if (timer) clearInterval(timer);
  delete timers[id];
}

/**
 * Where focus goes when a press takes away the control it was on: the steps
 * open under the row when "Подключить" gives way to "Отмена", and
 * "Подключить" comes back in place of "Отмена" and of "Отключить". Left
 * alone, it falls to the page and the next Tab starts from the top.
 */
const root = ref<HTMLElement | null>(null);

async function focusIn(id: ChannelId, selector: string) {
  await nextTick();
  root.value
    ?.querySelector<HTMLElement>(`[data-channel="${id}"] ${selector}`)
    ?.focus();
}

/** "Отмена": the steps go, and the press that opens them again takes focus. */
function cancelLinking(id: ChannelId) {
  stopLinking(id);
  void focusIn(id, ".connect-btn");
}

function stopLinking(id: ChannelId) {
  linking[id] = null;
  stopTimer(id);
}

/** The time a code has left, as "13:58". */
function timeLeft(id: ChannelId): string {
  const seconds = linking[id]?.secondsLeft ?? 0;
  const mins = Math.floor(seconds / 60);
  const secs = seconds % 60;
  return `${mins}:${secs.toString().padStart(2, "0")}`;
}

/**
 * The command is sent from the bot, away from this page, and nothing tells the
 * page it went through. So while a row shows its steps, coming back to the
 * page reads the channels again, and a row whose channel is linked now trades
 * the steps for its settings. Without it the row went on offering the command
 * of a channel already linked, and then "Подключить" again once the code ran
 * out.
 */
let checking = false;

async function checkLinked() {
  if (checking || document.visibilityState === "hidden") return;
  const open = CHANNELS.filter(({ id }) => linking[id]);
  if (!open.length) return;

  checking = true;
  const { data, error } = await accountApi.getNotificationPreferences();
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

// ---- Arriving at #notifications

/**
 * The notifications page links here, "/account#notifications", and the router
 * scrolls to the section as soon as the page is drawn, while the blocks above
 * it still say "Загрузка...". This is the last section of the page, so that
 * scroll stops at the foot of a page that is still short, and the blocks above
 * then grow and push the section down past the window: at 1366 the link
 * landed on "Смена имени пользователя". So while the address names it, the
 * section brings itself back into view each time the page it stands in grows,
 * until the reader takes the page himself or the page has had time to settle.
 */
const READER_INPUTS = ["wheel", "touchstart", "keydown", "pointerdown"];
const SETTLE_MS = 5000;

let letPageGo: (() => void) | null = null;

function keepInViewWhileThePageLoads() {
  const section = root.value;
  const page = section?.parentElement;
  if (window.location.hash !== "#notifications" || !section || !page) return;
  if (typeof ResizeObserver === "undefined") return;

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
  // An observer reports the size it starts from, so this is the first scroll.
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

// The grey panel of the section, as "Сессии" has it.
.notifications-content
  padding: $medium
  background-color: $bg-element

.loading-state
  padding: $medium

.channels
  display: flex
  flex-direction: column
  gap: $small

// A framed row of the panel, as a row of "Сессии" is: the head of the row,
// and under it what the channel brings or the steps that link it.
.channel-item
  display: flex
  flex-direction: column
  gap: $small
  padding: $small $medium
  background-color: $bg-element
  border: 1px solid $border

// The name and its state on the left, the one press level with them on the
// right, at every width: a phone shows the row as the full version does.
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

// The main word of the row, as the device of a session is.
.channel-name
  margin: 0
  font-size: $font-size
  font-weight: 500
  color: $text

// The line under the name, as the line under the device of a session is.
.channel-state
  margin: 0
  font-size: $secondary-font-size
  color: $text-muted

.channel-settings
  display: flex
  flex-direction: column
  gap: $small

// A box and its words, as a setting of the blacklist above is.
.setting-item
  display: flex
  align-items: center
  gap: $small
  cursor: pointer
  color: $text

// The caption of a group of boxes: 14, bold, as "Что делать с внесенными" of
// the blacklist and the captions of the fields of the site are.
.categories-caption
  margin: $small 0 0
  +field-caption

.categories-list
  display: flex
  flex-direction: column
  gap: $small

// Linking
.link-process
  display: flex
  flex-direction: column
  gap: $small

.link-step
  margin: 0
  color: $text

// The command as words of the line, in the code font of the site. A tap
// selects all of it, and it is never broken in two.
.command
  font-family: $code-font
  font-size: $font-size
  white-space: nowrap
  user-select: all

// The start link stands in for the command, and like it is never broken in
// two: an address split at its "?" reads as two.
.start-link
  white-space: nowrap

.timer
  margin: 0
  font-size: $secondary-font-size
  color: $text-muted

.timer-value
  font-size: $font-size
  font-weight: 500
  color: $text

// Spaced by the gap of the row, at its left edge.
.channel-refusal
  --action-result-margin: 0
</style>
