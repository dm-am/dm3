<script setup lang="ts">
/**
 * The blog staff: inviting an assistant is the owner's (InviteAssistant),
 * removal asks BlogIntention.Edit; the page passes both.
 */
import { computed } from "vue";
import { storeToRefs } from "pinia";
import { useBlogDetailsStore, blogApi } from "@/entities/blog";
import { AssistantsEditor } from "@/features/module-staff";
import { SettingsSection } from "@/shared/ui/SettingsSection";
import { OWNS_REFUSAL } from "@/shared/api/interceptor";

defineProps<{
  canInvite: boolean;
  canRemove: boolean;
}>();

const store = useBlogDetailsStore();
const { blog } = storeToRefs(store);

const assistants = computed(() => blog.value?.assistants ?? []);

// The kind comes in lower case (BlogRoleExtensions.ToApiString).
const loadInvitations = () =>
  blog.value ? blogApi.getInvitations(blog.value.id) : null;

const sendInvitation = (username: string) =>
  blog.value ? blogApi.inviteAssistant(blog.value.id, username) : null;

async function removeAssistant(username: string) {
  if (!blog.value) return;
  const result = await blogApi.removeAssistant(
    blog.value.id,
    username,
    OWNS_REFUSAL,
  );
  if (!result.error) void store.loadBlog(blog.value.id);
  return result;
}
</script>

<template>
  <SettingsSection title="Управление ролями">
    <AssistantsEditor
      :assistants="assistants"
      :can-invite="canInvite"
      :can-remove="canRemove"
      removal-message="Ассистент потеряет права в блоге"
      :load-invitations="loadInvitations"
      :send-invitation="sendInvitation"
      :remove-assistant="removeAssistant"
    />
  </SettingsSection>
</template>
