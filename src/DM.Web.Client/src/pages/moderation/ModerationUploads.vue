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
 *
 * The filter is the search line of the moderation lists: the site's search
 * field as wide as the column and "Найти" beside it, no caption over it. It
 * looks the name up on "Найти" and not as the reader types, because the
 * server finds one user by his exact name: a list that reloaded on every
 * letter would find nobody until the last one. The placeholder says so
 * ("Точное имя пользователя"). The name lives in the address, as on the
 * warnings beside, so a narrowed list is a link a moderator can hand to
 * another one, and a new name opens its list on the first page and not on
 * the page of the list being left.
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
import { parsePageNumber } from "@/shared/lib/filters";
import {
  uploadPreviewColumn,
  uploadFileColumn,
} from "@/shared/lib/utils/upload";
import { useUploadDelete } from "@/shared/lib/composables/useUploadDelete";
import { useGuardedRequest } from "@/shared/lib/composables/useGuardedRequest";
import { useRoleGate } from "./lib/useRoleGate";
import { VALUE_UNAVAILABLE } from "@/shared/lib/constants/copy";

const PAGE_SIZE = 25;

const route = useRoute();
const router = useRouter();
const { hasAccess, deniedText } = useRoleGate("Moderator");

const uploads = ref<Upload[]>([]);
const paging = ref<PagingModel | null>(null);

// A page number and a name both come from the address and both change under
// the reader, so two answers can be on the wire at once; the guard drops the
// one the reader has already left.
const {
  loading,
  error: loadError,
  run,
} = useGuardedRequest({ message: "Не удалось загрузить список файлов" });

const pageNumber = computed(() => parsePageNumber(route.query.number) ?? 1);

/** The one filter the list takes (doc: the "Пользователь" filter). */
const username = computed(() => {
  const value = route.query.username;
  const name = Array.isArray(value) ? value[0] : value;
  return name ? String(name) : undefined;
});

// The field shows what the address says, so a link handed over opens with the
// name already in it and the back button puts the previous name back.
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

function fetch() {
  const name = username.value;
  return run(
    async () => {
      const result = await moderationApi.getAllUploads({
        username: name,
        number: pageNumber.value,
        size: PAGE_SIZE,
      });
      // The server looks the name up before it lists anything, and a name
      // nobody carries is its 404. For the list that is no failure but a list
      // with nothing in it, the answer the warnings beside give the same name:
      // "Не удалось загрузить" with "Повторить" sent the moderator to retry a
      // typo.
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

watch([pageNumber, username], fetch, { immediate: true });

/**
 * The name goes to the address, and the page number stays behind: it counted
 * pages of the list the moderator is leaving. An emptied field drops the
 * filter. The field takes the trimmed name here rather than waiting for the
 * address to hand it back: the same name again, padded, is no navigation.
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

/**
 * Emptied by the cross of the field or by hand, the field drops the filter at
 * once: a list narrowed to a name under a field that names nobody would say
 * two different things.
 */
function onInput(value: string) {
  usernameInput.value = value;
  if (!value.trim() && username.value) applyFilter();
}

/** Nothing under a name, said in words: the empty table says "пока нет". */
const isFilteredEmpty = computed(
  () => !!username.value && !loading.value && uploads.value.length === 0,
);

// --- Delete upload (ConfirmDialog-gated) ---
const { deleteTarget, deleteFile } = useUploadDelete(fetch);
</script>

<template>
  <div class="moderation-uploads">
    <page-title>Все загруженные файлы</page-title>

    <SecondaryText v-if="!hasAccess">{{ deniedText }}</SecondaryText>

    <template v-else>
      <form class="search-line" @submit.prevent="applyFilter">
        <FilterSearchInput
          :model-value="usernameInput"
          placeholder="Точное имя пользователя"
          @update:model-value="onInput"
        />
        <Button type="submit" variant="primary">Найти</Button>
      </form>

      <ErrorState v-if="loadError" :message="loadError" :retry="fetch" />

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
// The search line of the moderation lists: the field takes the width the
// button leaves, both $control-height, one line at every width.
.search-line
  display: flex
  align-items: center
  gap: $small
  margin-bottom: $medium

.muted
  color: $text-muted

.pager
  margin-top: $medium
</style>
