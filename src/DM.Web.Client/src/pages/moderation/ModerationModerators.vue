<script setup lang="ts">
/**
 * ModerationModerators — "Модерация" (product doc 4.2.1.5 first link).
 * Lists the moderation team with zones of responsibility: forum boards,
 * curated games and curated blogs (GET v1/moderation/moderators,
 * highest role first). Moderator+ (server-enforced via RequireRole).
 */
import { onMounted, ref } from "vue";
import { moderationApi, type ModeratorOverview } from "@/entities/moderation";
import { DataTable, type Column } from "@/shared/ui/DataTable";
import { ErrorState } from "@/shared/ui/ErrorState";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { UserLink } from "@/entities/user";
import { useRoleGate } from "./lib/useRoleGate";
import { VALUE_UNAVAILABLE } from "@/shared/lib/constants/copy";

const { hasAccess, deniedText } = useRoleGate("Moderator");

const moderators = ref<ModeratorOverview[]>([]);
const loading = ref(false);
const loadError = ref<string | null>(null);

const columns: Column[] = [
  { key: "user", label: "Модератор", width: "22%" },
  { key: "boards", label: "Форумы" },
  { key: "games", label: "Курируемые игры" },
  { key: "blogs", label: "Курируемые блоги" },
];

// DataTable requires rows with an id — key by the moderator's user id.
type ModeratorRow = ModeratorOverview & { id: string };

const rows = ref<ModeratorRow[]>([]);

async function fetch() {
  loading.value = true;
  const { data, error } = await moderationApi.getModerators();
  loading.value = false;
  if (error) {
    loadError.value = "Не удалось загрузить список модераторов";
    return;
  }
  loadError.value = null;
  moderators.value = data?.resources ?? [];
  rows.value = moderators.value.map((m) => ({ ...m, id: m.user.id }));
}

onMounted(fetch);
</script>

<template>
  <div class="moderation-moderators">
    <page-title>Модерация</page-title>

    <SecondaryText v-if="!hasAccess">{{ deniedText }}</SecondaryText>

    <ErrorState v-else-if="loadError" :message="loadError" :retry="fetch" />

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

<style scoped lang="sass">
.muted
  color: $text-muted
</style>
