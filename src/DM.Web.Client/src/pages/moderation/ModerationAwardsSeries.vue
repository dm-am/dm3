<script setup lang="ts">
import { formatDate } from "@/shared/lib/utils/datetime";
/**
 * ModerationAwardsSeries — contest series details: an edit form (topic
 * URL / IsActive), a grant form (award a user within this series) and the
 * list of previously granted awards with a revoke action.
 *
 * Series identity — id (uuid), taken from the `id` route param.
 */
import { computed, onMounted, ref, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import type {
  ContestSeries,
  ContestSeriesAward,
} from "@/shared/api/models/achievements";
import {
  achievementApi,
  awardMetal,
  metalStyle,
  useContestSeries,
  formatContestSeriesTitle,
} from "@/entities/achievement";
import { BlockTitle, SecondaryText } from "@/shared/ui/Layout";
import { UserAutocomplete } from "@/entities/user";
import { GameIcon } from "@/shared/ui/Icon";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { UserLink } from "@/entities/user";
import { Select, type SelectOption } from "@/shared/ui/Select";
import { refusalHere } from "@/shared/lib/errors";
import { useRoleGate } from "./lib/useRoleGate";
import { sectionRole } from "./lib/sections";

// A sub-page of the awards tab, so it wears that tab's row.
const { hasAccess, deniedText } = useRoleGate(sectionRole("moderation-awards"));
const route = useRoute();
const router = useRouter();
const {
  adminSeries: series,
  awardTypes,
  load: loadPublic,
  loadAdmin,
  reload,
} = useContestSeries();

// The page needs both slices: the series itself from the admin catalog (a
// hidden series must still open to be restored), the grant dropdown from
// the public one (a hidden award type must not be offered for granting).
const load = () => Promise.all([loadPublic(), loadAdmin()]);

const seriesId = computed(() => String(route.params.id));
const current = computed<ContestSeries | null>(
  () => series.value?.find((s) => s.id === seriesId.value) ?? null,
);

// The award types a grant may carry, in the words the dropdown shows. The
// public slice, so a hidden type is not offered.
const awardTypeOptions = computed<SelectOption[]>(() =>
  (awardTypes.value ?? []).map((t) => ({
    value: t.id,
    label: `${t.title} (${t.description})`,
  })),
);

// --- Edit form ---
const edit = ref({ topicUrl: "", isActive: true });
const editSaving = ref(false);

watch(
  current,
  (c) => {
    if (c) {
      edit.value = { topicUrl: c.topicUrl ?? "", isActive: c.isActive };
    }
  },
  { immediate: true },
);

async function saveEdit() {
  if (!current.value || editSaving.value) return;
  editSaving.value = true;
  try {
    await achievementApi.updateContestSeries(current.value.id, {
      topicUrl: edit.value.topicUrl.trim() || "",
      isActive: edit.value.isActive,
    });
    await reload();
  } finally {
    editSaving.value = false;
  }
}

// --- Grant form ---
const grantForm = ref({ username: "", awardTypeId: "", workUrl: "" });
const granting = ref(false);
const grantError = ref<string | null>(null);

async function grant() {
  if (!current.value || granting.value) return;
  if (!grantForm.value.username || !grantForm.value.awardTypeId) return;
  granting.value = true;
  grantError.value = null;
  try {
    const { error } = await achievementApi.grantUserAward(
      grantForm.value.username,
      {
        awardTypeId: grantForm.value.awardTypeId,
        contestSeriesId: current.value.id,
        workUrl: grantForm.value.workUrl.trim() || null,
      },
    );
    if (error) {
      // The strip under the form is the one place a rejected grant is
      // answered - the moderator is looking at the fields it is about: the
      // award is already on this user, the user does not exist, and a refused
      // action, a rate limit or a dead server, whose plate the interceptor
      // held is taken down. An expired session and a dead connection stay
      // with the plate.
      grantError.value = refusalHere(error, "Не удалось выдать награду");
      return;
    }
    grantForm.value.username = "";
    grantForm.value.awardTypeId = "";
    grantForm.value.workUrl = "";
    await loadGrants();
  } finally {
    granting.value = false;
  }
}

// --- Grants list ---
// Everyone awarded in this series, straight from the server. It used to be an
// in-memory accumulator that never accumulated, over a UserAward model with no
// recipient — so the list was always empty and its revoke button would have
// sent an empty username.

const grants = ref<ContestSeriesAward[]>([]);
const grantsLoading = ref(false);

const GRANTS_FAILED = "Не удалось загрузить выданные награды";

/**
 * Why the list did not arrive, printed where the list would stand.
 *
 * A failed load is the state of the block and not a plate over it: there is
 * no list underneath to go back to, and a plate in the corner over an empty
 * block explains nothing (UI_STANDARDS, "Всплывающие сообщения"). The api
 * method has no seam for `ownsRefusal`, so the block keeps quiet on the
 * statuses the interceptor announces and prints the rest in the server's
 * words. Cleared by the next read.
 */
const grantsRefusal = ref("");

/**
 * Whether the last read was refused. "Награды в этой серии еще не выдавались"
 * is a claim about the series, and a refused read makes none: without this a
 * dead server read as a series nobody was awarded in.
 */
const grantsFailed = ref(false);

async function loadGrants() {
  if (!current.value) return;
  grantsLoading.value = true;
  grantsRefusal.value = "";
  grantsFailed.value = false;
  const { data, error } = await achievementApi.getContestSeriesAwards(
    current.value.id,
  );
  grantsLoading.value = false;
  if (error) {
    // The rows of an earlier read are not this answer: after a grant they are
    // short of the award just given, after a series switch they are another
    // series' rows.
    grants.value = [];
    grantsFailed.value = true;
    grantsRefusal.value = refusalHere(error, GRANTS_FAILED) ?? "";
    return;
  }
  grants.value = data?.resources ?? [];
}

const pendingRevoke = ref<ContestSeriesAward | null>(null);

// The request of the confirmation. The window waits for it, prints a refusal in
// itself and closes on success alone, so the interceptor keeps quiet about it.
async function confirmRevoke() {
  const award = pendingRevoke.value;
  if (!award) return;
  const result = await achievementApi.revokeUserAward(
    award.user.username,
    award.id,
    { ownsRefusal: true, ownsRateLimit: true },
  );
  if (!result.error) {
    grants.value = grants.value.filter((a) => a.id !== award.id);
  }
  return result;
}

// The grants follow the resolved series, so switching series from the list
// reloads them without depending on mount order.
watch(() => current.value?.id, loadGrants);

onMounted(async () => {
  await load();
  await loadGrants();
});
</script>

<template>
  <page-title>Серия конкурсов</page-title>

  <SecondaryText v-if="!hasAccess">{{ deniedText }}</SecondaryText>

  <section v-else class="series-detail">
    <header class="series-detail__header">
      <button
        type="button"
        class="text-button"
        @click="router.push({ name: 'moderation-awards' })"
      >
        ← К списку серий
      </button>
    </header>

    <BlockTitle v-if="current">
      {{ formatContestSeriesTitle(current.contestType, current.number) }},
      {{ current.year }}
    </BlockTitle>
    <SecondaryText v-else>Серия не найдена</SecondaryText>

    <template v-if="current">
      <!-- Edit section -->
      <section class="block">
        <h3>Параметры серии</h3>
        <div class="form-row">
          <label for="series-topic-url">Топик с итогами</label>
          <input
            id="series-topic-url"
            v-model="edit.topicUrl"
            type="url"
            placeholder="/forum/topic/..."
          />
        </div>
        <div class="form-row inline">
          <label>
            <input v-model="edit.isActive" type="checkbox" />
            Активна (видна в dropdown при выдаче)
          </label>
        </div>
        <Button
          type="button"
          variant="primary"
          :loading="editSaving"
          @click="saveEdit"
        >
          Сохранить
        </Button>
      </section>

      <!-- Grant section -->
      <section class="block">
        <h3>Выдать награду</h3>
        <div class="form-row">
          <label for="grant-username">Пользователь</label>
          <UserAutocomplete
            id="grant-username"
            v-model="grantForm.username"
            placeholder="Введите имя..."
          />
        </div>
        <div class="form-row">
          <label for="grant-award-type">Тип награды</label>
          <Select
            id="grant-award-type"
            v-model="grantForm.awardTypeId"
            :options="awardTypeOptions"
            placeholder="Не выбрано"
          />
        </div>
        <div class="form-row">
          <label for="grant-work-url">Топик с работой (опционально)</label>
          <input
            id="grant-work-url"
            v-model="grantForm.workUrl"
            type="url"
            placeholder="/forum/topic/..."
          />
        </div>
        <p v-if="grantError" class="form-error">{{ grantError }}</p>
        <Button
          type="button"
          variant="primary"
          :loading="granting"
          :disabled="!grantForm.username || !grantForm.awardTypeId"
          @click="grant"
        >
          Выдать
        </Button>
      </section>

      <!-- Grants list -->
      <section class="block">
        <h3>Выданные в этой серии</h3>
        <SecondaryText v-if="grantsLoading">Загрузка...</SecondaryText>
        <!-- Why the list did not arrive, in its place. A plate in the corner
             used to say this over an empty block. -->
        <p v-else-if="grantsRefusal" class="form-error" role="alert">
          {{ grantsRefusal }}
        </p>
        <SecondaryText v-else-if="grants.length === 0 && !grantsFailed">
          Награды в этой серии еще не выдавались
        </SecondaryText>
        <ul v-else-if="grants.length > 0" class="grants-list">
          <li v-for="a in grants" :key="a.id" class="grant-row">
            <GameIcon
              :name="a.type.iconName"
              class="grant-icon"
              :style="metalStyle(awardMetal(a.type.tier))"
            />
            <span class="grant-title">{{ a.type.title }}</span>
            <UserLink :user="a.user" />
            <span class="grant-date">{{ formatDate(a.awardedUtc) }}</span>
            <button
              type="button"
              class="text-button"
              @click="pendingRevoke = a"
            >
              Отозвать
            </button>
          </li>
        </ul>
      </section>
    </template>

    <ConfirmDialog
      :show="pendingRevoke !== null"
      title="Отозвать награду?"
      :message="`Награда &quot;${pendingRevoke?.type.title ?? ''}&quot; пропадет из профиля &quot;${pendingRevoke?.user.username ?? ''}&quot;`"
      confirm-label="Отозвать"
      failure="Не удалось отозвать награду"
      :action="confirmRevoke"
      @update:show="(v) => !v && (pendingRevoke = null)"
    />
  </section>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *

.series-detail
  display: flex
  flex-direction: column
  gap: $medium

  &__header
    display: flex
    align-items: center

.block
  +card()

  h3
    margin: 0 0 $small
    font-size: $secondary-font-size
    text-transform: uppercase
    letter-spacing: 0.5px
    color: $text-meta

.form-row
  display: flex
  flex-direction: column
  gap: $minor
  margin-bottom: $small

  &.inline
    flex-direction: row
    align-items: center
    gap: $small

  // A caption of a field of the page: +field-caption, as FormField draws it.
  label
    +field-caption

  // The words of a checkbox beside its box are not a caption: regular, as
  // they stood.
  &.inline label
    color: $text-muted
    font-weight: 400

// The way back to the list: a button that is a link of the site.
.text-button
  +inline-link-button

.form-error
  color: $accent-red
  margin: $small 0
  font-size: $secondary-font-size

.grants-list
  list-style: none
  margin: 0
  padding: 0
  display: flex
  flex-direction: column
  gap: $tiny

.grant-row
  display: grid
  grid-template-columns: 24px 1fr auto auto auto
  align-items: center
  gap: $small
  padding: $small
  border: 1px solid $border

  // The metal of the award, as the profile draws it (entities/achievement
  // tierMetal); a tier without one stays neutral.
  .grant-icon
    font-size: 20px
    color: var(--tier-metal, $text-muted)

  .grant-date
    font-variant-numeric: tabular-nums
    color: $text-meta
    font-size: $secondary-font-size
</style>
