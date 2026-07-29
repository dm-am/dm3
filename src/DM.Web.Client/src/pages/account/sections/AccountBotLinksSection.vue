<template>
  <section class="section">
    <h2 class="section-title">Привязка ботов</h2>

    <div class="bot-links-content">
      <!-- Telegram linking -->
      <div class="bot-card">
        <div class="bot-header">
          <span class="bot-name">Telegram</span>
        </div>

        <div v-if="telegramLinking" class="link-process">
          <p class="link-instructions">
            1. Откройте бота:
            <a :href="telegramBotUrl" target="_blank" class="bot-link">
              @{{ telegramBotUsername }}
            </a>
          </p>
          <p class="link-instructions">2. Отправьте команду:</p>
          <div class="code-block">
            <code>/connect {{ telegramCode }}</code>
            <Tooltip
              :text="copied === 'telegram' ? 'Скопировано!' : 'Скопировать'"
            >
              <button
                class="copy-btn"
                @click="copyToClipboard(`/connect ${telegramCode}`)"
                aria-label="Скопировать код"
              >
                {{ copied === "telegram" ? "Скопировано" : "Копировать" }}
              </button>
            </Tooltip>
          </div>
          <div class="timer-section">
            <span class="timer-label">Код действителен:</span>
            <span class="timer-value">{{ formatTime(telegramTimeLeft) }}</span>
            <div class="timer-bar">
              <div
                class="timer-progress"
                :style="{ width: `${(telegramTimeLeft / 900) * 100}%` }"
              />
            </div>
          </div>
          <button class="cancel-btn" @click="cancelTelegramLinking">
            Отмена
          </button>
        </div>

        <div v-else class="bot-action">
          <p class="bot-description">
            Получайте уведомления о новых сообщениях, комментариях и событиях в
            играх.
          </p>
          <button
            class="connect-btn"
            :disabled="generatingCode === 'telegram'"
            @click="startTelegramLinking"
          >
            {{ generatingCode === "telegram" ? "..." : "Подключить Telegram" }}
          </button>
        </div>
      </div>

      <!-- Discord linking -->
      <div class="bot-card">
        <div class="bot-header">
          <span class="bot-name">Discord</span>
        </div>

        <div v-if="discordLinking" class="link-process">
          <p class="link-instructions">
            1. Откройте бота:
            <a :href="discordBotUrl" target="_blank" class="bot-link">
              Dungeon Master Bot
            </a>
          </p>
          <p class="link-instructions">2. Отправьте команду:</p>
          <div class="code-block">
            <code>/connect {{ discordCode }}</code>
            <Tooltip
              :text="copied === 'discord' ? 'Скопировано!' : 'Скопировать'"
            >
              <button
                class="copy-btn"
                @click="copyToClipboard(`/connect ${discordCode}`)"
                aria-label="Скопировать код"
              >
                {{ copied === "discord" ? "Скопировано" : "Копировать" }}
              </button>
            </Tooltip>
          </div>
          <div class="timer-section">
            <span class="timer-label">Код действителен:</span>
            <span class="timer-value">{{ formatTime(discordTimeLeft) }}</span>
            <div class="timer-bar">
              <div
                class="timer-progress"
                :style="{ width: `${(discordTimeLeft / 900) * 100}%` }"
              />
            </div>
          </div>
          <button class="cancel-btn" @click="cancelDiscordLinking">
            Отмена
          </button>
        </div>

        <div v-else class="bot-action">
          <p class="bot-description">
            Получайте уведомления прямо в Discord через личные сообщения.
          </p>
          <button
            class="connect-btn"
            :disabled="generatingCode === 'discord'"
            @click="startDiscordLinking"
          >
            {{ generatingCode === "discord" ? "..." : "Подключить Discord" }}
          </button>
        </div>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { ref, onUnmounted } from "vue";
import { accountApi } from "@/shared/api";
import { Tooltip } from "@/shared/ui/Tooltip";
import { useToast } from "@/shared/lib/composables/useToast";

// Bot configuration (should match backend config)
const telegramBotUsername = "DM3NotifyBot";
const telegramBotUrl = `https://t.me/${telegramBotUsername}`;
const discordBotUrl = "https://discord.com/"; // Will be updated with actual bot invite

const toast = useToast();

// Telegram state
const telegramLinking = ref(false);
const telegramCode = ref("");
const telegramTimeLeft = ref(0);
let telegramTimer: ReturnType<typeof setInterval> | null = null;

// Discord state
const discordLinking = ref(false);
const discordCode = ref("");
const discordTimeLeft = ref(0);
let discordTimer: ReturnType<typeof setInterval> | null = null;

// UI state
const generatingCode = ref<"telegram" | "discord" | null>(null);
const copied = ref<"telegram" | "discord" | null>(null);

