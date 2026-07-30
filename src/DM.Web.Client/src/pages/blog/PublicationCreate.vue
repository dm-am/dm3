<script setup lang="ts">
/**
 * PublicationCreate — create a new publication in the blog (dev doc
 * 4.2.3.6.7 "Создание публикации"). Form: title (required), rubric (optional
 * select over the blog's rubrics), content (BBCode editor). "Опубликовать"
 * creates and publishes immediately; "Отмена" returns to the blog feed.
 * Access is gated to owner + assistant; the backend enforces this too.
 */
import { computed, ref } from "vue";
import { useRoute, useRouter } from "vue-router";
import { storeToRefs } from "pinia";
import { useBlogDetailsStore, blogApi } from "@/entities/blog";
import { PublicationForm } from "@/features/publication";
import PageTitle from "@/shared/ui/Layout/PageTitle.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { useToast } from "@/shared/lib/composables/useToast";
import { notifyFailure } from "@/shared/lib/errors";

const route = useRoute();
const router = useRouter();
const toast = useToast();
const blogStore = useBlogDetailsStore();
const { blog, rubrics, canManage } = storeToRefs(blogStore);

const blogId = computed(() => route.params.id as string);

// "Без рубрики" sentinel: the Select needs a concrete value; empty string
// maps to "no rubricId" in the payload.
const NO_RUBRIC = "";
const rubricOptions = computed(() => [
  { value: NO_RUBRIC, label: "Без рубрики" },
  ...rubrics.value.map((r) => ({ value: r.id, label: r.title })),
]);

const title = ref("");
const rubricId = ref<string>(NO_RUBRIC);
const content = ref("");
const saving = ref(false);

const valid = computed(
  () => title.value.trim().length > 0 && content.value.trim().length > 0,
);

async function publish() {
  if (!valid.value || saving.value) return;
  saving.value = true;
  const { data, error } = await blogApi.createPublication(blogId.value, {
    title: title.value.trim(),
    content: content.value,
    rubricId: rubricId.value || null,
    publishImmediately: true,
  });
  saving.value = false;
  if (error) {
    notifyFailure(error, "Не удалось создать публикацию");
    return;
  }
  toast.success("Публикация создана");
  // Refresh the blog so counters and the sidebar stay in sync.
  if (blog.value) blogStore.loadBlog(blogId.value);
  const createdRubric = data?.resource?.rubric?.id;
  router.push({
    name: "blog-feed",
    params: { id: blogId.value },
    query: createdRubric ? { rubric: createdRubric } : {},
  });
}

function cancel() {
  router.push({ name: "blog-feed", params: { id: blogId.value } });
}
</script>

<template>
  <div class="publication-create">
    <page-title>Создание публикации</page-title>

    <secondary-text v-if="!canManage">
      Создание публикаций доступно мастеру блога и ассистентам.
    </secondary-text>

    <PublicationForm
      v-else
      v-model:title="title"
      v-model:rubric-id="rubricId"
      v-model:content="content"
      :rubric-options="rubricOptions"
      :valid="valid"
      :loading="saving"
      action="Опубликовать"
      cancel-label="Отмена"
      :draft-key="`blog_${blogId}_publication_new`"
      @submit="publish"
      @cancel="cancel"
    />
  </div>
</template>

<style scoped lang="sass">
.publication-create
  max-width: $grid-step * 200
</style>
