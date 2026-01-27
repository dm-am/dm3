<script setup lang="ts">
import { IconType } from "@/components/icons/iconType";
import { useBoardsStore } from "@/stores";
import { onMounted, watch, nextTick, ref } from "vue";
import SecondaryText from "@/components/layout/SecondaryText.vue";
import { storeToRefs } from "pinia";
import { initBbcodeInteractive } from "@/utils/bbcodeInteractive";

const store = useBoardsStore();
const { news } = storeToRefs(store);
const newsContainer = ref<HTMLElement | null>(null);

onMounted(() => store.fetchNews());

// Initialize interactive BBCode elements when news loads
watch(news, () => {
  nextTick(() => {
    initBbcodeInteractive(newsContainer.value);
  });
});
</script>

<template>
  <block-title>Последние новости</block-title>
  <the-loader v-if="!news" />
  <secondary-text v-else-if="!news.length">Ничего нового</secondary-text>

  <div v-else ref="newsContainer">
    <div v-for="article in news" :key="article.id" class="article">
      <router-link
        class="article-title"
        :to="{ name: 'topic', params: { id: article.id } }"
      >
        {{ article.title }}
      </router-link>
      <div class="article-description" v-html="article.description"></div>
      <div>
        <user-link :user="article.author!" />,
        <human-timespan :date="article.createdUtc!" />&nbsp;<the-icon
          :font="IconType.CommentsNoUnread"
        />
      </div>
    </div>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"
@import "src/assets/styles/BbcodeContent"

.article
  margin: $medium 0

.article-title
  font-weight: bold

.article-description
  +bbcode-content
  margin: $small 0
</style>
