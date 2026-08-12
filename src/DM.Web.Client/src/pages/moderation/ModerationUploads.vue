<script setup lang="ts">
/**
 * ModerationUploads — "Все загруженное" (doc 4.2.3.8.9). Every file
 * uploaded to the website, newest first, paged (GET v1/uploads?scope=all,
 * moderator+ gated server-side, doc 4.2.3.8.9). A username filter narrows to
 * one user's uploads. Delete soft-deletes the file (ConfirmDialog-gated).
 *
 * The uploader column links to each file's owner via the Upload DTO's
 * uploaderUsername (falls back to the active username filter, then to "—").
 */
import { computed, ref, watch } from "vue";
import { useRoute } from "vue-router";
import { moderationApi } from "@/entities/moderation";
import type { Upload } from "@/shared/api/models/common/upload";
import type { PagingInfo as PagingModel } from "@/shared/api/models/common";
import { DataTable, type Column } from "@/shared/ui/DataTable";
import { Paging } from "@/shared/ui/Paging";
import { ErrorState } from "@/shared/ui/ErrorState";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { formatDate } from "@/shared/lib/utils/datetime";
import { formatFileSize } from "@/shared/lib/utils/fileSize";
import {
  isImage,
  fileExt,
  uploadPreviewColumn,
  uploadFileColumn,
} from "@/shared/lib/utils/upload";
import { useToast } from "@/shared/lib/composables/useToast";
import { useRoleGate } from "./lib/useRoleGate";
import { notifyFailure } from "@/shared/lib/errors";
import { VALUE_UNAVAILABLE } from "@/shared/lib/constants/copy";

const PAGE_SIZE = 25;

const route = useRoute();
const toast = useToast();
const { hasAccess, deniedText } = useRoleGate("Moderator");

const uploads = ref<Upload[]>([]);
const paging = ref<PagingModel | null>(null);
const loading = ref(false);
const loadError = ref<string | null>(null);

// Username filter (doc: the "Пользователь" filter)
const usernameInput = ref("");
const usernameFilter = ref("");

const pageNumber = computed(() => {
  const n = parseInt(String(route.query.number ?? "1"), 10);
  return Number.isFinite(n) && n > 0 ? n : 1;
});

const columns: Column[] = [
  uploadPreviewColumn,
  uploadFileColumn,
  { key: "uploader", label: "Загрузил", width: "18%" },
  { key: "date", label: "Дата", width: "14%", hideOnMobile: true },
  { key: "size", label: "Размер", width: "12%", align: "right" },
  { key: "actions", label: "", width: "12%", align: "center" },
];

async function fetch() {
  loading.value = true;
  const { data, error } = await moderationApi.getAllUploads({
    username: usernameFilter.value || undefined,
    number: pageNumber.value,
    size: PAGE_SIZE,
  });
  loading.value = false;
  if (error) {
    loadError.value = "Не удалось загрузить список файлов";
    return;
  }
  loadError.value = null;
  uploads.value = data?.resources ?? [];
  paging.value = data?.paging ?? null;
}

watch(pageNumber, fetch, { immediate: true });

function applyFilter() {
  usernameFilter.value = usernameInput.value.trim();
  fetch();
}

// --- Delete upload (ConfirmDialog-gated) ---
const deleteTarget = ref<Upload | null>(null);
const deleting = ref(false);

async function confirmDelete() {
  if (!deleteTarget.value || deleting.value) return;
  deleting.value = true;
  const { error } = await moderationApi.deleteUpload(deleteTarget.value.id);
  deleting.value = false;
  if (error) {
    notifyFailure(error, "Не удалось удалить файл");
    return;
  }
  toast.success("Файл удален");
  deleteTarget.value = null;
  await fetch();
}
</script>

<template>
  <div class="moderation-uploads">
    <page-title>Все загруженные файлы</page-title>

    <SecondaryText v-if="!hasAccess">{{ deniedText }}</SecondaryText>

    <template v-else>
      <form class="filters" @submit.prevent="applyFilter">
        <FormField label="Пользователь" name="uploads-username">
          <input
            id="uploads-username"
            v-model="usernameInput"
            type="text"
            placeholder="Имя пользователя"
            autocomplete="off"
          />
        </FormField>
        <Button type="submit">Найти</Button>
      </form>

      <ErrorState v-if="loadError" :message="loadError" :retry="fetch" />

      <template v-else>
        <DataTable
          :columns="columns"
          :data="uploads"
          :loading="loading"
          empty-text="Загруженных файлов пока нет"
          aria-label="Загруженные файлы"
        >
          <template #cell-preview="{ row }">
            <img
              v-if="isImage(row) && row.url"
              :src="row.url"
              :alt="row.originalFileName"
              class="upload-thumb"
              loading="lazy"
            />
            <span v-else class="upload-ext">{{ fileExt(row) }}</span>
          </template>
          <template #cell-file="{ row }">
            <a
              v-if="row.url"
              :href="row.url"
              target="_blank"
              rel="noopener"
              class="upload-name"
            >
              {{ row.originalFileName }}
            </a>
            <span v-else class="upload-name">{{ row.originalFileName }}</span>
          </template>
          <template #cell-uploader="{ row }">
            <router-link
              v-if="row.uploaderUsername || usernameFilter"
              :to="{
                name: 'profile',
                params: { username: row.uploaderUsername || usernameFilter },
              }"
            >
              {{ row.uploaderUsername || usernameFilter }}
            </router-link>
            <span v-else class="muted" :title="row.userId">{{
              VALUE_UNAVAILABLE
            }}</span>
          </template>
          <template #cell-date="{ row }">
            {{ formatDate(row.createdUtc) }}
          </template>
          <template #cell-size="{ row }">
            {{ formatFileSize(row.sizeBytes) }}
          </template>
          <template #cell-actions="{ row }">
            <button
              type="button"
              class="delete-button"
              @click="deleteTarget = row"
            >
              Удалить
            </button>
          </template>
        </DataTable>

        <Paging
          v-if="paging"
          class="pager"
          :paging="paging"
          :to="{ name: 'moderation-uploads' }"
          use-query
        />
      </template>
    </template>

    <ConfirmDialog
      :show="deleteTarget !== null"
      title="Удаление файла"
      :message="`Удалить файл ${deleteTarget?.originalFileName ?? ''}?`"
      confirm-label="Удалить"
      danger
      :loading="deleting"
      @update:show="(v) => !v && (deleteTarget = null)"
      @confirm="confirmDelete"
    />
  </div>
</template>

<style scoped lang="sass">
@import "@/assets/styles/Inputs"

.filters
  display: flex
  align-items: flex-end
  gap: $small
  margin-bottom: $small

  > :first-child
    flex: 0 1 320px

  // Align the submit button with the input row (FormField adds its own
  // vertical margin around the labeled field)
  button
    margin-bottom: $small

.upload-thumb
  display: block
  width: 48px
  height: 48px
  object-fit: cover
  border-radius: $border-radius
  margin: 0 auto

.upload-ext
  color: $text-muted
  font-size: $tertiary-font-size
  font-weight: bold

.upload-name
  overflow-wrap: anywhere

.delete-button
  +inline-link-button

  // Destructive action stays red at rest and on hover
  &,
  &:hover:not(:disabled)
    color: $accent-red

.muted
  color: $text-muted

.pager
  margin-top: $medium
</style>