async function startTelegramLinking() {
  generatingCode.value = "telegram";
  const { data, error } = await accountApi.generateBotCode("telegram");
  generatingCode.value = null;

  if (error) {
    toast.error("Не удалось сгенерировать код");
    return;
  }

  if (data) {
    telegramCode.value = data.code;
    const expiresAt = new Date(data.expiresUtc).getTime();
    telegramTimeLeft.value = Math.max(
      0,
      Math.floor((expiresAt - Date.now()) / 1000),
    );
    telegramLinking.value = true;

    // Start countdown
    telegramTimer = setInterval(() => {
      telegramTimeLeft.value--;
      if (telegramTimeLeft.value <= 0) {
        cancelTelegramLinking();
        toast.warning("Код истек. Сгенерируйте новый.");
      }
    }, 1000);
  }
}

function cancelTelegramLinking() {
  telegramLinking.value = false;
  telegramCode.value = "";
  telegramTimeLeft.value = 0;
  if (telegramTimer) {
    clearInterval(telegramTimer);
    telegramTimer = null;
  }
}

async function startDiscordLinking() {
  generatingCode.value = "discord";
  const { data, error } = await accountApi.generateBotCode("discord");
  generatingCode.value = null;

  if (error) {
    toast.error("Не удалось сгенерировать код");
    return;
  }

  if (data) {
    discordCode.value = data.code;
    const expiresAt = new Date(data.expiresUtc).getTime();
    discordTimeLeft.value = Math.max(
      0,
      Math.floor((expiresAt - Date.now()) / 1000),
    );
    discordLinking.value = true;

    // Start countdown
    discordTimer = setInterval(() => {
      discordTimeLeft.value--;
      if (discordTimeLeft.value <= 0) {
        cancelDiscordLinking();
        toast.warning("Код истек. Сгенерируйте новый.");
      }
    }, 1000);
  }
}

function cancelDiscordLinking() {
  discordLinking.value = false;
  discordCode.value = "";
  discordTimeLeft.value = 0;
  if (discordTimer) {
    clearInterval(discordTimer);
    discordTimer = null;
  }
}

function formatTime(seconds: number): string {
  const mins = Math.floor(seconds / 60);
  const secs = seconds % 60;
  return `${mins}:${secs.toString().padStart(2, "0")}`;
}

async function copyToClipboard(text: string) {
  try {
    await navigator.clipboard.writeText(text);
    if (text.includes(telegramCode.value)) {
      copied.value = "telegram";
    } else {
      copied.value = "discord";
    }
    setTimeout(() => {
      copied.value = null;
    }, 2000);
  } catch {
    toast.error("Не удалось скопировать");
  }
}

// Cleanup on unmount
onUnmounted(() => {
  if (telegramTimer) clearInterval(telegramTimer);
  if (discordTimer) clearInterval(discordTimer);
});
</script>

<style scoped lang="sass">
@import "@/assets/styles/Inputs"
@import "../AccountPage.styles"

.bot-links-content
  padding: $medium
  background-color: $bg-element
  border-radius: $border-radius
  display: flex
  flex-direction: column
  gap: $medium

.bot-card
  padding: $medium
  background-color: $bg
  border-radius: $border-radius
  border: 1px solid $border

.bot-header
  display: flex
  align-items: center
  gap: $small
  margin-bottom: $small

.bot-icon
  font-size: 1.3em

.bot-name
  font-weight: 600
  color: $text

.bot-description
  margin: 0 0 $small
  font-size: $secondary-font-size
  color: $text-muted

.bot-action
  display: flex
  flex-direction: column

.connect-btn
  align-self: flex-start
  +button

// Link process
.link-process
  display: flex
  flex-direction: column
  gap: $small

.link-instructions
  margin: 0
  font-size: $secondary-font-size
  color: $text

.bot-link
  color: $link
  text-decoration: none

  &:hover
    text-decoration: underline

.code-block
  display: flex
  align-items: center
  gap: $small
  padding: $small $medium
  background-color: $bg-element
  border-radius: $border-radius
  font-family: monospace

  code
    flex: 1
    font-size: 1.1em
    color: $text
    user-select: all

.copy-btn
  +button

.timer-section
  display: flex
  flex-wrap: wrap
  align-items: center
  gap: $small
  margin-top: $small

.timer-label
  font-size: $secondary-font-size
  color: $text-muted

.timer-value
  font-weight: 500
  color: $text

.timer-bar
  width: 100%
  height: 4px
  background-color: $border
  border-radius: 2px
  overflow: hidden
  margin-top: $tiny

.timer-progress
  height: 100%
  background-color: $link
  transition: width 1s linear

.cancel-btn
  align-self: flex-start
  margin-top: $small
  +button
</style>
