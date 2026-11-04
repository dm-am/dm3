<script setup lang="ts">
/**
 * Every file uploaded to the site, newest first; a name narrows it to one
 * uploader. The server finds the user by the exact name, so the name is looked
 * up on "Найти", not as the reader types, and lives in the address.
 */
import { computed, ref, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import { moderationApi } from "@/entities/moderation";
import type { Upload } from "@/shared/api/models/common/upload";
import type { PagingInfo as PagingModel } from "@/shared/api/models/common";
import { type Column } from "@/shared/ui/DataTable";
import { UploadsTable } from "@/shared/ui/UploadsTable";
import { Paging } from "@/shared/ui/Paging";
import { ErrorState } from "@/shared/ui/ErrorState";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { FilterSearchInput } from "@/shared/ui/Filters";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import {
  parsePageNumber,
  parseStringFromUrl,
  toQueryValue,
} from "@/shared/lib/filters";
import {
  uploadPreviewColumn,
  uploadFileColumn,
} from "@/shared/lib/utils/upload";
import { useUploadDelete } from "@/shared/lib/composables/useUploadDelete";
import { usePaging } from "@/shared/lib/composables/usePaging";
import { useGuardedRequest } from "@/shared/lib/composables/useGuardedRequest";
import { VALUE_UNAVAILABLE } from "@/shared/lib/constants/copy";

const route = useRoute();
const router = useRouter();
// A page as long as the reader's lists (account settings).
const { pageSize } = usePaging();

const uploads = ref<Upload[]>([]);
const paging = ref<PagingModel | null>(null);

// The number and the name both come from the address; the guard drops the answer the reader has left.
const {
  loading,
  error: loadError,
  run,
} = useGuardedRequest({ message: "Не удалось загрузить список файлов" });

const pageNumber = computed(() => parsePageNumber(route.query.number) ?? 1);

const username = computed(
  () => parseStringFromUrl(toQueryValue(route.query.username)) || undefined,
);

// The field shows what the address says, so the back button puts the previous name back.
const usernameInput = ref(username.value ?? "");
watch(username, (name) => (usernameInput.value = name ?? ""));

const columns: Column[] = [
  uploadPreviewColumn,
  uploadFileColumn,
  { key: "uploader", label: "Загрузил", width: "20%" },
  { key: "date", label: "Дата", width: "16%" },
  { key: "size", label: "Размер", width: "14%", align: "right" },
  { key: "actions", label: "", width: "14%", align: "center" },
];

function loadUploads() {
  const name = username.value;
  return run(
    async () => {
      const result = await moderationApi.getAllUploads({
        username: name,
        number: pageNumber.value,
        size: pageSize.value,
      });
      // A name nobody carries is the server's 404: for the list that is an empty
      // list, as the warnings beside answer it, not a failure to retry.
      if (name && result.error?.status === 404)
        return { data: null, error: null };
      return result;
    },
    (data) => {
      uploads.value = data?.resources ?? [];
      paging.value = data?.paging ?? null;
    },
  );
}

watch([pageNumber, username], loadUploads, { immediate: true });

/**
 * The name goes to the address without the page number; an emptied field drops
 * the filter. The field takes the trimmed name: the same name padded is no navigation.
 */
function applyFilter() {
  const query = { ...route.query };
  delete query.number;
  const name = usernameInput.value.trim();
  if (name) query.username = name;
  else delete query.username;
  usernameInput.value = name;
  router.push({ name: "moderation-uploads", query });
}

/** An emptied field drops the filter at once. */
function onInput(value: string) {
  usernameInput.value = value;
  if (!value.trim() && username.value) applyFilter();
}

/** Nothing under a name, said in words: the empty table says "пока нет". */
const isFilteredEmpty = computed(
  () => !!username.value && !loading.value && uploads.value.length === 0,
);

const { deleteTarget, deleteFile } = useUploadDelete(loadUploads);
</script>

<template>
  <div class="moderation-uploads">
    <page-title>Все загруженные файлы</page-title>

    <form class="search-line" @submit.prevent="applyFilter">
      <FilterSearchInput
        :model-value="usernameInput"
        placeholder="Точное имя пользователя"
        @update:model-value="onInput"
      />
      <Button type="submit" variant="primary">Найти</Button>
    </form>

    <ErrorState v-if="loadError" :message="loadError" :retry="loadUploads" />

    <SecondaryText v-else-if="isFilteredEmpty"
      >Загруженных файлов по заданным фильтрам не найдено</SecondaryText
    >

    <template v-else>
      <UploadsTable
        :columns="columns"
        :uploads="uploads"
        :loading="loading"
        @remove="(row) => (deleteTarget = row)"
      >
        <template #cell-uploader="{ row }">
          <router-link
            v-if="row.uploaderUsername || username"
            :to="{
              name: 'profile',
              params: { username: row.uploaderUsername || username },
            }"
          >
            {{ row.uploaderUsername || username }}
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

    <ConfirmDialog
      :show="deleteTarget !== null"
      title="Удалить файл?"
      :message="`Файл &quot;${deleteTarget?.originalFileName ?? ''}&quot; исчезнет из постов, куда он вставлен`"
      confirm-label="Удалить"
      failure="Не удалось удалить файл"
      :action="deleteFile"
      @update:show="(v) => !v && (deleteTarget = null)"
    />
  </div>
</template>

<style scoped lang="sass">
// The field takes the width the button leaves, one line at every width.
.search-line
  display: flex
  align-items: center
  gap: $small
  margin-bottom: $medium

.pager
  margin-top: $medium
</style>
