<script setup lang="ts">
/**
 * ModerationBans — "Последние баны" (doc 4.2.1.5 / 4.2.2.22).
 * All bans ever issued — active, expired and lifted — newest first,
 * paged (GET v1/bans/history). Ban block composition per doc 4.2.2.22:
 * violator, type, term, reason, "от {Имя}, dd.MM.yyyy HH:mm".
 * Lifting a ban early is SeniorModerator+ (ConfirmDialog-gated).
 */
import { computed, ref, watch } from "vue";
import { useRoute } from "vue-router";
import ModerationApi, { type Ban } from "@/shared/api/moderationApi";
import type { Paging as PagingModel } from "@/shared/api/models/common";
import { Paging } from "@/shared/ui/Paging";
import { ErrorState } from "@/shared/ui/ErrorState";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { formatDateFull } from "@/shared/lib/utils/datetime";
import { useToast } from "@/shared/lib/composables/useToast";
import { useRoleGate } from "./lib/useRoleGate";
import { BAN_TYPE_LABELS, banTermLabel } from "./lib/labels";

const PAGE_SIZE = 20;

const route = useRoute();
const toast = useToast();
const { hasAccess, isSeniorModerator } = useRoleGate("Moderator");

const bans = ref<Ban[]>([]);
const paging = ref<PagingModel | null>(null);
const loading = ref(false);
const loadError = ref<string | null>(null);

const pageNumber = computed(() => {
  const n = parseInt(String(route.query.number ?? "1"), 10);
  return Number.isFinite(n) && n > 0 ? n : 1;
});

async function fetch() {
  loading.value = true;
  const { data, error } = await ModerationApi.getBanHistory({
    skip: (pageNumber.value - 1) * PAGE_SIZE,
    take: PAGE_SIZE,
  });
  loading.value = false;
  if (error) {
    loadError.value = "Не удалось загрузить баны";
    return;
  }
  loadError.value = null;
  bans.value = data?.resources ?? [];
  paging.value = data?.paging ?? null;
}

watch(pageNumber, fetch, { immediate: true });

// Human status of a ban record in the history list
function banStateLabel(ban: Ban): string {
  if (ban.liftedUtc) return `Снят ${formatDateFull(ban.liftedUtc)}`;
  if (ban.isActive) return "Активен";
  return "Истек";
}

// --- Lift ban (SeniorModerator+) ---
const liftTarget = ref<Ban | null>(null);
const lifting = ref(false);

async function confirmLift() {
  if (!liftTarget.value || lifting.value) return;
  lifting.value = true;
  const { error } = await ModerationApi.liftBan(liftTarget.value.id);
  lifting.value = false;
  if (error) {
    toast.error("Не удалось снять бан");
    return;
  }
  toast.success("Бан снят");
  liftTarget.value = null;
  await fetch();
}

const isEmpty = computed(() => !loading.value && bans.value.length === 0);
</script>

<template>
  <div class="moderation-bans">
    <page-title>Последние баны</page-title>

    <SecondaryText v-if="!hasAccess">
      Страница доступна только модераторам
    </SecondaryText>

    <ErrorState v-else-if="loadError" :message="loadError" :retry="fetch" />

    <!-- Skeleton: geometric twin of the ban card (header + reason + meta) -->
    <div
      v-else-if="loading && !bans.length"
      class="ban-list"
      aria-hidden="true"
    >
      <div v-for="i in 5" :key="i" class="ban-card">
        <div class="skeleton-line skeleton-header"></div>
        <div class="skeleton-line skeleton-reason"></div>
        <div class="skeleton-line skeleton-meta"></div>
      </div>
    </div>

    <SecondaryText v-else-if="isEmpty">Банов пока нет</SecondaryText>

    <template v-else>
      <div class="ban-list">
        <div v-for="ban in bans" :key="ban.id" class="ban-card">
          <div class="ban-header">
            <router-link
              v-if="ban.user"
              :to="{ name: 'profile', params: { username: ban.user.username } }"
            >
              {{ ban.user.username }}
            </router-link>
            <span class="ban-type">{{ BAN_TYPE_LABELS[ban.type] }}</span>
            <span class="ban-term">{{ banTermLabel(ban) }}</span>
            <span
              class="ban-state"
              :class="{ 'is-active': ban.isActive && !ban.liftedUtc }"
            >
              {{ banStateLabel(ban) }}
            </span>
          </div>

          <p class="ban-reason">{{ ban.comment }}</p>

          <div class="ban-meta">
            <template v-if="ban.moderator">
              от
              <router-link
                :to="{
                  name: 'profile',
                  params: { username: ban.moderator.username },
                }"
              >
                {{ ban.moderator.username }}</router-link
              >,
            </template>
            {{ formatDateFull(ban.startedUtc) }}
            <button
              v-if="isSeniorModerator && ban.isActive && !ban.liftedUtc"
              type="button"
              class="lift-button"
              @click="liftTarget = ban"
            >
              Снять бан
            </button>
          </div>
        </div>
      </div>

      <Paging
        v-if="paging"
        :paging="paging"
        :to="{ name: 'moderation-bans' }"
        use-query
      />
    </template>

    <ConfirmDialog
      :show="liftTarget !== null"
      title="Снятие бана"
      :message="`Снять бан с пользователя ${liftTarget?.user?.username ?? ''} досрочно?`"
      confirm-label="Снять бан"
      danger
      :loading="lifting"
      @update:show="(v) => !v && (liftTarget = null)"
      @confirm="confirmLift"
    />
  </div>
</template>

<style scoped lang="sass">
@import "@/assets/styles/Inputs"
@import "@/assets/styles/Skeleton"

.ban-list
  display: flex
  flex-direction: column
  gap: $medium
  margin-bottom: $medium

.ban-card
  border: 1px solid $border
  border-radius: $border-radius
  padding: $medium
  background: $bg-element

.ban-header
  display: flex
  align-items: baseline
  flex-wrap: wrap
  gap: $small
  margin-bottom: $small

.ban-type,
.ban-term
  color: $text-muted
  font-size: $secondary-font-size

.ban-state
  margin-left: auto
  color: $text-muted
  font-size: $secondary-font-size

  &.is-active
    color: $accent-red

.ban-reason
  margin: 0 0 $small
  overflow-wrap: anywhere

.ban-meta
  display: flex
  align-items: baseline
  gap: $small
  color: $text-muted
  font-size: $secondary-font-size

.lift-button
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
