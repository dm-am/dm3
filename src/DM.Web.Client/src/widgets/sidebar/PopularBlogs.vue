<template>
  <SidebarBlock token="PopularBlogs">
    <template #title>Популярные блоги</template>
    <SidebarSkeleton
      v-if="store.popularBlogs === null && !store.popularBlogsError"
      :lines="10"
    />
    <SecondaryText v-else-if="store.popularBlogs === null">
      Не удалось загрузить.
      <button
        type="button"
        class="retry-link"
        @click="store.fetchPopularBlogs(true)"
      >
        Повторить
      </button>
    </SecondaryText>
    <SecondaryText v-else-if="store.popularBlogs.length === 0">
      Популярных блогов пока нет
    </SecondaryText>
    <template v-else>
      <BlogLink
        v-for="blog in store.popularBlogs"
        :key="blog.id"
        :blog="blog"
        :counters="true"
        :always-show-counters="!userStore.user"
      />
    </template>
    <DashSeparator spacing="tiny" width="75%" />
    <div>
      <span class="muted" aria-hidden="true">- </span>
      <router-link
        class="forward"
        :to="{
          name: 'blogs',
          query: { sortBy: 'popularity', sortOrder: 'desc' },
        }"
        >Все популярные блоги</router-link
      >
    </div>
  </SidebarBlock>
</template>

<script setup lang="ts">
import SidebarBlock from "./SidebarBlock.vue";
import SidebarSkeleton from "./SidebarSkeleton.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import BlogLink from "./BlogLink.vue";
import { useBlogsStore } from "@/entities/blog";
import { useUserStore } from "@/entities/user";
import { onMounted } from "vue";
import { DashSeparator } from "@/shared/ui/DashSeparator";

const store = useBlogsStore();
const userStore = useUserStore();

onMounted(() => store.fetchPopularBlogs());
</script>

<style scoped lang="sass">
@import "src/assets/styles/Inputs"

.muted
  color: $text-muted

.forward
  font-weight: bold

.retry-link
  +inline-link-button
</style>
