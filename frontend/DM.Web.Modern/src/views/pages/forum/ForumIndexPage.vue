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
    boards.value.forEach((board) => {
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
      :disabled="markingAllAsRead || !boards.some((b) => b.unreadCommentsCount)"
      @click="markAllAsRead"
    >
      {{ markingAllAsRead ? "Отмечаю..." : "Пометить всё прочитанным" }}
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
      <div v-for="board in boards" :key="board.id" class="boards-row">
        <div class="col-title">
          <router-link :to="{ name: 'forum', params: { id: board.id } }">
            {{ board.id }}
          </router-link>
        </div>
        <div class="col-description">{{ board.description || "" }}</div>
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
            <human-date
              :date="board.lastComment.createdUtc"
              format="DD.MM.YYYY HH:mm"
            />
          </template>
          <span v-else class="no-comments">—</span>
        </div>
      </div>
    </template>
  </div>
</template>

<style lang="sass">
@import "@/assets/styles/Themes"
@import "@/assets/styles/Tables"
@import "@/assets/styles/Inputs"

.forum-actions
  margin-bottom: $medium
  display: flex
  justify-content: flex-end

.mark-all-read-btn
  +button

.boards-table
  width: 100%
  +table

.boards-header,
.boards-row
  display: grid
  grid-template-columns: 20% 35% 10% 15% 20%
  align-items: stretch
  +table-columns

  & > div
    display: flex
    align-items: center

.boards-header
  +table-header

  .col-description,
  .col-topics,
  .col-comments,
  .col-last
    justify-content: center

.boards-row
  +table-row

  &:last-child
    border-bottom: none

  .col-title a
    color: $link
    &:hover
      color: $link-hover

  .col-description
    color: $text

  .col-topics,
  .col-comments,
  .col-last
    justify-content: center
    text-align: center

  .col-comments
    flex-direction: column

  .col-comments .unread
    color: $heading

.no-comments
  color: $heading-alt

.boards-empty
  padding: $big
  text-align: center
</style>
