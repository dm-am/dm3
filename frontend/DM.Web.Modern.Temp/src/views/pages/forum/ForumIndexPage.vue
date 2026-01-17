<script setup lang="ts">
import { useBoardsStore, useUserStore } from "@/stores";
import { storeToRefs } from "pinia";
import { onMounted, ref } from "vue";
import UserLink from "@/components/community/UserLink.vue";
import HumanDate from "@/components/dates/HumanDate.vue";
import forumApi from "@/api/requests/forumApi";

const store = useBoardsStore();
const { boards } = storeToRefs(store);
const { user } = storeToRefs(useUserStore());

const markingAllAsRead = ref(false);

async function markAllAsRead() {
  if (!boards.value) return;

  markingAllAsRead.value = true;
  try {
    await forumApi.markAllForumAsRead();
    // Update local state
    boards.value.forEach(board => {
      (board as any).unreadCommentsCount = 0;
      (board as any).unreadTopicsCount = 0;
    });
  } finally {
    markingAllAsRead.value = false;
  }
}

onMounted(() => store.fetchBoards());
</script>

<template>
  <page-title>Форум</page-title>

  <div v-if="user && boards" class="forum-actions">
    <button
      class="mark-all-read-btn"
      :disabled="markingAllAsRead || !boards.some(b => b.unreadCommentsCount)"
      @click="markAllAsRead"
    >
      {{ markingAllAsRead ? 'Отмечаю...' : 'Пометить всё прочитанным' }}
    </button>
  </div>

  <div class="boards-table">
    <div class="boards-header">
      <div class="col-title">Раздел</div>
      <div class="col-description">Описание</div>
      <div class="col-topics">Темы</div>
      <div class="col-comments">Комментарии</div>
      <div class="col-last">Последняя активность</div>
    </div>

    <the-loader v-if="!boards" :big="true" />
    <secondary-text v-else-if="!boards.length" class="boards-empty">
      Нет доступных разделов
    </secondary-text>
    <template v-else>
      <div
        v-for="board in boards"
        :key="board.id"
        class="boards-row"
      >
        <div class="col-title">
          <router-link :to="{ name: 'forum', params: { id: board.id } }">
            {{ board.id }}
          </router-link>
        </div>
        <div class="col-description">{{ board.description || '' }}</div>
        <div class="col-topics">{{ board.topicsCount || 0 }}</div>
        <div class="col-comments">
          {{ board.commentsCount || 0 }}
          <span v-if="board.unreadCommentsCount" class="unread">
            ({{ board.unreadCommentsCount }})
          </span>
        </div>
        <div class="col-last">
          <template v-if="board.lastComment">
            <user-link :user="board.lastComment.author" />,
            <human-date :date="board.lastComment.createdUtc" format="DD.MM.YYYY HH:mm" />
          </template>
          <span v-else class="no-comments">—</span>
        </div>
      </div>
    </template>
  </div>
</template>

<style lang="sass">
@import "@/assets/styles/Themes"

.forum-actions
  margin-bottom: $medium
  display: flex
  justify-content: flex-end

.mark-all-read-btn
  padding: $small $medium
  border: 1px solid
  +theme(border-color, $border)
  +theme(background-color, $button-background)
  +theme(color, $button-text)
  cursor: pointer

  &:hover:not(:disabled)
    +theme(background-color, $button-background-hover)
    +theme(color, $button-text-hover)

  &:disabled
    +theme(background-color, $button-background-disabled)
    +theme(color, $button-text-disabled)
    cursor: not-allowed

.boards-table
  width: 100%
  border: 1px solid #ccc
  +theme(border-color, $border)

.boards-header,
.boards-row
  display: grid
  grid-template-columns: 20% 35% 10% 15% 20%
  align-items: stretch

  & > div
    padding: $small $medium
    border-right: 1px solid #ccc
    +theme(border-right-color, $border)
    display: flex
    align-items: center

    &:last-child
      border-right: none

.boards-header
  border-bottom: 1px solid #ccc
  +theme(background-color, $panel-background)
  +theme(border-bottom-color, $border)
  +theme(color, $text)
  font-weight: bold

  .col-description,
  .col-topics,
  .col-comments,
  .col-last
    justify-content: center

.boards-row
  border-bottom: 1px solid #ccc
  +theme(border-bottom-color, $border)

  &:last-child
    border-bottom: none

  &:hover
    +theme(background-color, $panel-background-hover)

  .col-title a
    +theme(color, $active-text)
    &:hover
      +theme(color, $active-text-hover)

  .col-description
    +theme(color, $text)

  .col-topics,
  .col-comments,
  .col-last
    justify-content: center
    text-align: center

  .col-comments
    flex-direction: column

  .col-comments .unread
    +theme(color, $accent-text)

.no-comments
  +theme(color, $muted-text)

.boards-empty
  padding: $big
  text-align: center
</style>
