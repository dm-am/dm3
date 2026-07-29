<script setup lang="ts">
/**
 * PublicationEdit — edit an existing publication (dev doc 4.2.3.6.8
 * "Редактирование публикации"). Form: title, rubric, content; "Сохранить"
 * patches, "Удалить" deletes with a confirmation, "Отменить" returns to
 * the blog feed. Access is gated to owner + assistant (backend-enforced).
 *
 * Content round-trip caveat: the API serves publication content as
 * server-rendered HTML (CommonBbText), not the raw BBCode source. The
 * editor is seeded through htmlToBbcode (the editor's own reverse
 * conversion) and the content is included in the PATCH only when the
 * author actually changed it, so an untouched publication is never
 * rewritten with a lossy round-trip.
 */
import { computed, ref } from "vue";
import { useRoute, useRouter } from "vue-router";
import { storeToRefs } from "pinia";
import { useBlogDetailsStore, blogApi } from "@/entities/blog";
import type { Publication, UpdatePublicationInput } from "@/entities/blog";
import { htmlToBbcode } from "@/shared/lib/utils/bbcode";
import { PublicationForm } from "@/features/publication";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { ErrorState } from "@/shared/ui/ErrorState";
import PageTitle from "@/shared/ui/Layout/PageTitle.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { useFetchData } from "@/shared/lib/composables/useFetchData";
import { useToast } from "@/shared/lib/composables/useToast";

const route = useRoute();
const router = useRouter();
const toast = useToast();
const blogStore = useBlogDetailsStore();
const { rubrics, canManage } = storeToRefs(blogStore);

const blogId = computed(() => route.params.id as string);
const pubId = computed(() => route.params.pubId as string);

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

async function save() {
  if (!publication.value || !valid.value || saving.value) return;
  saving.value = true;

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
    toast.error("Не удалось сохранить публикацию");
    return;
  }
  toast.success("Публикация сохранена");
  router.push({ name: "blog-feed", params: { id: blogId.value } });
}

// --- Delete (with confirmation) ---
const confirmingDelete = ref(false);

async function confirmDelete() {
  confirmingDelete.value = false;
  if (!publication.value) return;
  const { error } = await blogApi.deletePublication(pubId.value);
  if (error) {
    toast.error("Не удалось удалить публикацию");
    return;
  }
  toast.success("Публикация удалена");
  blogStore.loadBlog(blogId.value);
  router.push({ name: "blog-feed", params: { id: blogId.value } });
}

function cancel() {
  router.push({ name: "blog-feed", params: { id: blogId.value } });
}
</script>

<template>
  <div class="publication-edit">
    <page-title>Редактирование публикации</page-title>

    <secondary-text v-if="!canManage">
      Редактирование публикаций доступно мастеру блога и ассистентам.
    </secondary-text>

    <!-- 1. loading (no form skeleton exists yet; the in-zone pattern for
         form-like pages is the plain hint, see GameNotepad) -->
    <secondary-text v-else-if="loading && !publication">
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
        action="Сохранить"
        cancel-label="Отменить"
        @submit="save"
        @cancel="cancel"
      />

      <div class="danger-row">
        <button
          type="button"
          class="delete-publication"
          @click="confirmingDelete = true"
        >
          Удалить публикацию
        </button>
      </div>
    </template>

    <!-- 4. empty / not found -->
    <secondary-text v-else>Публикация не найдена</secondary-text>

    <ConfirmDialog
      :show="confirmingDelete"
      title="Удаление публикации"
      message="Удалить эту публикацию? Действие необратимо."
      confirm-label="Удалить публикацию"
      danger
      @confirm="confirmDelete"
      @cancel="confirmingDelete = false"
    />
  </div>
</template>

<style scoped lang="sass">
.publication-edit
  max-width: $grid-step * 200

.danger-row
  margin-top: $big

.delete-publication
  padding: $small $medium
  border: 1px solid $accent-red
  border-radius: $border-radius
  background: none
  color: $accent-red
  cursor: pointer
  font: inherit

  &:hover
    +tint($accent-red, 10%)
</style>
