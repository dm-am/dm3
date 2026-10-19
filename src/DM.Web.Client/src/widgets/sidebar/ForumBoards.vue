<script setup lang="ts">
import SidebarBlock from "./SidebarBlock.vue";
import SidebarSkeleton from "./SidebarSkeleton.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { Tooltip } from "@/shared/ui/Tooltip";
import { useBoardsStore } from "@/entities/forum";
import { useAuthStore } from "@/entities/user";
import { storeToRefs } from "pinia";
import { useSidebarRefresh } from "./useSidebarRefresh";

const store = useBoardsStore();
const { boards, boardsError } = storeToRefs(store);
const userStore = useAuthStore();

useSidebarRefresh(store.fetchBoards);

// A guest gets totals, nothing unread, and the tooltip must not claim otherwise.
function commentsTooltip(count: number): string {
  return userStore.user
    ? `Непрочитанных комментариев: ${count}`
    : `Комментариев: ${count}`;
}
</script>

<template>
  <SidebarBlock token="ForumBoards" title="Форум">
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
@use "@/assets/styles/Links" as *

.bracket
  color: $text-muted

.retry
  margin-left: $small
  +inline-link-button
</style>
