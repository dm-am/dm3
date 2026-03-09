<script setup lang="ts">
import { ref, onMounted, watch } from "vue";
import type { Blog } from "@/entities/blog";
import { blogApi } from "@/entities/blog";

const props = defineProps<{
  username: string;
}>();

const blogs = ref<Blog[]>([]);
const loading = ref(true);

async function fetchBlogs() {
  loading.value = true;
  const { data } = await blogApi.getUserBlogs(props.username);
  blogs.value = data?.resources || [];
  loading.value = false;
}

onMounted(fetchBlogs);
watch(() => props.username, fetchBlogs);
</script>

<template>
  <section v-if="blogs.length > 0 || loading" class="profile-blogs">
    <h3 class="section-title">Блоги</h3>

    <div v-if="loading" class="loading">Загрузка...</div>

    <div v-else class="blogs-list">
      <div v-for="blog in blogs" :key="blog.id" class="blog-item">
        <span class="blog-title">{{ blog.title }}</span>
        <span v-if="blog.publicationCount" class="blog-count">
          {{ blog.publicationCount }} публикаций
        </span>
      </div>
    </div>
  </section>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.profile-blogs
  background: $bg-element
  border-radius: $border-radius
  padding: $medium
  margin-bottom: $medium

.section-title
  color: $text
  margin: 0 0 $medium
  font-size: 1rem

.loading
  color: $text-muted
  font-size: $secondary-font-size

.blogs-list
  display: flex
  flex-direction: column
  gap: $tiny

.blog-item
  display: flex
  justify-content: space-between
  align-items: center
  padding: $small
  background: $bg-element-overlay
  border-radius: $border-radius

.blog-title
  color: $text
  font-weight: 500

.blog-count
  font-size: $secondary-font-size
  color: $text-muted
</style>
