<script setup lang="ts">
/**
 * "Удаленные пользователи" — the removed accounts, and the one way back from a
 * removal.
 *
 * A removed account is hidden from every other list, profile and lookup in the
 * product, so this page is the only place it can be seen at all. That is what
 * the search here is for: a moderator answering a support letter has a name or
 * an address out of that letter and nothing else, and one field matches both,
 * because whoever wrote the letter is the one who decided which of the two he
 * quoted.
 *
 * There is no queue and nothing to decide: a reader who wants his account back
 * writes to support like he would about anything else, a person reads that
 * letter, and "Восстановить" makes the account answer again at once. Removing
 * one is the other door and lives on the moderated profile.
 *
 * The name is not a link. The profile behind it answers as if the account never
 * existed, which is the point of a removal, and a link to a 404 would read as a
 * broken row rather than as a removed one.
 *
 * The address column is drawn for an administrator only, the way the server
 * withholds the field below that rank: a column of empty cells would say the
 * accounts have no address rather than that this viewer may not read it.
 *
 * The search is the search line of the moderation lists: the site's search
 * field as wide as the column and "Найти" beside it, no caption over it, the
 * placeholder "Имя или почта" naming what it matches. It looks a term up on
 * "Найти" and not as the reader types, as its neighbours do. The server
 * matches the term anywhere in a name or an address, so this placeholder,
 * unlike theirs, does not say "Точное".
 *
 * Both the search term and the page number live in the address bar rather than
 * in refs here, so a narrowed list is a link one moderator can hand to another.
 */
