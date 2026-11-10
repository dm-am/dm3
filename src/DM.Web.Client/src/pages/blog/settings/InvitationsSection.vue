<script setup lang="ts">
/**
 * The blog's outstanding invitations, inviting a reader and cancelling one;
 * assistant invites are RolesSection's. The read, the invite and the cancel
 * ask three intentions, so the page passes two flags.
 */
import { computed } from "vue";
import { storeToRefs } from "pinia";
import { useBlogDetailsStore, blogApi } from "@/entities/blog";
import { InvitationsEditor } from "@/features/module-staff";
import { SettingsSection } from "@/shared/ui/SettingsSection";

defineProps<{
  canInvite: boolean;
  canCancel: boolean;
}>();

const store = useBlogDetailsStore();
const { blog } = storeToRefs(store);

const blogId = computed(() => blog.value?.id ?? "");

// The endpoint serves newest first; the editor puts them oldest first.
const loadInvitations = () =>
  blogId.value ? blogApi.getInvitations(blogId.value) : null;

const sendInvitation = (username: string) =>
  blogId.value ? blogApi.inviteReader(blogId.value, username) : null;

const cancelInvitation = (id: string) =>
  blogId.value ? blogApi.cancelInvitation(blogId.value, id) : null;
</script>

<template>
  <SettingsSection title="Приглашения">
    <InvitationsEditor
      :can-invite="canInvite"
      :can-cancel="canCancel"
      invite-label="Пригласить читателя"
      :load-invitations="loadInvitations"
      :send-invitation="sendInvitation"
      :cancel-invitation="cancelInvitation"
    />
  </SettingsSection>
</template>
