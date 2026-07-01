<template>
  <SidebarBlock token="PopularBlogs">
    <template #title>Популярные блоги</template>
    <SidebarSkeleton
      v-if="store.popularBlogs === null && !store.popularBlogsError"
      :lines="10"
    />
    <SecondaryText v-else-if="store.popularBlogs === null">
      Не удалось загрузить
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
    <div class="separator">
      - - - - - - - - - - - - - - - - - - - - - - - - - -
    </div>
    <div>
      <span class="muted">- </span>
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

const store = useBlogsStore();
const userStore = useUserStore();

onMounted(() => store.fetchPopularBlogs());
</script>

<style scoped lang="sass">
@import "src/assets/styles/Themes"

.muted
  color: $text-muted

.forward
  font-weight: bold

.separator
  color: $text-muted
</style>