import { computed, ref, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import { moderationApi, type RemovedAccount } from "@/entities/moderation";
import type { PagingInfo as PagingModel } from "@/shared/api/models/common";
import { DataTable, type Column } from "@/shared/ui/DataTable";
import { Paging } from "@/shared/ui/Paging";
import { ErrorState } from "@/shared/ui/ErrorState";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { FilterSearchInput } from "@/shared/ui/Filters";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { ROLE_INFO, getRoleBadge } from "@/shared/config/roles";
import { formatDate } from "@/shared/lib/utils/datetime";
import { parsePageNumber } from "@/shared/lib/filters";
import { VALUE_UNAVAILABLE } from "@/shared/lib/constants/copy";
import { useGuardedRequest } from "@/shared/lib/composables/useGuardedRequest";
import { useRoleGate } from "./lib/useRoleGate";
import { sectionRole } from "./lib/sections";

/** The page of the neighbouring moderation lists. */
const PAGE_SIZE = 25;

const route = useRoute();
const router = useRouter();
const { hasAccess, deniedText, isAdmin } = useRoleGate(
  sectionRole("moderation-removed-accounts"),
);

const accounts = ref<RemovedAccount[]>([]);
const paging = ref<PagingModel | null>(null);

// A term and a page number both come from the address and both change under the
// reader, so two answers can be on the wire at once; the guard drops the one
// the reader has already left instead of letting whichever lands last decide
// what the table shows.
const {
  loading,
  error: loadError,
  run,
} = useGuardedRequest({
  message: "Не удалось загрузить удаленных пользователей",
});

const pageNumber = computed(() => parsePageNumber(route.query.number) ?? 1);

/** The one filter the list takes; a term nothing carries is an empty page. */
const search = computed(() => {
  const value = route.query.search;
  const term = Array.isArray(value) ? value[0] : value;
  return term ? String(term) : undefined;
});

// The field shows what the address says, so a link handed over opens with the
// term already in it and the back button puts the previous term back.
const searchInput = ref(search.value ?? "");
watch(search, (term) => (searchInput.value = term ?? ""));

function fetch() {
  return run(
    () =>
      moderationApi.getRemovedAccounts({
        search: search.value,
        skip: (pageNumber.value - 1) * PAGE_SIZE,
        take: PAGE_SIZE,
      }),
    (data) => {
      accounts.value = data?.resources ?? [];
      paging.value = data?.paging ?? null;
    },
  );
}

// Nothing is asked for on behalf of a viewer the gate refuses: the refusal is
// the whole of what such a viewer sees, and a request under it would be a 403
// in the log for a screen that was never drawn.
watch(
  [pageNumber, search],
  () => {
    if (hasAccess.value) fetch();
  },
  { immediate: true },
);

/**
 * The term goes to the address, and the page number stays behind: it counted
 * pages of the list the moderator is leaving, and carrying it over would open
 * the new list at a page it may not have. An emptied field drops the filter.
 *
 * The field takes the trimmed term here rather than waiting for the address to
 * hand it back: the same term again, padded, leaves the address exactly where
 * it already is, and a navigation to the address already open is no navigation
 * at all, so nothing downstream of it runs.
 */
function applySearch() {
  const query = { ...route.query };
  delete query.number;
  const term = searchInput.value.trim();
  if (term) query.search = term;
  else delete query.search;
  searchInput.value = term;
  router.push({ name: "moderation-removed-accounts", query });
}

/**
 * Emptied by the cross of the field or by hand, the field drops the filter at
 * once: a list narrowed to a term under a field that holds none would say two
 * different things.
 */
function onInput(value: string) {
  searchInput.value = value;
  if (!value.trim() && search.value) applySearch();
}

const columns = computed<Column[]>(() => {
  const base: Column[] = [{ key: "username", label: "Пользователь" }];
  if (isAdmin.value) base.push({ key: "email", label: "Почта", width: "22%" });
  base.push(
    { key: "role", label: "Роль", width: "16%" },
    { key: "registered", label: "Регистрация", width: "14%" },
    {
      key: "activity",
      label: "Последняя активность",
      width: "16%",
    },
    { key: "actions", label: "", width: "14%", align: "center" },
  );
  return base;
});

/**
 * The role of one account, in the singular.
 *
 * ROLE_INFO names the staff roles in the plural, because it titles the sections
 * that group them, so a removed administrator would read "Администраторы" in a
 * column about him alone. The badge carries the singular for exactly those
 * roles, and ROLE_INFO is already singular for the rest.
 */
function roleLabel(role: RemovedAccount["role"]): string {
  return getRoleBadge(role)?.label ?? ROLE_INFO[role].title;
}

const emptyText = computed(() =>
  search.value ? "Ничего не найдено" : "Удаленных пользователей нет",
);

// --- Bringing one back ---
const restoreTarget = ref<RemovedAccount | null>(null);

// The request of the confirmation. The window waits for it, prints a refusal in
// itself and closes on success alone, so the interceptor keeps quiet about it.
async function confirmRestore() {
  const target = restoreTarget.value;
  if (!target) return;
  const result = await moderationApi.restoreAccount(target.username, {
    ownsRefusal: true,
    ownsRateLimit: true,
  });
  if (!result.error) void fetch();
  return result;
}
</script>

<template>
  <div class="moderation-removed-accounts">
    <page-title>Удаленные пользователи</page-title>

    <SecondaryText v-if="!hasAccess">{{ deniedText }}</SecondaryText>

    <template v-else>
      <form class="search-line" @submit.prevent="applySearch">
        <FilterSearchInput
          :model-value="searchInput"
          placeholder="Имя или почта"
          @update:model-value="onInput"
        />
        <Button type="submit" variant="primary">Найти</Button>
      </form>

      <ErrorState v-if="loadError" :message="loadError" :retry="fetch" />

      <template v-else>
        <DataTable
          :columns="columns"
          :data="accounts"
          :loading="loading"
          :empty-text="emptyText"
          aria-label="Удаленные пользователи"
        >
          <template #cell-email="{ row }">
            {{ row.email || VALUE_UNAVAILABLE }}
          </template>
          <template #cell-role="{ row }">
            {{ roleLabel(row.role) }}
          </template>
          <template #cell-registered="{ row }">
            {{ formatDate(row.registeredUtc) }}
          </template>
          <template #cell-activity="{ row }">
            {{
              row.lastActivityUtc
                ? formatDate(row.lastActivityUtc)
                : VALUE_UNAVAILABLE
            }}
          </template>
          <template #cell-actions="{ row }">
            <button
              type="button"
              class="restore-button"
              @click="restoreTarget = row"
            >
              Восстановить
            </button>
          </template>
        </DataTable>

        <Paging
          v-if="paging"
          class="pager"
          :paging="paging"
          :to="{ name: 'moderation-removed-accounts' }"
          use-query
        />
      </template>
    </template>

    <!-- Nothing is scheduled and nothing waits: the account answers again the
         moment this is pressed. -->
    <ConfirmDialog
      :show="restoreTarget !== null"
      title="Восстановить аккаунт?"
      :message="`Аккаунт &quot;${restoreTarget?.username ?? ''}&quot; вернется вместе с именем, почтой и всеми записями`"
      confirm-label="Восстановить"
      failure="Не удалось восстановить аккаунт"
      :action="confirmRestore"
      @update:show="(shown) => !shown && (restoreTarget = null)"
    />
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *

// The search line of the moderation lists: the field takes the width the
// button leaves, both $control-height, one line at every width.
.search-line
  display: flex
  align-items: center
  gap: $small
  margin-bottom: $medium

.pager
  margin-top: $medium

.restore-button
  +inline-link-button
</style>
