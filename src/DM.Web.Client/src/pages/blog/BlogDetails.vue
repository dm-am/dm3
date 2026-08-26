<script setup lang="ts">
// Blog info sub-page (mirrors GameDetails): a compact key-value info table,
// the rubric table, and the blog description — the old site's module-info
// layout, rendered with our components (dev doc 4.2.3.6.1 "Информация блога").
// The discussion block lives on its own page (BlogComments), linked from the
// BlogPanel.
import { computed } from "vue";
import { storeToRefs } from "pinia";
import {
  useBlogDetailsStore,
  BlogStatusBadge,
  type Rubric,
} from "@/entities/blog";
import { UserLink } from "@/entities/user";
import { ContentText } from "@/shared/ui/Content";
import { DataTable, type Column } from "@/shared/ui/DataTable";
import BlockTitle from "@/shared/ui/Layout/BlockTitle.vue";
import { DashSeparator } from "@/shared/ui/DashSeparator";
import { formatDate } from "@/shared/lib/utils/datetime";

const blogStore = useBlogDetailsStore();
const { blog, rubrics } = storeToRefs(blogStore);

// Feed/rubric links use the short public id (fall back to the GUID); the
// blog route is publicId-tolerant on the backend.
const blogId = computed(() => blog.value?.publicId ?? blog.value?.id ?? "");
const assistants = computed(() => blog.value?.assistants ?? []);

const rubricColumns: Column[] = [
  { key: "title", label: "Рубрика", align: "left" },
  { key: "publications", label: "Публикаций", align: "center" },
];

function rubricTo(r: Rubric) {
  return {
    name: "blog-feed",
    params: { id: blogId.value },
    query: { rubric: r.id },
  };
}
</script>

<template>
  <div v-if="blog" class="blog-details">
    <!-- Key-value info table -->
    <table class="info-table">
      <tbody>
        <tr>
          <th>Статус</th>
          <td><BlogStatusBadge :status="blog.status" /></td>
        </tr>
        <tr>
          <th>Автор</th>
          <td><UserLink :user="blog.author" hide-badge /></td>
        </tr>
        <tr>
          <th>{{ assistants.length > 1 ? "Ассистенты" : "Ассистент" }}</th>
          <td>
            <template v-if="assistants.length"
              ><template v-for="(a, i) in assistants" :key="a.id"
                ><UserLink :user="a" hide-badge /><span
                  v-if="i < assistants.length - 1"
                  >,
                </span></template
              ></template
            >
            <span v-else class="muted">нет</span>
          </td>
        </tr>
        <tr v-if="blog.mentor">
          <th>Наставник</th>
          <td><UserLink :user="blog.mentor" hide-badge /></td>
        </tr>
        <tr>
          <th>Дата создания</th>
          <td>{{ formatDate(blog.createdUtc) }}</td>
        </tr>
        <tr>
          <th>Публикаций</th>
          <td>{{ blog.publicationCount }}</td>
        </tr>
        <tr>
          <th>Подписчиков</th>
          <td>{{ blog.subscribersCount }}</td>
        </tr>
      </tbody>
    </table>

    <!-- Rubrics -->
    <section v-if="rubrics.length" class="rubrics">
      <BlockTitle>Рубрики</BlockTitle>
      <DataTable
        :columns="rubricColumns"
        :data="rubrics"
        :show-row-numbers="true"
        empty-text="Рубрик пока нет"
      >
        <template #cell-title="{ row }">
          <router-link class="rubric-link" :to="rubricTo(row)">{{
            row.title
          }}</router-link>
        </template>
        <template #cell-publications="{ row }">{{
          row.publicationCount
        }}</template>
      </DataTable>
    </section>

    <!-- Description (BBCode, server-rendered) -->
    <section v-if="blog.description" class="description">
      <DashSeparator />
      <ContentText :html="blog.description" />
    </section>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Tables" as *

.blog-details
  display: flex
  flex-direction: column
  gap: $big

.info-table
  +info-table

.rubrics
  display: flex
  flex-direction: column
  gap: $small

.muted
  color: $text-muted

.rubric-link
  color: $link
  &:hover
    color: $link-hover
</style>
