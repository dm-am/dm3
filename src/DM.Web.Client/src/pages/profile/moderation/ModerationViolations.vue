<script setup lang="ts">
import { ref } from "vue";
import type {
  ViolationSummary,
  ModerationPermissions,
} from "@/shared/api/models/moderation";
import type { Username } from "@/shared/api/models/community";
import type {
  Warning,
  Ban,
  CreateWarning,
  CreateBan,
} from "@/shared/api/moderationApi";
import { BanType } from "@/shared/api/moderationApi";
import { moderationApi } from "@/shared/api";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import TheButton from "@/shared/ui/Button/TheButton.vue";
import { useToast } from "@/shared/lib/composables/useToast";
import dayjs from "dayjs";

const props = defineProps<{
  violations: ViolationSummary;
  permissions: ModerationPermissions;
  targetUsername: string;
}>();

const emit = defineEmits<{
  (e: "updated"): void;
}>();

const toast = useToast();

// Expandable lists
const showWarnings = ref(false);
const showBans = ref(false);
const warningsList = ref<Warning[] | null>(null);
const bansList = ref<Ban[] | null>(null);
const loadingWarnings = ref(false);
const loadingBans = ref(false);

// Inline forms
const showWarningForm = ref(false);
const showBanForm = ref(false);

// Warning form fields
const warningPoints = ref(1);
const warningReason = ref("");
const issuingWarning = ref(false);

// Ban form fields
const banType = ref<BanType>(BanType.Temporary);
const banDurationHours = ref(24);
const banComment = ref("");
const issuingBan = ref(false);

const banDurationOptions = [
  { label: "1 час", hours: 1 },
  { label: "1 день", hours: 24 },
  { label: "3 дня", hours: 72 },
  { label: "7 дней", hours: 168 },
  { label: "30 дней", hours: 720 },
];

function formatDate(dateStr: string): string {
  return dayjs(dateStr).format("DD.MM.YYYY");
}

function formatDateTime(dateStr: string): string {
  return dayjs(dateStr).format("DD.MM.YYYY HH:mm");
}

async function toggleWarnings() {
  showWarnings.value = !showWarnings.value;
  if (showWarnings.value && warningsList.value === null) {
    loadingWarnings.value = true;
    const { data } = await moderationApi.getWarnings(props.targetUsername as Username);
    warningsList.value = data?.warnings ?? [];
    loadingWarnings.value = false;
  }
}

async function toggleBans() {
  showBans.value = !showBans.value;
  if (showBans.value && bansList.value === null) {
    loadingBans.value = true;
    const { data } = await moderationApi.getBans(props.targetUsername as Username);
    bansList.value = data?.history ?? [];
    loadingBans.value = false;
  }
}

async function issueWarning() {
  if (!warningReason.value.trim()) return;
  if (!confirm(`Вынести предупреждение (${warningPoints.value} б.) пользователю ${props.targetUsername}?`)) return;
  issuingWarning.value = true;
  const payload: CreateWarning = {
    username: props.targetUsername,
    points: warningPoints.value,
    reason: warningReason.value.trim(),
  };
  const { error } = await moderationApi.createWarning(payload);
  issuingWarning.value = false;
  if (error) {
    toast.error("Не удалось вынести предупреждение");
    return;
  }
  toast.success("Предупреждение вынесено");
  warningReason.value = "";
  warningPoints.value = 1;
  showWarningForm.value = false;
  warningsList.value = null;
  emit("updated");
}

async function removeWarning(warningId: string) {
  if (!confirm("Снять предупреждение?")) return;
  const { error } = await moderationApi.removeWarning(warningId);
  if (error) {
    toast.error("Не удалось снять предупреждение");
    return;
  }
  warningsList.value = null;
  emit("updated");
}

async function issueBan() {
  if (!banComment.value.trim()) return;
  const banTypeStr = banType.value === BanType.Permanent ? "перманентный" : `на ${banDurationHours.value} ч.`;
  if (!confirm(`Забанить ${props.targetUsername} (${banTypeStr})?`)) return;
  issuingBan.value = true;
  const payload: CreateBan = {
    username: props.targetUsername,
    type: banType.value,
    comment: banComment.value.trim(),
  };
  if (banType.value === BanType.Temporary) {
    payload.durationHours = banDurationHours.value;
  }
  const { error } = await moderationApi.createBan(payload);
  issuingBan.value = false;
  if (error) {
    toast.error("Не удалось забанить пользователя");
    return;
  }
  toast.success("Бан выдан");
  banComment.value = "";
  banType.value = BanType.Temporary;
  banDurationHours.value = 24;
  showBanForm.value = false;
  bansList.value = null;
  emit("updated");
}

