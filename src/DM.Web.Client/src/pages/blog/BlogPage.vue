<script setup lang="ts">
// Thin blog layout (mirrors GamePage): blog header (H1 + status + meta) and
// a <router-view> for the active sub-page. All per-blog navigation and
// actions live in the left-sidebar BlogPanel; role flags are lifted into
// the shared useBlogDetailsStore (SSOT).
import { computed, onUnmounted } from "vue";
import { useRoute } from "vue-router";
import { storeToRefs } from "pinia";
import { useBlogDetailsStore, BlogStatusBadge } from "@/entities/blog";
import { useFetchData } from "@/shared/lib/composables/useFetchData";
import PageTitle from "@/shared/ui/Layout/PageTitle.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { UserLink } from "@/entities/user";

const route = useRoute();
const blogStore = useBlogDetailsStore();
const { blog, blogError } = storeToRefs(blogStore);

const blogId = computed(() => route.params.id as string);

const statusClass = computed(() => {
  if (!blog.value) return "";
  switch (blog.value.status) {
    case "Active":
      return "status-active";
    case "Closed":
      return "status-finished";
    default:
      return "";
  }
});

const assistants = computed(() => blog.value?.assistants ?? []);

// Premoderation status (newbie blogs) — hidden when "Approved" / absent
// (the blog does not require premoderation). Doc 4.2.2.15.
const premodLabel = computed(() => {
  switch (blog.value?.premoderationStatus) {
    case "AwaitingApproval":
      return "Ожидает проверки";
    case "AwaitingEdits":
      return "Требует правок";
    default:
      return null;
  }
});

useFetchData(
  () => blogStore.loadBlog(blogId.value),
  [
    {
      param: (p) => p.id,
      callback: (id) => {
        // Same as the game shell: the detail store is not keyed by id and the
        // route record is shared, so without wiping first the previous blog's
        // publications and comments stay under the new title.
        blogStore.reset();
        return blogStore.loadBlog(id as string);
      },
    },
  ],
);

onUnmounted(() => {
  blogStore.reset();
});
</script>

<template>
  <template v-if="blog">
    <div class="blog-header">
      <div class="blog-title-row">
        <page-title>{{ blog.title }}</page-title>
        <span :class="['blog-status', statusClass]">
          <BlogStatusBadge :status="blog.status" />
        </span>
        <span v-if="premodLabel" class="blog-premod">{{ premodLabel }}</span>
      </div>
      <secondary-text class="blog-meta">
        <span class="blog-author">
          Автор: <user-link :user="blog.author" />
        </span>
        <span v-if="assistants.length" class="blog-assistant">
          {{ assistants.length === 1 ? "Ассистент" : "Ассистенты" }}:
          <template
            v-for="(assistant, index) in assistants"
            :key="assistant.id"
          >
            <user-link :user="assistant" /><span
              v-if="index < assistants.length - 1"
              >,
            </span>
          </template>
        </span>
        <span class="blog-readers">Читатели: {{ blog.subscribersCount }}</span>
      </secondary-text>
    </div>

    <router-view />
  </template>

  <div v-else-if="blogError" class="blog-error">
    <p>{{ blogError }}</p>
    <router-link :to="{ name: 'blogs' }">Вернуться к списку блогов</router-link>
  </div>
</template>

<style scoped lang="sass">
.blog-header
  margin-bottom: $medium

.blog-title-row
  display: flex
  align-items: baseline
  gap: $medium
  flex-wrap: wrap

.blog-status
  font-size: $secondary-font-size
  padding: 2px $small
  border-radius: $border-radius
  background-color: $bg-element
  color: $text-muted

  &.status-active
    background-color: rgba($accent-green, 0.2)
    color: $accent-green

  &.status-finished
    background-color: rgba($text-muted, 0.2)
    color: $text-muted

.blog-premod
  font-size: $secondary-font-size
  padding: 2px $small
  border-radius: $border-radius
  background-color: rgba($accent-red, 0.15)
  color: $accent-red

.blog-meta
  display: flex
  flex-wrap: wrap
  gap: $small
  margin-top: $tiny

.blog-author,
.blog-assistant,
.blog-readers
  margin-right: $medium

.blog-error
  padding: $big
  text-align: center
  color: $accent-red

  a
    color: $link
    margin-top: $small
    display: inline-block
</style>
