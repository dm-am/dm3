<script setup lang="ts">
import SidebarBlock from "./SidebarBlock.vue";
import SidebarSkeleton from "./SidebarSkeleton.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { Tooltip } from "@/shared/ui/Tooltip";
import { useBoardsStore } from "@/entities/forum";
import { useUserStore } from "@/entities/user";
import { onMounted, watch } from "vue";
import { storeToRefs } from "pinia";

const store = useBoardsStore();
const { boards } = storeToRefs(store);
const userStore = useUserStore();

// Fetch on mount (uses cache with stale-while-revalidate)
onMounted(() => {
  store.fetchBoards();
});

// Refetch only on actual login/logout to update unread counts
watch(
  () => userStore.user?.username,
  (newUsername, oldUsername) => {
    // Only refetch if user actually logged in or out
    if ((newUsername && !oldUsername) || (!newUsername && oldUsername)) {
      store.fetchBoards();
    }
  },
);
</script>

<template>
  <SidebarBlock token="ForumBoards">
    <template #title>Форум</template>
    <SidebarSkeleton v-if="boards === null" :lines="6" />
    <SecondaryText v-else-if="boards.length === 0">
      Нет разделов форума
    </SecondaryText>
    <div v-else v-for="forum in boards" :key="forum.id" class="board-link">
      <span class="muted">- </span
      ><Tooltip :text="forum.description || undefined"
        ><router-link :to="{ name: 'forum', params: { alias: forum.alias } }">{{
          forum.title
        }}</router-link></Tooltip
      ><span v-if="forum.unreadCommentsCount" class="counters"
        ><span class="bracket"> (</span
        ><Tooltip :text="`Непрочитанных комментариев: ${forum.unreadCommentsCount}`"
          ><router-link
            :to="{ name: 'forum', params: { alias: forum.alias } }"
          >{{ forum.unreadCommentsCount }}</router-link></Tooltip
        ><span class="bracket">)</span
      ></span>
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