async function liftBan(banId: string) {
  if (!confirm("Снять бан?")) return;
  const { error } = await moderationApi.liftBan(banId);
  if (error) {
    toast.error("Не удалось снять бан");
    return;
  }
  bansList.value = null;
  emit("updated");
}
</script>

<template>
  <div class="mod-section">
    <h4 class="mod-section_title">Нарушения</h4>

    <!-- Warnings summary + expandable list -->
    <div class="mod-violations_group">
      <div class="mod-violations_summary" @click="toggleWarnings">
        <span>
          Предупреждения: <strong>{{ violations.totalWarnings }}</strong>
        </span>
        <span
          v-if="violations.activeWarningPoints > 0"
          class="mod-warning-points"
        >
          ({{ violations.activeWarningPoints }} активных баллов)
        </span>
        <span class="mod-expand-icon">{{ showWarnings ? "▼" : "▶" }}</span>
      </div>

      <div v-if="showWarnings" class="mod-violations_list">
        <secondary-text v-if="loadingWarnings">Загрузка...</secondary-text>
        <template v-else-if="warningsList">
          <secondary-text v-if="warningsList.length === 0">
            Нет предупреждений
          </secondary-text>
          <div
            v-for="w in warningsList"
            :key="w.id"
            class="mod-violation-item"
            :class="{ 'mod-violation-active': w.isActive }"
          >
            <div class="mod-violation-item_header">
              <secondary-text>{{ formatDateTime(w.createdUtc) }}</secondary-text>
              <span v-if="w.moderator">{{ w.moderator.username }}</span>
              <span class="mod-warning-badge">{{ w.points }} б.</span>
            </div>
            <div class="mod-violation-item_text">{{ w.reason }}</div>
            <a
              v-if="permissions.canIssueWarning && w.isActive"
              class="mod-action mod-action-danger"
              @click="removeWarning(w.id)"
            >
              Снять
            </a>
          </div>
        </template>
      </div>
    </div>

    <!-- Bans summary + expandable list -->
    <div class="mod-violations_group">
      <div class="mod-violations_summary" @click="toggleBans">
        <span>
          Баны: <strong>{{ violations.totalBans }}</strong>
        </span>
        <span v-if="violations.isCurrentlyBanned" class="mod-active-ban">
          (активный бан{{
            violations.currentBanEndUtc
              ? " до " + formatDate(violations.currentBanEndUtc)
              : " — перманентный"
          }})
        </span>
        <span class="mod-expand-icon">{{ showBans ? "▼" : "▶" }}</span>
      </div>

      <div v-if="showBans" class="mod-violations_list">
        <secondary-text v-if="loadingBans">Загрузка...</secondary-text>
        <template v-else-if="bansList">
          <secondary-text v-if="bansList.length === 0">
            Нет банов
          </secondary-text>
          <div
            v-for="b in bansList"
            :key="b.id"
            class="mod-violation-item"
            :class="{ 'mod-violation-active': b.isActive }"
          >
            <div class="mod-violation-item_header">
              <secondary-text>{{ formatDateTime(b.startedUtc) }}</secondary-text>
              <span v-if="b.moderator">{{ b.moderator.username }}</span>
              <span class="mod-ban-type">{{ b.type }}</span>
              <span v-if="b.expiresUtc">
                до {{ formatDateTime(b.expiresUtc) }}
              </span>
            </div>
            <div class="mod-violation-item_text">{{ b.comment }}</div>
            <a
              v-if="permissions.canLiftBan && b.isActive"
              class="mod-action mod-action-danger"
              @click="liftBan(b.id)"
            >
              Снять бан
            </a>
          </div>
        </template>
      </div>
    </div>

    <!-- Action buttons -->
    <div class="mod-violations_actions">
      <the-button
        v-if="permissions.canIssueWarning"
        @click="showWarningForm = !showWarningForm"
      >
        {{ showWarningForm ? "Отмена" : "Вынести предупреждение" }}
      </the-button>

      <the-button
        v-if="permissions.canIssueBan"
        @click="showBanForm = !showBanForm"
      >
        {{ showBanForm ? "Отмена" : "Забанить" }}
      </the-button>
    </div>

    <!-- Inline Warning Form -->
    <div v-if="showWarningForm" class="mod-inline-form">
      <h5 class="mod-inline-form_title">Вынести предупреждение</h5>
      <div class="mod-form-field">
        <label class="mod-form-label">Баллы</label>
        <select v-model="warningPoints" class="mod-select">
          <option :value="1">1 — Мелкое нарушение</option>
          <option :value="2">2 — Нарушение правил</option>
          <option :value="3">3 — Серьезное нарушение</option>
        </select>
      </div>
      <div class="mod-form-field">
        <label class="mod-form-label">Причина</label>
        <textarea
          v-model="warningReason"
          rows="3"
          placeholder="Причина предупреждения..."
          class="mod-textarea"
        />
      </div>
      <the-button
        :disabled="!warningReason.trim()"
        :loading="issuingWarning"
        @click="issueWarning"
      >
        Вынести предупреждение
      </the-button>
    </div>

    <!-- Inline Ban Form -->
    <div v-if="showBanForm" class="mod-inline-form">
      <h5 class="mod-inline-form_title">Забанить пользователя</h5>
      <div class="mod-form-field">
        <label class="mod-form-label">Тип бана</label>
        <select v-model="banType" class="mod-select">
          <option :value="BanType.Temporary">Временный</option>
          <option :value="BanType.Permanent">Перманентный</option>
        </select>
      </div>
      <div v-if="banType === BanType.Temporary" class="mod-form-field">
        <label class="mod-form-label">Длительность</label>
        <select v-model="banDurationHours" class="mod-select">
          <option
            v-for="opt in banDurationOptions"
            :key="opt.hours"
            :value="opt.hours"
          >
            {{ opt.label }}
          </option>
        </select>
      </div>
      <div class="mod-form-field">
        <label class="mod-form-label">Причина</label>
        <textarea
          v-model="banComment"
          rows="3"
          placeholder="Причина бана..."
          class="mod-textarea"
        />
      </div>
      <the-button
        :disabled="!banComment.trim()"
        :loading="issuingBan"
        @click="issueBan"
      >
        Забанить
      </the-button>
    </div>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"
