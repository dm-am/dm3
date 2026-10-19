<script setup lang="ts">
// The blog's facts, its rubrics and its description, the old site's module-info layout.
import { computed } from "vue";
import { storeToRefs } from "pinia";
import {
  useBlogDetailsStore,
  BlogStatusBadge,
  type Rubric,
} from "@/entities/blog";
import { RemovedAccountLink, UserLink } from "@/entities/user";
import { ContentText } from "@/shared/ui/Content";
import { DataTable, type Column } from "@/shared/ui/DataTable";
import BlockTitle from "@/shared/ui/Layout/BlockTitle.vue";
import { DashSeparator } from "@/shared/ui/DashSeparator";
import { formatDate } from "@/shared/lib/utils/datetime";

const blogStore = useBlogDetailsStore();
const { blog, rubrics } = storeToRefs(blogStore);

// The short public id, else the GUID; the blog route takes either.
const blogId = computed(() => blog.value?.publicId ?? blog.value?.id ?? "");
const assistants = computed(() => blog.value?.assistants ?? []);

const rubricColumns: Column[] = [
  { key: "title", label: "Рубрика", align: "left" },
  { key: "publications", label: "Публикаций", width: "50%", align: "center" },
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
    <table class="info-table">
      <tbody>
        <tr>
          <th>Статус</th>
          <td><BlogStatusBadge :status="blog.status" /></td>
        </tr>
        <tr>
          <th>Автор</th>
          <td>
            <UserLink v-if="blog.author" :user="blog.author" hide-badge />
            <RemovedAccountLink v-else />
          </td>
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

    <section v-if="rubrics.length" class="rubrics">
      <BlockTitle>Рубрики</BlockTitle>
      <DataTable
        :columns="rubricColumns"
        :data="rubrics"
        :show-row-numbers="true"
        empty-text="Рубрик пока нет"
      >
        <template #cell-title="{ row }">
          <router-link :to="rubricTo(row)">{{ row.title }}</router-link>
        </template>
        <template #cell-publications="{ row }">{{
          row.publicationCount
        }}</template>
      </DataTable>
    </section>

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
</style>
