<script setup lang="ts">
/**
 * ProfileBlogsTable — the user's hosted blogs (owner or assistant) as a
 * table with client-side search. Blogs have no "player" role, so there is
 * no host/player toggle — the table covers hosted blogs, and the gray
 * reader-blogs line (ProfileBlogs) covers subscriptions separately.
 *
 * Blog titles link to the blog page ('blog' route).
 *
 * Four states: loading skeleton (DataTable) → error line → empty
 * (two-state) → content.
 */
import { computed, ref, onMounted, watch } from "vue";
import { DataTable, type Column } from "@/shared/ui/DataTable";
import { FilterSearchInput } from "@/shared/ui/Filters";
import { blogApi, BlogStatusBadge, type Blog } from "@/entities/blog";
import { highlightMatch } from "@/shared/lib/utils/highlight";
import HumanDate from "@/shared/ui/Date/HumanDate.vue";

const props = defineProps<{
  /** Profile owner whose hosted blogs we list. */
  username: string;
}>();

const allBlogs = ref<Blog[]>([]);
const loading = ref(true);
const error = ref(false);
const search = ref("");

async function fetchBlogs() {
  loading.value = true;
  error.value = false;
  const { data, error: apiError } = await blogApi.getBlogsByHost(
    props.username,
  );
  loading.value = false;
  if (apiError) {
    error.value = true;
    return;
  }
  allBlogs.value = data?.resources ?? [];
}

onMounted(fetchBlogs);
watch(() => props.username, fetchBlogs);

// Blog count per author is small, so search filters in the browser —
// keeps the contract narrow (no dedicated backend search param).
const blogs = computed(() => {
  const q = search.value.trim().toLowerCase();
  if (!q) return allBlogs.value;
  return allBlogs.value.filter((b) => b.title.toLowerCase().includes(q));
});

const hasActiveFilters = computed(() => search.value.trim().length > 0);

const emptyText = computed(() =>
  hasActiveFilters.value
    ? "Блогов по заданным фильтрам не найдено"
    : "Пользователь не ведет блогов",
);

const columns: Column[] = [
  { key: "title", label: "Название", width: "44%", align: "left" },
  { key: "status", label: "Статус", width: "20%", align: "left" },
  { key: "subscribers", label: "Читатели", width: "12%", align: "center" },
  {
    key: "created",
    label: "Дата создания",
    width: "24%",
    align: "left",
    hideOnMobile: true,
  },
];
</script>

<template>
  <div class="profile-blogs-table">
    <div class="controls">
      <FilterSearchInput
        v-model="search"
        placeholder="Поиск по названию"
        class="search"
      />
    </div>

    <div v-if="error" class="error-message">Не удалось загрузить блоги.</div>

    <DataTable
      v-if="!error || blogs.length > 0"
      :columns="columns"
      :data="blogs"
      :loading="loading"
      :empty-text="emptyText"
    >
      <template #cell-title="{ row }">
        <router-link
          class="blog-title"
          :to="{ name: 'blog', params: { id: row.id } }"
        >
          <span v-if="search" v-html="highlightMatch(row.title, search)"></span>
          <template v-else>{{ row.title }}</template>
        </router-link>
      </template>

      <template #cell-status="{ row }">
        <BlogStatusBadge :status="row.status" />
      </template>

      <template #cell-subscribers="{ row }">
        <span>{{ row.subscribersCount ?? 0 }}</span>
      </template>

      <template #cell-created="{ row }">
        <HumanDate :date="row.createdUtc" format="DD.MM.YYYY" />
      </template>
    </DataTable>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Themes"

.profile-blogs-table
  display: flex
  flex-direction: column
  gap: $medium

.controls
  display: flex
  flex-wrap: wrap
  align-items: center
  gap: $small

// Search fills all free space (same as the /blogs filter bar, where the
// search input is flex: 1).
.search
  flex: 1
  min-width: 200px

.error-message
  padding: $medium
  color: $text-on-red
  background-color: $bg-highlight-red
  border-radius: $border-radius

.blog-title
  color: $link
  &:hover
    color: $link-hover
</style>
