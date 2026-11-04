<script setup lang="ts">
/**
 * A new publication of the blog, for the owner and the assistants; anybody
 * else gets the site's "not found", decided after restoreSession, since
 * deciding before it would send an author away from his blog.
 */
import { computed, ref } from "vue";
import { useRoute, useRouter } from "vue-router";
import { storeToRefs } from "pinia";
import { useBlogDetailsStore, blogApi } from "@/entities/blog";
import { restoreSession } from "@/entities/user";
import { PublicationForm } from "@/features/publication";
import { composerDraftKey } from "@/shared/lib/utils/draftKey";
import { useActionResult } from "@/shared/lib/composables/useActionResult";
import { useNotFoundWhen } from "@/shared/lib/composables/useNotFoundWhen";

const route = useRoute();
const router = useRouter();
const blogStore = useBlogDetailsStore();
const { blog, rubrics, canManage } = storeToRefs(blogStore);

const blogId = computed(() => route.params.id as string);

const sessionSettled = ref(false);
const settle = () => (sessionSettled.value = true);
restoreSession().then(settle, settle);

useNotFoundWhen(() => sessionSettled.value && !canManage.value);

// "Без рубрики": the Select needs a value, and "" sends no rubricId.
const NO_RUBRIC = "";
const rubricOptions = computed(() => [
  { value: NO_RUBRIC, label: "Без рубрики" },
  ...rubrics.value.map((r) => ({ value: r.id, label: r.title })),
]);

const title = ref("");
const rubricId = ref<string>(NO_RUBRIC);
const content = ref("");
const saving = ref(false);
const formRef = ref<InstanceType<typeof PublicationForm> | null>(null);

const valid = computed(
  () => title.value.trim().length > 0 && content.value.trim().length > 0,
);

const saveResult = useActionResult();

async function publish() {
  // Validity gates the form, which keeps its action disabled while invalid.
  if (saving.value) return;
  saving.value = true;
  saveResult.clear();
  const { data, error } = await blogApi.createPublication(blogId.value, {
    title: title.value,
    content: content.value,
    rubricId: rubricId.value || null,
  });
  saving.value = false;
  if (error) {
    saveResult.refuse(error, "Не удалось создать публикацию");
    return;
  }
  // No success toast: the blog feed this lands on carries the publication first.

  // Sent, so the composer draft goes: a restored one would post it twice.
  formRef.value?.clearDraft();
  // The counters and the sidebar follow the blog.
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
    <PublicationForm
      v-if="canManage"
      ref="formRef"
      v-model:title="title"
      v-model:rubric-id="rubricId"
      v-model:content="content"
      :rubric-options="rubricOptions"
      :valid="valid"
      :loading="saving"
      :refusal="saveResult.refusal.value"
      action="Опубликовать"
      cancel-label="Отмена"
      :draft-key="composerDraftKey('blog', 'publication', blogId)"
      @submit="publish"
      @cancel="cancel"
    />
  </div>
</template>

<style scoped lang="sass">
.publication-create
  max-width: $grid-step * 200
</style>
