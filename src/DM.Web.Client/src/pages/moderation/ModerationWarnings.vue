<script setup lang="ts">
/**
 * ModerationWarnings — "Последние предупреждения" (doc 4.2.1.5 / 4.2.2.21).
 * All warnings across the website, newest first (GET v1/moderation/warnings).
 * Warning block composition per doc 4.2.2.21: type ("Предупреждение
 * (N баллов)" / "Устное предупреждение (0 баллов)"), reason,
 * "от {Имя}, dd.MM.yyyy HH:mm", delete button (Moderator+).
 */
import { computed, onMounted, ref } from "vue";
import { moderationApi, type Warning } from "@/entities/moderation";
import { ErrorState } from "@/shared/ui/ErrorState";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { formatDateFull } from "@/shared/lib/utils/datetime";
import { useToast } from "@/shared/lib/composables/useToast";
import { UserLink } from "@/entities/user";
import { useRoleGate } from "./lib/useRoleGate";
import { warningTypeLabel } from "./lib/labels";
import { notifyFailure } from "@/shared/lib/errors";

const toast = useToast();
const { hasAccess, deniedText } = useRoleGate("Moderator");

const warnings = ref<Warning[]>([]);
const loading = ref(false);
const loadError = ref<string | null>(null);

async function fetch() {
  loading.value = true;
  const { data, error } = await moderationApi.getAllWarnings();
  loading.value = false;
  if (error) {
    loadError.value = "Не удалось загрузить предупреждения";
    return;
  }
  loadError.value = null;
  // Newest first (doc: "в обратном хронологическом порядке")
  warnings.value = (data?.resources ?? []).sort(
    (a, b) =>
      new Date(b.createdUtc).getTime() - new Date(a.createdUtc).getTime(),
  );
}

onMounted(fetch);

// --- Remove warning (Moderator+) ---
const removeTarget = ref<Warning | null>(null);
const removing = ref(false);

async function confirmRemove() {
  if (!removeTarget.value || removing.value) return;
  removing.value = true;
  const { error } = await moderationApi.removeWarning(removeTarget.value.id);
  removing.value = false;
  if (error) {
    notifyFailure(error, "Не удалось удалить предупреждение");
    return;
  }
  toast.success("Предупреждение удалено");
  removeTarget.value = null;
  await fetch();
}

const isEmpty = computed(() => !loading.value && warnings.value.length === 0);
</script>

<template>
  <div class="moderation-warnings">
    <page-title>Последние предупреждения</page-title>

    <SecondaryText v-if="!hasAccess">{{ deniedText }}</SecondaryText>

    <ErrorState v-else-if="loadError" :message="loadError" :retry="fetch" />

    <!-- Skeleton: geometric twin of the warning card (header + reason + meta) -->
    <div
      v-else-if="loading && !warnings.length"
      class="warning-list"
      aria-hidden="true"
    >
      <div v-for="i in 5" :key="i" class="warning-card">
        <div class="skeleton-line skeleton-header"></div>
        <div class="skeleton-line skeleton-reason"></div>
        <div class="skeleton-line skeleton-meta"></div>
      </div>
    </div>

    <SecondaryText v-else-if="isEmpty">Предупреждений пока нет</SecondaryText>

    <div v-else class="warning-list">
      <div
        v-for="warning in warnings"
        :key="warning.id"
        class="warning-card"
        :class="{ 'is-inactive': !warning.isActive }"
      >
        <div class="warning-header">
          <UserLink v-if="warning.user" :user="warning.user" />
          <span class="warning-type">{{
            warningTypeLabel(warning.points)
          }}</span>
          <span v-if="!warning.isActive" class="warning-state">Неактивно</span>
        </div>

        <p class="warning-reason">{{ warning.reason }}</p>

        <div class="warning-meta">
          <template v-if="warning.moderator">
            от
            <router-link
              :to="{
                name: 'profile',
                params: { username: warning.moderator.username },
              }"
            >
              {{ warning.moderator.username }}</router-link
            >,
          </template>
          {{ formatDateFull(warning.createdUtc) }}
          <button
            v-if="warning.isActive"
            type="button"
            class="remove-button"
            @click="removeTarget = warning"
          >
            Удалить
          </button>
        </div>
      </div>
    </div>

    <ConfirmDialog
      :show="removeTarget !== null"
      title="Удаление предупреждения"
      :message="`Удалить предупреждение пользователя ${removeTarget?.user?.username ?? ''}? Баллы будут пересчитаны.`"
      confirm-label="Удалить"
      danger
      :loading="removing"
      @update:show="(v) => !v && (removeTarget = null)"
      @confirm="confirmRemove"
    />
  </div>
</template>

<style scoped lang="sass">
@import "@/assets/styles/Inputs"
@import "@/assets/styles/Skeleton"

.warning-list
  display: flex
  flex-direction: column
  gap: $medium

.warning-card
  +card()

  &.is-inactive
    opacity: 0.6

.warning-header
  display: flex
  align-items: baseline
  flex-wrap: wrap
  gap: $small
  margin-bottom: $small

.warning-type
  color: $text-muted
  font-size: $secondary-font-size

.warning-state
  margin-left: auto
  color: $text-muted
  font-size: $secondary-font-size

.warning-reason
  margin: 0 0 $small
  overflow-wrap: anywhere

.warning-meta
  display: flex
  align-items: baseline
  gap: $small
  color: $text-muted
  font-size: $secondary-font-size

.remove-button
  margin-left: auto
  +inline-link-button

// --- Skeleton lines (match the three content lines of a card) ---
.skeleton-line
  height: 1em
  margin-bottom: $small
  +skeleton-shimmer

  &:last-child
    margin-bottom: 0

.skeleton-header
  width: 60%

.skeleton-reason
  width: 90%

.skeleton-meta
  width: 40%
</style>
