<script setup lang="ts">
import { ref, onMounted, watch } from "vue";
import type { UserLogin, BestPost } from "@/api/models/community";
import communityApi from "@/api/requests/communityApi";
import SecondaryText from "@/components/layout/SecondaryText.vue";
import dayjs from "dayjs";

const props = defineProps<{
  login: UserLogin;
}>();

const bestPost = ref<BestPost | null>(null);
const loading = ref(true);
const isExpanded = ref(false);

async function fetchBestPost() {
  loading.value = true;
  const { data } = await communityApi.getBestPost(props.login);
  bestPost.value = data?.resource || null;
  loading.value = false;
}

onMounted(fetchBestPost);
watch(() => props.login, fetchBestPost);
</script>

<template>
  <section v-if="bestPost || loading" class="profile-best-post">
    <div class="section-header" @click="isExpanded = !isExpanded">
      <h3 class="section-title">Лучший пост</h3>
      <button class="toggle-btn" :class="{ expanded: isExpanded }">
        {{ isExpanded ? "Свернуть" : "Развернуть" }}
      </button>
    </div>

    <div v-if="loading" class="loading">Загрузка...</div>

    <template v-else-if="bestPost">
      <div class="post-meta">
        <router-link
          :to="{ name: 'game', params: { id: bestPost.gameId } }"
          class="game-link"
        >
          {{ bestPost.gameTitle }}
        </router-link>
        <secondary-text>{{ bestPost.roomTitle }}</secondary-text>
        <span class="post-rating" :class="{ positive: bestPost.rating > 0 }">
          +{{ bestPost.rating }}
        </span>
      </div>

      <div
        v-show="isExpanded"
        class="post-content"
        v-html="bestPost.text"
      />

      <secondary-text v-show="isExpanded" class="post-date">
        {{ dayjs(bestPost.createdUtc).format("DD.MM.YYYY HH:mm") }}
      </secondary-text>
    </template>
  </section>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"
@import "src/assets/styles/BbcodeContent"

.profile-best-post
  background: $bg-element
  border-radius: $border-radius
  padding: $medium
  margin-bottom: $medium

.section-header
  display: flex
  justify-content: space-between
  align-items: center
  cursor: pointer

.section-title
  color: $text
  margin: 0
  font-size: 1rem

.toggle-btn
  background: none
  border: none
  color: $link
  font-size: $secondary-font-size
  cursor: pointer
  padding: 0

  &:hover
    color: $link-hover

.loading
  color: $text-muted
  font-size: $secondary-font-size
  margin-top: $small

.post-meta
  display: flex
  align-items: center
  gap: $small
  margin-top: $small

.game-link
  color: $link
  text-decoration: none
  font-weight: bold

  &:hover
    color: $link-hover

.post-rating
  margin-left: auto
  font-weight: bold
  color: $text-muted

  &.positive
    color: $accent-green

.post-content
  margin-top: $medium
  max-height: $grid-step * 80
  overflow: hidden
  position: relative
  +bbcode-content

  &::after
    content: ''
    position: absolute
    bottom: 0
    left: 0
    right: 0
    height: $big
    background: linear-gradient(transparent, $bg-element)

.post-date
  margin-top: $small
  display: block
</style>
