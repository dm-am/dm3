<script setup lang="ts">
import { IconType } from "@/components/icons/iconType";
import ThePaging from "@/components/ThePaging.vue";
import { useRoute } from "vue-router";
import { storeToRefs } from "pinia";
import { useBoardsStore } from "@/stores";
import UserLink from "@/components/community/UserLink.vue";
import HumanTimespan from "@/components/dates/HumanTimespan.vue";
import HumanDate from "@/components/dates/HumanDate.vue";

const route = useRoute();
const { topics } = storeToRefs(useBoardsStore());
</script>

<template>
  <the-paging
    v-if="topics"
    :paging="topics.paging!"
    :to="{ name: 'forum', params: route.params }"
  />

  <div class="topics-table">
    <div class="topics-header">
      <div class="col-title">Тема</div>
      <div class="col-date">Дата</div>
      <div class="col-author">Автор</div>
      <div class="col-comments">
        <the-icon :font="IconType.CommentsNoUnread" />
      </div>
      <div class="col-last">Последнее сообщение</div>
    </div>

    <the-loader v-if="!topics" :big="true" />
    <secondary-text v-else-if="!topics.resources.length" class="topics-empty">
      Еще не создано ни одной темы
    </secondary-text>
    <template v-else>
      <div
        v-for="topic in topics.resources"
        :key="topic.id"
        :class="[
          'topics-row',
          { closed: topic.isClosed, attached: topic.isAttached },
        ]"
      >
        <div class="col-title">
          <router-link
            :to="{
              name: 'topic',
              params: {
                id: topic.id,
                n: topic.commentsCount - topic.unreadCommentsCount,
              },
            }"
          >
            <the-icon v-if="topic.isAttached" :font="IconType.Attached" />
            <the-icon v-if="topic.isClosed" :font="IconType.Closed" />
            {{ topic.title }}
          </router-link>
        </div>
        <div class="col-date">
          <human-date :date="topic.createdUtc!" format="DD.MM.YYYY HH:mm" />
        </div>
        <div class="col-author">
          <user-link :user="topic.author!" />
        </div>
        <div class="col-comments">
          {{ topic.commentsCount }}
          <span v-if="topic.unreadCommentsCount" class="unread">
            (+{{ topic.unreadCommentsCount }})
          </span>
        </div>
        <div class="col-last">
          <template v-if="topic.lastComment">
            <user-link :user="topic.lastComment.author" />,
            <router-link
              :to="{
                name: 'topic',
                params: { id: topic.id, n: topic.commentsCount },
              }"
            >
              <human-timespan :date="topic.lastComment.createdUtc" />
            </router-link>
          </template>
          <span v-else class="no-comments">—</span>
        </div>
      </div>
    </template>
  </div>
</template>

<style scoped lang="sass">
@import "@/assets/styles/Themes"
@import "@/assets/styles/Tables"

.topics-table
  width: 100%
  +table

.topics-header,
.topics-row
  display: grid
  grid-template-columns: 1fr 130px 140px 80px 200px
  align-items: center
  +table-columns

.topics-header
  +table-header
  font-weight: normal

  .col-date,
  .col-author,
  .col-comments,
  .col-last
    text-align: center

.topics-row
  +table-row

  &.closed
    opacity: 0.7
    &.attached
      opacity: 1

  &.attached .col-title a
    font-weight: bold

  .col-title a
    color: $link
    &:hover
      color: $link-hover

  .col-date,
  .col-comments
    text-align: center

  .col-author
    text-align: center

  .col-last
    text-align: right
    color: $text-muted

.unread
  color: $heading

.no-comments
  color: $heading-alt

.topics-empty
  padding: $big
  text-align: center
</style>
