<script setup lang="ts">
/**
 * ModerationNewUsers — "Новые пользователи" (doc 4.2.3.8.5).
 * Users with newbie role (< 100 game posts) registered within the last
 * 3 months, newest registrations first. Row: name (profile link),
 * registration date, game post count.
 */
import { computed, ref, watch } from "vue";
import { useRoute } from "vue-router";
import { userApi } from "@/entities/user";
import type { User } from "@/shared/api/models/community";
import type { Paging as PagingModel } from "@/shared/api/models/common";
import { DataTable, type Column } from "@/shared/ui/DataTable";
import { Paging } from "@/shared/ui/Paging";
import { ErrorState } from "@/shared/ui/ErrorState";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { formatDate } from "@/shared/lib/utils/datetime";
import { VALUE_UNAVAILABLE } from "@/shared/lib/constants/copy";
import { UserLink } from "@/entities/user";
import { useRoleGate } from "./lib/useRoleGate";

const PAGE_SIZE = 25;

const route = useRoute();
const { hasAccess, deniedText } = useRoleGate("Moderator");

const users = ref<User[]>([]);
const paging = ref<PagingModel | null>(null);
const loading = ref(false);
const loadError = ref<string | null>(null);

const pageNumber = computed(() => {
  const n = parseInt(String(route.query.number ?? "1"), 10);
  return Number.isFinite(n) && n > 0 ? n : 1;
});

const columns: Column[] = [
  { key: "user", label: "Пользователь" },
  { key: "registered", label: "Регистрация", width: "25%" },
  { key: "posts", label: "Игровых постов", width: "25%", align: "center" },
];

// Doc: registered no more than 3 months ago
function threeMonthsAgoUtc(): string {
  const d = new Date();
  d.setMonth(d.getMonth() - 3);
  return d.toISOString();
}

async function fetch() {
  loading.value = true;
  const { data, error } = await userApi.getUsers({
    isNewbie: true,
    activity: "All",
    sort: "Registered",
    sortOrder: "desc",
    registeredFromUtc: threeMonthsAgoUtc(),
    take: PAGE_SIZE,
    skip: (pageNumber.value - 1) * PAGE_SIZE,
  });
  loading.value = false;
  if (error) {
    loadError.value = "Не удалось загрузить новых пользователей";
    return;
  }
  loadError.value = null;
  users.value = data?.resources ?? [];
  paging.value = data?.paging ?? null;
}

watch(pageNumber, fetch, { immediate: true });

// DataTable rows keyed by user id
const rows = computed(() => users.value.map((u) => ({ ...u, id: u.id })));
</script>

<template>
  <div class="moderation-new-users">
    <page-title>Новые пользователи</page-title>

    <SecondaryText v-if="!hasAccess">{{ deniedText }}</SecondaryText>

    <ErrorState v-else-if="loadError" :message="loadError" :retry="fetch" />

    <template v-else>
      <DataTable
        :columns="columns"
        :data="rows"
        :loading="loading"
        empty-text="Новых пользователей пока нет"
        aria-label="Новые пользователи"
      >
        <template #cell-user="{ row }">
          <UserLink :user="row" />
        </template>
        <template #cell-registered="{ row }">
          {{ formatDate(row.registeredUtc ?? row.registrationUtc) }}
        </template>
        <template #cell-posts="{ row }">
          {{ row.rating?.totalPosts ?? VALUE_UNAVAILABLE }}
        </template>
      </DataTable>

      <Paging
        v-if="paging"
        class="pager"
        :paging="paging"
        :to="{ name: 'moderation-new-users' }"
        use-query
      />
    </template>
  </div>
</template>

<style scoped lang="sass">
.pager
  margin-top: $medium
</style>
