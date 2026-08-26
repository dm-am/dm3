<script setup lang="ts">
/**
 * ModerationUploads — "Все загруженные файлы" (doc 4.2.3.8.9). Every file
 * uploaded to the website, newest first, paged (GET v1/uploads?scope=all,
 * moderator+ gated server-side, doc 4.2.3.8.9). A username filter narrows to
 * one user's uploads. Delete soft-deletes the file (ConfirmDialog-gated).
 *
 * The table is the shared UploadsTable, the same one the owner's "Загруженное"
 * draws; this page adds the column it alone has. The uploader column links to
 * each file's owner via the Upload DTO's uploaderUsername (falls back to the
 * active username filter, then to "—").
 */
import { computed, ref, watch } from "vue";
import { useRoute } from "vue-router";
import { moderationApi } from "@/entities/moderation";
import type { Upload } from "@/shared/api/models/common/upload";
import type { PagingInfo as PagingModel } from "@/shared/api/models/common";
import { type Column } from "@/shared/ui/DataTable";
import { UploadsTable } from "@/shared/ui/UploadsTable";
import { Paging } from "@/shared/ui/Paging";
import { ErrorState } from "@/shared/ui/ErrorState";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { parsePageNumber } from "@/shared/lib/filters";
import {
  uploadPreviewColumn,
  uploadFileColumn,
} from "@/shared/lib/utils/upload";
import { useUploadDelete } from "@/shared/lib/composables/useUploadDelete";
import { useRoleGate } from "./lib/useRoleGate";
import { VALUE_UNAVAILABLE } from "@/shared/lib/constants/copy";

const PAGE_SIZE = 25;

const route = useRoute();
const { hasAccess, deniedText } = useRoleGate("Moderator");

const uploads = ref<Upload[]>([]);
const paging = ref<PagingModel | null>(null);
const loading = ref(false);
const loadError = ref<string | null>(null);

// Username filter (doc: the "Пользователь" filter)
const usernameInput = ref("");
const usernameFilter = ref("");

const pageNumber = computed(() => parsePageNumber(route.query.number) ?? 1);

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
const { deleteTarget, deleting, confirmDelete } = useUploadDelete(fetch);
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
        <UploadsTable
          :columns="columns"
          :uploads="uploads"
          :loading="loading"
          @remove="(row) => (deleteTarget = row)"
        >
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
        </UploadsTable>

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

.muted
  color: $text-muted

.pager
  margin-top: $medium
</style>
