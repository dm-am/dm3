<script setup lang="ts">
import { onMounted, ref } from "vue";
import { moderationApi, type ModeratorOverview } from "@/entities/moderation";
import { DataTable, type Column } from "@/shared/ui/DataTable";
import { ErrorState } from "@/shared/ui/ErrorState";
import { UserLink } from "@/entities/user";
import { VALUE_UNAVAILABLE } from "@/shared/lib/constants/copy";

const loading = ref(false);
const loadError = ref<string | null>(null);

const columns: Column[] = [
  { key: "user", label: "Модератор", width: "24%" },
  { key: "boards", label: "Форумы" },
  { key: "games", label: "Курируемые игры", width: "25.3%" },
  { key: "blogs", label: "Курируемые блоги", width: "25.3%" },
];

// DataTable keys its rows by id: the moderator's user id.
type ModeratorRow = ModeratorOverview & { id: string };

const rows = ref<ModeratorRow[]>([]);

async function loadModerators() {
  loading.value = true;
  const { data, error } = await moderationApi.getModerators();
  loading.value = false;
  if (error) {
    loadError.value = "Не удалось загрузить список модераторов";
    return;
  }
  loadError.value = null;
  rows.value = (data?.resources ?? []).map((m) => ({ ...m, id: m.user.id }));
}

onMounted(loadModerators);
</script>

<template>
  <div class="moderation-moderators">
    <page-title>Модераторы</page-title>

    <ErrorState v-if="loadError" :message="loadError" :retry="loadModerators" />

    <DataTable
      v-else
      :columns="columns"
      :data="rows"
      :loading="loading"
      empty-text="Модераторов пока нет"
      aria-label="Модераторы и их зоны ответственности"
    >
      <template #cell-user="{ row }">
        <UserLink :user="row.user" />
      </template>
      <template #cell-boards="{ row }">
        <span v-if="row.boards.length">
          {{ row.boards.map((z) => z.title).join(", ") }}
        </span>
        <span v-else class="muted">{{ VALUE_UNAVAILABLE }}</span>
      </template>
      <template #cell-games="{ row }">
        <template v-if="row.curatedGames.length">
          <template v-for="(zone, i) in row.curatedGames" :key="zone.id">
            <router-link :to="{ name: 'game', params: { id: zone.id } }">{{
              zone.title
            }}</router-link
            ><template v-if="i < row.curatedGames.length - 1">, </template>
          </template>
        </template>
        <span v-else class="muted">{{ VALUE_UNAVAILABLE }}</span>
      </template>
      <template #cell-blogs="{ row }">
        <template v-if="row.curatedBlogs.length">
          <template v-for="(zone, i) in row.curatedBlogs" :key="zone.id">
            <router-link :to="{ name: 'blog', params: { id: zone.id } }">{{
              zone.title
            }}</router-link
            ><template v-if="i < row.curatedBlogs.length - 1">, </template>
          </template>
        </template>
        <span v-else class="muted">{{ VALUE_UNAVAILABLE }}</span>
      </template>
    </DataTable>
  </div>
</template>
