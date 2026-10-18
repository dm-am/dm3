<script setup lang="ts">
/**
 * PublicationEdit — edit an existing publication (dev doc 4.2.3.6.8
 * "Редактирование публикации"). Form: title, rubric, content; "Сохранить"
 * patches and opens the publication itself, "Удалить" deletes with a
 * confirmation and returns to the rubric feed the publication lay in,
 * "Отмена" returns to the blog feed. Access is gated to owner + assistant
 * (backend-enforced). Anybody else, a guest included, gets the site's own
 * "not found" at this address, as on the settings of the blog, and not a page
 * with a refusal or an invitation to sign in on it: the links here are drawn
 * only for the owner and the assistants. The way out waits for the answer
 * about the session (restoreSession), since a session the browser kept no
 * copy of is named by that answer alone, and deciding before it would send an
 * author away from his own text.
 *
 * Content round-trip caveat: the API serves publication content as
 * server-rendered HTML (CommonBbText), not the raw BBCode source. The
 * editor is seeded through htmlToBbcode (the editor's own reverse
 * conversion) and the content is included in the PATCH only when the
 * author actually changed it, so an untouched publication is never
 * rewritten with a lossy round-trip.
 */
import { computed, ref, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import { storeToRefs } from "pinia";
import { useBlogDetailsStore, blogApi } from "@/entities/blog";
import type { Publication, UpdatePublicationInput } from "@/entities/blog";
import { restoreSession } from "@/entities/user";
import { htmlToBbcode } from "@/shared/lib/utils/bbcode";
import { PublicationForm } from "@/features/publication";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import Button from "@/shared/ui/Button/Button.vue";
import { ErrorState } from "@/shared/ui/ErrorState";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { useFetchData } from "@/shared/lib/composables/useFetchData";
import { useActionResult } from "@/shared/lib/composables/useActionResult";
import { UnsavedChangesGuard } from "@/shared/ui/UnsavedChangesGuard";

const route = useRoute();
const router = useRouter();
const blogStore = useBlogDetailsStore();
const { rubrics, canManage } = storeToRefs(blogStore);

const blogId = computed(() => route.params.id as string);
const pubId = computed(() => route.params.pubId as string);

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

const NO_RUBRIC = "";
const rubricOptions = computed(() => [
  { value: NO_RUBRIC, label: "Без рубрики" },
  ...rubrics.value.map((r) => ({ value: r.id, label: r.title })),
]);

// --- Publication load (local slice: the shared store holds the feed, not a
// single publication) ---
const publication = ref<Publication | null>(null);
const loading = ref(false);
const loadError = ref<string | null>(null);

const title = ref("");
const rubricId = ref<string>(NO_RUBRIC);
const content = ref("");
// The as-seeded editor value; content is PATCHed only when it diverges.
const initialContent = ref("");

async function load() {
  loading.value = true;
  loadError.value = null;
  const { data, error } = await blogApi.getPublication(pubId.value);
  if (error || !data?.resource) {
    loadError.value = "Не удалось загрузить публикацию";
    publication.value = null;
  } else {
    publication.value = data.resource;
    title.value = data.resource.title;
    rubricId.value = data.resource.rubric?.id ?? NO_RUBRIC;
    initialContent.value = htmlToBbcode(data.resource.content);
    content.value = initialContent.value;
  }
  loading.value = false;
}

useFetchData(load, [{ param: (p) => p.pubId, callback: () => load() }]);

const valid = computed(
  () => title.value.trim().length > 0 && content.value.trim().length > 0,
);

const saving = ref(false);
// Released on a successful save or delete: both navigate away themselves.
const saved = ref(false);

// The edit form carries no draft-key on purpose (a draft of an edit would
// resurrect itself over the published text later), so leaving the page is the
// only thing standing between the author and losing the edit.
const dirty = computed(
  () =>
    !saved.value &&
    !!publication.value &&
    (title.value !== publication.value.title ||
      rubricId.value !== (publication.value.rubric?.id ?? NO_RUBRIC) ||
      content.value !== initialContent.value),
);

/** A refused submit, said over the footer of the form. */
const saveResult = useActionResult();

async function save() {
  if (!publication.value || !valid.value || saving.value) return;
  saving.value = true;
  saveResult.clear();

  const patch: UpdatePublicationInput = {
    title: title.value.trim(),
  };
  if (rubricId.value) {
    patch.rubricId = rubricId.value;
  } else if (publication.value.rubric) {
    patch.clearRubric = true;
  }
  if (content.value !== initialContent.value) {
    patch.content = content.value;
  }

  const { error } = await blogApi.updatePublication(pubId.value, patch);
  saving.value = false;
  if (error) {
    saveResult.refuse(error, "Не удалось сохранить публикацию");
    return;
  }
  saved.value = true;
  // No success toast: the navigation below opens the publication itself, and
  // the saved title, rubric and text are what the author lands on. Put the
  // toast back only if this page ever stops leaving for the result.
  router.push({
    name: "blog-publication",
    params: { id: blogId.value, pubId: pubId.value },
  });
}

// --- Delete (with confirmation) ---
const confirmingDelete = ref(false);

// The request of the confirmation. The window waits for it, prints a refusal in
// itself and closes on success alone, so the interceptor keeps quiet about it.
async function confirmDelete() {
  if (!publication.value) return;
  const result = await blogApi.deletePublication(pubId.value, {
    ownsRefusal: true,
    ownsRateLimit: true,
  });
  if (result.error) return result;
  saved.value = true;
  // The rubric the publication lay in, taken from the loaded publication and
  // not from the form: the author may have picked another rubric in the select
  // without saving, and the feed to come back to is the one it was really in.
  const deletedFrom = publication.value.rubric?.id;
  blogStore.loadBlog(blogId.value);
  // No success toast: the navigation below lands on that feed, which reloads
  // without the publication — the gap is the confirmation. Put the toast back
  // only if this page ever stops leaving for the result.
  router.push({
    name: "blog-feed",
    params: { id: blogId.value },
    query: deletedFrom ? { rubric: deletedFrom } : {},
  });
  return result;
}

function cancel() {
  router.push({ name: "blog-feed", params: { id: blogId.value } });
}
</script>

<template>
  <div class="publication-edit">
    <!-- Anybody but the owner and the assistants is sent to "not found". -->
    <template v-if="canManage">
      <!-- 1. loading (no form skeleton exists yet; the in-zone pattern for
           form-like pages is the plain hint, see GameNotepad) -->
      <secondary-text v-if="loading && !publication">
        Загрузка...
      </secondary-text>

      <!-- 2. error -->
      <ErrorState v-else-if="loadError" :message="loadError" :retry="load" />

      <!-- 3. content -->
      <template v-else-if="publication">
        <PublicationForm
          v-model:title="title"
          v-model:rubric-id="rubricId"
          v-model:content="content"
          :rubric-options="rubricOptions"
          :valid="valid"
          :loading="saving"
          :refusal="saveResult.refusal.value"
          action="Сохранить"
          cancel-label="Отмена"
          @submit="save"
          @cancel="cancel"
        />

        <div class="danger-row">
          <Button
            type="button"
            class="delete-publication"
            @click="confirmingDelete = true"
          >
            Удалить публикацию
          </Button>
        </div>

        <UnsavedChangesGuard :dirty="dirty" />
      </template>

      <!-- 4. empty / not found -->
      <secondary-text v-else>Публикация не найдена</secondary-text>
    </template>

    <ConfirmDialog
      v-model:show="confirmingDelete"
      title="Удалить публикацию?"
      message="Публикация будет удалена без возможности восстановления"
      confirm-label="Удалить"
      failure="Не удалось удалить публикацию"
      :action="confirmDelete"
    />
  </div>
</template>

<style scoped lang="sass">
.publication-edit
  max-width: $grid-step * 200

.danger-row
  margin-top: $big
</style>