@import "src/assets/styles/Inputs"

.mod-section
  margin-bottom: $medium

.mod-section_title
  margin: 0 0 $small 0
  color: $heading

.mod-violations_group
  margin-bottom: $small

.mod-violations_summary
  cursor: pointer
  user-select: none
  padding: $minor 0
  display: flex
  align-items: baseline
  gap: $small
  &:hover
    color: $link-hover

.mod-expand-icon
  font-size: $tertiary-font-size

.mod-warning-points
  color: $accent-red
  font-size: $secondary-font-size

.mod-active-ban
  color: $accent-red
  font-size: $secondary-font-size
  font-weight: bold

.mod-violations_list
  padding-left: $medium
  margin-bottom: $small

.mod-violation-item
  padding: $small
  border-bottom: 1px solid $bg-element-accent
  &:last-child
    border-bottom: none

.mod-violation-active
  background: $bg-highlight-yellow

.mod-violation-item_header
  display: flex
  align-items: baseline
  gap: $small
  margin-bottom: $minor

.mod-violation-item_text
  white-space: pre-wrap
  word-break: break-word

.mod-warning-badge
  font-size: $secondary-font-size
  font-weight: bold
  color: $accent-red

.mod-ban-type
  font-size: $secondary-font-size
  font-weight: bold
  color: $accent-red

.mod-action
  cursor: pointer
  font-size: $secondary-font-size
  color: $link
  margin-top: $minor
  display: inline-block
  &:hover
    color: $link-hover
    text-decoration: underline

.mod-action-danger
  color: $accent-red
  &:hover
    color: $accent-red-hover

.mod-violations_actions
  display: flex
  gap: $small
  margin-top: $medium

.mod-inline-form
  margin-top: $medium
  padding: $medium
  background: $bg-element-hover
  border-radius: $border-radius

.mod-inline-form_title
  margin: 0 0 $small 0
  color: $heading

.mod-form-field
  margin-bottom: $small

.mod-form-label
  display: block
  font-size: $secondary-font-size
  color: $text-muted
  margin-bottom: $minor

.mod-textarea
  width: 100%
  resize: vertical
  box-sizing: border-box
  +input()

.mod-select
  width: 100%
  box-sizing: border-box
  +input()
</style>
