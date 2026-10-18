<script setup lang="ts">
/**
 * PublicationCreate — create a new publication in the blog (dev doc
 * 4.2.3.6.7 "Создание публикации"). Form: title (required), rubric (optional
 * select over the blog's rubrics), content (BBCode editor). "Опубликовать"
 * creates and publishes immediately; "Отмена" returns to the blog feed.
 * Access is gated to owner + assistant; the backend enforces this too.
 *
 * Anybody else, a guest included, gets the site's own "not found" at this
 * address, as on the settings of the blog, and not a page with a refusal or an
 * invitation to sign in on it: the link here is drawn only for the owner and
 * the assistants. The way out waits for the answer about the session
 * (restoreSession), since a session the browser kept no copy of is named by
 * that answer alone, and deciding before it would send an author away from
 * his blog.
 */
import { computed, ref, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import { storeToRefs } from "pinia";
import { useBlogDetailsStore, blogApi } from "@/entities/blog";
import { restoreSession } from "@/entities/user";
import { PublicationForm } from "@/features/publication";
import { composerDraftKey } from "@/shared/lib/utils/draftKey";
import { useActionResult } from "@/shared/lib/composables/useActionResult";

const route = useRoute();
const router = useRouter();
const blogStore = useBlogDetailsStore();
const { blog, rubrics, canManage } = storeToRefs(blogStore);

const blogId = computed(() => route.params.id as string);

const sessionSettled = ref(false);
const settle = () => (sessionSettled.value = true);
restoreSession().then(settle, settle);

watch(
  () => sessionSettled.value && !canManage.value,
  (refused) => {
    if (!refused) return;
    void router.replace({
      name: "not-found",
      params: { pathMatch: route.path.slice(1).split("/") },
      query: route.query,
      hash: route.hash,
    });
  },
  { immediate: true, flush: "post" },
);

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
const formRef = ref<InstanceType<typeof PublicationForm> | null>(null);

const valid = computed(
  () => title.value.trim().length > 0 && content.value.trim().length > 0,
);

/** A refused submit, said over the footer of the form. */
const saveResult = useActionResult();

async function publish() {
  // Validity gates the form, not this handler: the page hands `valid` down and
  // the form keeps its action disabled while it is false, so a submit that
  // arrives here is one the author was allowed to make. The second check that
  // used to stand here answered the form's own event with silence.
  if (saving.value) return;
  saving.value = true;
  saveResult.clear();
  const { data, error } = await blogApi.createPublication(blogId.value, {
    title: title.value.trim(),
    content: content.value,
    rubricId: rubricId.value || null,
    publishImmediately: true,
  });
  saving.value = false;
  if (error) {
    saveResult.refuse(error, "Не удалось создать публикацию");
    return;
  }
  // No success toast: the redirect below lands on the blog feed, which
  // reloads and carries the fresh publication first.

  // The text is published, so the draft of it is no longer a draft. It lived
  // seven days, and the next visit to this page offered to restore an already
  // published record — accepting posted it twice. Every other composer clears
  // its draft here; this one navigated away instead.
  formRef.value?.clearDraft();
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
