<script setup lang="ts">
import SidebarBlock from "./SidebarBlock.vue";
import SidebarSkeleton from "./SidebarSkeleton.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { Tooltip } from "@/shared/ui/Tooltip";
import { useBoardsStore } from "@/entities/forum";
import { useUserStore } from "@/entities/user";
import { onMounted, ref, watch } from "vue";
import { storeToRefs } from "pinia";

const store = useBoardsStore();
const { boards } = storeToRefs(store);
const userStore = useUserStore();

// Detect failure locally: when the fetch settles and the list is still
// null, the request failed (prevents an eternal skeleton).
const failed = ref(false);

// Fetch on mount (uses cache with stale-while-revalidate)
onMounted(async () => {
  await store.fetchBoards();
  failed.value = boards.value === null;
});

// Refetch only on actual login/logout to update unread counts
watch(
  () => userStore.user?.username,
  async (newUsername, oldUsername) => {
    // Only refetch if user actually logged in or out
    if ((newUsername && !oldUsername) || (!newUsername && oldUsername)) {
      await store.fetchBoards();
      failed.value = boards.value === null;
    }
  },
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
  <SidebarBlock token="ForumBoards">
    <template #title>Форум</template>
    <SidebarSkeleton v-if="boards === null && !failed" :lines="6" />
    <SecondaryText v-else-if="boards === null">
      Не удалось загрузить
    </SecondaryText>
    <SecondaryText v-else-if="boards.length === 0">
      Разделов форума пока нет
    </SecondaryText>
    <div v-else v-for="forum in boards" :key="forum.id" class="board-link">
      <span class="muted">- </span
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
    </div>
  </SidebarBlock>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Themes"

.muted
  color: $text-muted
  user-select: none

.counters
  // Container only, no color

.bracket
  color: $text-muted
</style>
