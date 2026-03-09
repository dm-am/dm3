<script setup lang="ts">
import { IconType } from "@/shared/ui/Icon/iconType";
import { useBoardsStore } from "@/entities/forum";
import { onMounted, watch, nextTick, ref } from "vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { storeToRefs } from "pinia";
import { initBbcodeInteractive } from "@/shared/lib/utils/bbcodeInteractive";

const store = useBoardsStore();
const { news } = storeToRefs(store);
const newsContainer = ref<HTMLElement | null>(null);

onMounted(() => store.fetchNews());

// Initialize interactive BBCode elements when news loads
watch(news, () => {
  nextTick(() => {
    if (newsContainer.value) {
      initBbcodeInteractive(newsContainer.value);
    }
  });
});
</script>

<template>
  <block-title>Последние новости</block-title>
  <secondary-text v-if="news && !news.length">Нет новостных тем</secondary-text>

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
  margin: $small 0
  +bbcode-content
</style>
