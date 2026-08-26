<script setup lang="ts">
/**
 * ProfileUploadsPage — "Загруженное" (doc 4.2.3.3.7).
 *
 * The profile owner's uploaded files (avatars, post images), newest first,
 * paged, with a ConfirmDialog-gated delete. Mirrors the profile subpage
 * pattern (useProfileSubpage + ProfileSubpageHeader); the table itself is the
 * shared UploadsTable, the same one the moderation-wide list draws.
 *
 * Access: the page is for the file owner (doc), and admins can view any
 * user's uploads (GET /v1/uploads?username= is Admin-gated server-side).
 * Deletion server-side is owner-or-admin (DELETE /v1/uploads/{id}); the
 * doc also grants moderators delete rights — backend gap, flagged in the
 * integration notes.
 */
import { computed, ref, watch } from "vue";
import { useRoute } from "vue-router";
import uploadApi from "@/shared/api/uploadApi";
import type { Upload } from "@/shared/api/models/common/upload";
import type { PagingInfo as PagingModel } from "@/shared/api/models/common";
import { UserRole } from "@/shared/api/models/common";
import { useAuthStore } from "@/shared/stores/auth";
import {
  uploadPreviewColumn,
  uploadFileColumn,
} from "@/shared/lib/utils/upload";
import { type Column } from "@/shared/ui/DataTable";
import { UploadsTable } from "@/shared/ui/UploadsTable";
import { Paging } from "@/shared/ui/Paging";
import { ErrorState } from "@/shared/ui/ErrorState";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { SecondaryText } from "@/shared/ui/Layout";
import { ErrorPage } from "@/shared/ui/ErrorPage";
import ProfileSubpageHeader from "./ProfileSubpageHeader.vue";
import { useProfileSubpage } from "./useProfileSubpage";
import { useUploadDelete } from "@/shared/lib/composables/useUploadDelete";
import { parsePageNumber } from "@/shared/lib/filters";

const PAGE_SIZE = 20;

const route = useRoute();
const authStore = useAuthStore();

const { username, canonicalUsername, notFound, profileLink } =
  useProfileSubpage("Загруженные файлы");

// --- Access gate: owner or admin (doc: "доступна владельцу файлов") ---
const isOwner = computed(
  () =>
    !!authStore.user &&
    authStore.user.username.toLowerCase() === username.value.toLowerCase(),
);
const isAdmin = computed(() => authStore.user?.role === UserRole.Admin);
const canView = computed(() => isOwner.value || isAdmin.value);

// --- Data ---
const uploads = ref<Upload[]>([]);
const paging = ref<PagingModel | null>(null);
const loading = ref(false);
const loadError = ref<string | null>(null);

const pageNumber = computed(() => parsePageNumber(route.query.number) ?? 1);

async function fetch() {
  if (!canView.value) return;
  loading.value = true;
  const { data, error } = await uploadApi.getUploads({
    // Own uploads come from the default scope; the username filter is
    // admin-only server-side, so it is passed only for the admin case
    username: isOwner.value ? undefined : username.value,
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

watch([pageNumber, username, canView], fetch, { immediate: true });

const columns: Column[] = [
  uploadPreviewColumn,
  uploadFileColumn,
  { key: "date", label: "Дата", width: "16%" },
  { key: "size", label: "Размер", width: "14%", align: "right" },
  { key: "actions", label: "", width: "14%", align: "center" },
];

// --- Delete upload (ConfirmDialog-gated) ---
const { deleteTarget, deleting, confirmDelete } = useUploadDelete(fetch);
</script>

<template>
  <ErrorPage v-if="notFound" :code="404" />
  <div v-else class="profile-uploads-page">
    <ProfileSubpageHeader
      label="Загруженные файлы"
      :username="canonicalUsername"
    >
      Все файлы, загруженные игроком
      <router-link :to="profileLink">{{ canonicalUsername }}</router-link
      >: аватары и изображения из постов
    </ProfileSubpageHeader>

    <SecondaryText v-if="!canView">
      Страница доступна только владельцу файлов
    </SecondaryText>

    <template v-else>
      <ErrorState
        v-if="loadError"
        class="error-banner"
        :message="loadError"
        :retry="fetch"
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
.profile-uploads-page
  width: 100%

.error-banner
  margin-bottom: $medium

.pager
  margin-top: $medium
</style>
