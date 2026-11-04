<script setup lang="ts">
/**
 * The removed accounts, and the one way back from a removal: a removed account
 * is hidden from every other list, so this is the only place it can be seen.
 * One field matches a name or an address, whichever the support letter quoted,
 * anywhere in it. The name is not a link: the profile behind it answers 404.
 * The address column is drawn for an administrator only, as the server
 * withholds the field below that rank. The term and the page live in the
 * address, so a narrowed list is a link.
 */
import { computed, ref, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import { storeToRefs } from "pinia";
import { moderationApi, type RemovedAccount } from "@/entities/moderation";
import type { PagingInfo as PagingModel } from "@/shared/api/models/common";
import { DataTable, type Column } from "@/shared/ui/DataTable";
import { Paging } from "@/shared/ui/Paging";
import { ErrorState } from "@/shared/ui/ErrorState";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { FilterSearchInput } from "@/shared/ui/Filters";
import { ROLE_INFO, getRoleBadge } from "@/shared/config/roles";
import { formatDate } from "@/shared/lib/utils/datetime";
import {
  parsePageNumber,
  parseStringFromUrl,
  toQueryValue,
} from "@/shared/lib/filters";
import { NOTHING_FOUND, VALUE_UNAVAILABLE } from "@/shared/lib/constants/copy";
import { usePaging } from "@/shared/lib/composables/usePaging";
import { useGuardedRequest } from "@/shared/lib/composables/useGuardedRequest";
import { useAuthStore, userIsAdmin } from "@/entities/user";
import { OWNS_REFUSAL } from "@/shared/api/interceptor";

const route = useRoute();
const router = useRouter();
const { user } = storeToRefs(useAuthStore());
const isAdmin = computed(() => userIsAdmin(user.value));
// A page as long as the reader's lists (account settings).
const { pageSize } = usePaging();

const accounts = ref<RemovedAccount[]>([]);
const paging = ref<PagingModel | null>(null);

// The term and the page both come from the address; the guard drops the answer the reader has left.
const {
  loading,
  error: loadError,
  run,
} = useGuardedRequest({
  message: "Не удалось загрузить удаленных пользователей",
});

const pageNumber = computed(() => parsePageNumber(route.query.number) ?? 1);

/** The one filter the list takes; a term nothing carries is an empty page. */
const search = computed(
  () => parseStringFromUrl(toQueryValue(route.query.search)) || undefined,
);

// The field shows what the address says, so the back button puts the previous term back.
const searchInput = ref(search.value ?? "");
watch(search, (term) => (searchInput.value = term ?? ""));

function loadRemovedAccounts() {
  return run(
    () =>
      moderationApi.getRemovedAccounts({
        search: search.value,
        skip: (pageNumber.value - 1) * pageSize.value,
        take: pageSize.value,
      }),
    (data) => {
      accounts.value = data?.resources ?? [];
      paging.value = data?.paging ?? null;
    },
  );
}

watch([pageNumber, search], loadRemovedAccounts, { immediate: true });

/**
 * The term goes to the address without the page number, which counted pages of
 * the list being left; an emptied field drops the filter. The field takes the
 * trimmed term: the same term padded is the address already open, and a
 * navigation there runs nothing.
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

/** An emptied field drops the filter at once. */
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
 * The role of one account, in the singular: ROLE_INFO names the staff roles in
 * the plural, and the badge carries the singular for exactly those.
 */
function roleLabel(role: RemovedAccount["role"]): string {
  return getRoleBadge(role)?.label ?? ROLE_INFO[role].title;
}

const emptyText = computed(() =>
  search.value ? NOTHING_FOUND : "Удаленных пользователей нет",
);

const restoreTarget = ref<RemovedAccount | null>(null);

// The window waits for it, prints a refusal in itself and closes on success alone.
async function confirmRestore() {
  const target = restoreTarget.value;
  if (!target) return;
  const result = await moderationApi.restoreAccount(
    target.username,
    OWNS_REFUSAL,
  );
  if (!result.error) void loadRemovedAccounts();
  return result;
}
</script>

<template>
  <div class="moderation-removed-accounts">
    <page-title>Удаленные пользователи</page-title>

    <form class="search-line" @submit.prevent="applySearch">
      <FilterSearchInput
        :model-value="searchInput"
        placeholder="Имя или почта"
        @update:model-value="onInput"
      />
      <Button type="submit" variant="primary">Найти</Button>
    </form>

    <ErrorState
      v-if="loadError"
      :message="loadError"
      :retry="loadRemovedAccounts"
    />

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

    <!-- The account answers again the moment this is pressed. -->
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
@use "@/assets/styles/Links" as *

// The field takes the width the button leaves, one line at every width.
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
