<script setup lang="ts">
import SidebarBlock from "./SidebarBlock.vue";
import SidebarSkeleton from "./SidebarSkeleton.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { Tooltip } from "@/shared/ui/Tooltip";
import { useBoardsStore } from "@/entities/forum";
import { useAuthStore } from "@/entities/user";
import { onMounted, watch } from "vue";
import { storeToRefs } from "pinia";
import { useViewerChange } from "@/shared/lib/composables/useViewerChange";
import { useRoute } from "vue-router";

const store = useBoardsStore();
const { boards, boardsError } = storeToRefs(store);
const userStore = useAuthStore();
const route = useRoute();

// Fetch on mount (uses cache with stale-while-revalidate)
onMounted(() => store.fetchBoards());

// Refetch on any change of viewer to update unread counts
useViewerChange(() => store.fetchBoards(true));

// Re-trigger on navigation so a failed fetch gets another chance once the
// TTL cache considers it stale, without requiring a full page reload.
watch(
  () => route.fullPath,
  () => store.fetchBoards(),
);

// For guests the backend returns total counts (nothing can be unread),
// so the tooltip must not claim the counters are unread. Same wording
// pattern as useGameDisplay/useBlogDisplay counter tooltips.
function commentsTooltip(count: number): string {
  return userStore.user
    ? `Непрочитанных комментариев: ${count}`
    : `Комментариев: ${count}`;
}
</script>

<template>
  <SidebarBlock token="ForumBoards" title="Форум">
    <template #title>Форум</template>
    <SidebarSkeleton v-if="boards === null && !boardsError" :lines="6" />
    <li v-else-if="boards === null" class="error-row">
      <SecondaryText
        >Не удалось загрузить
        <button type="button" class="retry" @click="store.fetchBoards(true)">
          Повторить
        </button></SecondaryText
      >
    </li>
    <li v-else-if="boards.length === 0">
      <SecondaryText>Разделов форума пока нет</SecondaryText>
    </li>
    <li v-else v-for="forum in boards" :key="forum.id" class="board-link">
      <span class="muted" aria-hidden="true">- </span
      ><Tooltip :text="forum.description || undefined"
        ><router-link :to="{ name: 'forum', params: { alias: forum.alias } }">{{
          forum.title
        }}</router-link></Tooltip
      ><span v-if="forum.unreadCommentsCount" class="counters"
        ><span class="bracket"> (</span
        ><Tooltip :text="commentsTooltip(forum.unreadCommentsCount)"
          ><router-link
            :to="{ name: 'forum', params: { alias: forum.alias } }"
            >{{ forum.unreadCommentsCount }}</router-link
          ></Tooltip
        ><span class="bracket">)</span></span
      >
    </li>
  </SidebarBlock>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *

.muted
  color: $text-muted

.counters
  // Container only, no color

.bracket
  color: $text-muted

.retry
  margin-left: $small
  +inline-link-button
</style>
