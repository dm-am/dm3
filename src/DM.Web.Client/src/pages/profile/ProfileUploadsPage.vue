<script setup lang="ts">
/**
 * "Загруженное": the owner's uploaded files, newest first, paged, deleted after
 * a question; an admin sees anybody's. Anybody else gets the site's "not found".
 */
import { computed, ref, watch } from "vue";
import { useRoute } from "vue-router";
import uploadApi from "@/shared/api/uploadApi";
import type { Upload } from "@/shared/api/models/common/upload";
import type { PagingInfo as PagingModel } from "@/shared/api/models/common";
import { UserRole } from "@/shared/api/models/common";
import { isSameUsername, restoreSession, useAuthStore } from "@/entities/user";
import {
  uploadPreviewColumn,
  uploadFileColumn,
} from "@/shared/lib/utils/upload";
import { type Column } from "@/shared/ui/DataTable";
import {
  OWNER_UPLOAD_COLUMN_WIDTHS,
  UploadsTable,
} from "@/shared/ui/UploadsTable";
import { Paging } from "@/shared/ui/Paging";
import { ErrorState } from "@/shared/ui/ErrorState";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { ErrorPage } from "@/shared/ui/ErrorPage";
import ProfileSubpageHeader from "./ProfileSubpageHeader.vue";
import { useProfileSubpage } from "./useProfileSubpage";
import { useUploadDelete } from "@/shared/lib/composables/useUploadDelete";
import { parsePageNumber } from "@/shared/lib/filters";
import { usePaging } from "@/shared/lib/composables/usePaging";
import { useNotFoundWhen } from "@/shared/lib/composables/useNotFoundWhen";

// A page as long as the reader's lists (account settings).
const { pageSize } = usePaging();

const route = useRoute();
const authStore = useAuthStore();

const { username, canonicalUsername, notFound, profileLink } =
  useProfileSubpage("Загруженные файлы");

// The owner or an admin.
const isOwner = computed(() =>
  isSameUsername(authStore.user?.username, username.value),
);
const isAdmin = computed(() => authStore.user?.role === UserRole.Admin);
const canView = computed(() => isOwner.value || isAdmin.value);

// Decided after restoreSession, or a session the browser kept no copy of would send the owner away.
const sessionSettled = ref(false);
const settle = () => (sessionSettled.value = true);
restoreSession().then(settle, settle);

useNotFoundWhen(() => sessionSettled.value && !canView.value);

const uploads = ref<Upload[]>([]);
const paging = ref<PagingModel | null>(null);
const loading = ref(false);
const loadError = ref<string | null>(null);

const pageNumber = computed(() => parsePageNumber(route.query.number) ?? 1);

async function loadUploads() {
  if (!canView.value) return;
  loading.value = true;
  const { data, error } = await uploadApi.getUploads({
    // The username filter is the admin's: the owner's own uploads are the default scope.
    username: isOwner.value ? undefined : username.value,
    number: pageNumber.value,
    size: pageSize.value,
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

watch([pageNumber, username, canView], loadUploads, { immediate: true });

const columns: Column[] = [
  uploadPreviewColumn,
  uploadFileColumn,
  { key: "date", label: "Дата", width: OWNER_UPLOAD_COLUMN_WIDTHS.date },
  {
    key: "size",
    label: "Размер",
    width: OWNER_UPLOAD_COLUMN_WIDTHS.size,
    align: "right",
  },
  {
    key: "actions",
    label: "",
    width: OWNER_UPLOAD_COLUMN_WIDTHS.actions,
    align: "center",
  },
];

// Delete, asked first.
const { deleteTarget, deleteFile } = useUploadDelete(loadUploads);
</script>

<template>
  <ErrorPage v-if="notFound" :code="404" />
  <div v-else-if="canView" class="profile-uploads-page">
    <ProfileSubpageHeader
      label="Загруженные файлы"
      :username="canonicalUsername"
    >
      Все файлы, загруженные игроком
      <router-link :to="profileLink">{{ canonicalUsername }}</router-link
      >: аватары и изображения из постов
    </ProfileSubpageHeader>

    <ErrorState
      v-if="loadError"
      class="error-banner"
      :message="loadError"
      :retry="loadUploads"
    />

    <UploadsTable
      :columns="columns"
      :uploads="uploads"
      :loading="loading"
      @remove="(row) => (deleteTarget = row)"
    />

    <Paging
      v-if="paging"
      class="pager"
      :paging="paging"
      :to="{ name: 'profile-uploads', params: { username } }"
      use-query
    />

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
.profile-uploads-page
  width: 100%

.error-banner
  margin-bottom: $medium

.pager
  margin-top: $medium
</style>
