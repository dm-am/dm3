<script setup lang="ts">
import { computed, ref, watch } from "vue";
import { useRoute } from "vue-router";
import { userApi } from "@/entities/user";
import type { User } from "@/shared/api/models/community";
import type { PagingInfo as PagingModel } from "@/shared/api/models/common";
import { DataTable, type Column } from "@/shared/ui/DataTable";
import { Paging } from "@/shared/ui/Paging";
import { ErrorState } from "@/shared/ui/ErrorState";
import { formatDate } from "@/shared/lib/utils/datetime";
import { parsePageNumber } from "@/shared/lib/filters";
import { usePaging } from "@/shared/lib/composables/usePaging";
import { VALUE_UNAVAILABLE } from "@/shared/lib/constants/copy";
import { UserLink } from "@/entities/user";

const route = useRoute();
// A page as long as the reader's lists (account settings).
const { pageSize } = usePaging();

const users = ref<User[]>([]);
const paging = ref<PagingModel | null>(null);
const loading = ref(false);
const loadError = ref<string | null>(null);

const pageNumber = computed(() => parsePageNumber(route.query.number) ?? 1);

const columns: Column[] = [
  { key: "user", label: "Пользователь" },
  { key: "registered", label: "Регистрация", width: "27%" },
  { key: "posts", label: "Игровых постов", width: "27%", align: "center" },
];

// The newcomers of the last three months
function threeMonthsAgoUtc(): string {
  const d = new Date();
  d.setMonth(d.getMonth() - 3);
  return d.toISOString();
}

async function loadNewUsers() {
  loading.value = true;
  const { data, error } = await userApi.getUsers({
    isNewbie: true,
    activity: "All",
    sort: "Registered",
    sortOrder: "desc",
    registeredFromUtc: threeMonthsAgoUtc(),
    take: pageSize.value,
    skip: (pageNumber.value - 1) * pageSize.value,
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

watch(pageNumber, loadNewUsers, { immediate: true });
</script>

<template>
  <div class="moderation-new-users">
    <page-title>Новые пользователи</page-title>

    <ErrorState v-if="loadError" :message="loadError" :retry="loadNewUsers" />

    <template v-else>
      <DataTable
        :columns="columns"
        :data="users"
        :loading="loading"
        empty-text="Новых пользователей пока нет"
        aria-label="Новые пользователи"
      >
        <template #cell-user="{ row }">
          <UserLink :user="row" />
        </template>
        <template #cell-registered="{ row }">
          {{ formatDate(row.registeredUtc) }}
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
