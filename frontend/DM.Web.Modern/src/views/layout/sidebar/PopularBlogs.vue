<template>
  <menu-block token="PopularBlogs">
    <template #title>Популярные блоги</template>
    <template v-if="!store.popularBlogs || store.popularBlogs.length === 0">
      <secondary-text>Нет блогов с читателями</secondary-text>
    </template>
    <template v-else>
      <div v-for="blog in store.popularBlogs" :key="blog.id" class="blog-item">
        <span class="muted">- </span>
        <router-link
          :to="{ name: 'blogs' }"
          :title="`${blog.owner?.login} | Читателей: ${blog.subscribersCount ?? 0}`"
        >
          {{ blog.title }}
        </router-link>
        <span class="counters">
          <span class="muted"> (</span>{{ blog.publicationCount || 0 }}<span class="muted">/</span>{{ blog.commentsCount || 0 }}<span class="muted">)</span>
        </span>
      </div>
    </template>
  </menu-block>
</template>

<script setup lang="ts">
import MenuBlock from "@/views/layout/MenuBlock.vue";
import SecondaryText from "@/components/layout/SecondaryText.vue";
import { useBlogsStore } from "@/stores/blogs";
import { onMounted } from "vue";

const store = useBlogsStore();

onMounted(() => store.fetchPopularBlogs());
</script>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.blog-item
  margin-bottom: $tiny

  &:last-child
    margin-bottom: 0

.muted
  color: $text-muted

.counters .muted
  user-select: text
</style>
