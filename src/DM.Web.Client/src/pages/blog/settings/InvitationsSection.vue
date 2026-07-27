<script setup lang="ts">
/**
 * InvitationsSection — outstanding invitations for the blog plus the control
 * to invite a reader and to cancel a pending invitation. Assistant invites
 * live in RolesSection (mirrors the game split: staff invites with roles,
 * audience invites here). Backed by the invitation blogApi; the list is held
 * locally (not part of the shared blog store).
 */
import { computed, onMounted, ref } from "vue";
import { storeToRefs } from "pinia";
import { useBlogDetailsStore, blogApi } from "@/entities/blog";
import type { BlogInvitation } from "@/entities/blog";
import { UserLink } from "@/entities/user";
import { UserAutocomplete } from "@/entities/user";
import Button from "@/shared/ui/Button/Button.vue";
import { SettingsSection } from "@/shared/ui/SettingsSection";
import { RemoveButton } from "@/shared/ui/RemoveButton";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { useToast } from "@/shared/lib/composables/useToast";

const store = useBlogDetailsStore();
const { blog } = storeToRefs(store);
const toast = useToast();

const invitations = ref<BlogInvitation[]>([]);
const loading = ref(false);

const username = ref("");
const inviting = ref(false);

const typeLabels: Record<string, string> = {
  reader: "Читатель",
  assistant: "Ассистент",
};

const blogId = computed(() => blog.value?.id ?? "");

async function load() {
  if (!blogId.value) return;
  loading.value = true;
  const { data } = await blogApi.getInvitations(blogId.value);
  invitations.value = data?.resources ?? [];
  loading.value = false;
}

onMounted(load);

async function invite() {
  if (!blogId.value || !username.value.trim()) return;
  inviting.value = true;
  const { error } = await blogApi.inviteReader(
    blogId.value,
    username.value.trim(),
  );
  inviting.value = false;
  if (error) {
    toast.error("Не удалось отправить приглашение");
    return;
  }
  toast.success("Приглашение отправлено");
  username.value = "";
  await load();
}

async function cancel(invitation: BlogInvitation) {
  if (!blogId.value) return;
  const { error } = await blogApi.cancelInvitation(blogId.value, invitation.id);
  if (error) {
    toast.error("Не удалось отменить приглашение");
    return;
  }
  toast.success("Приглашение отменено");
  await load();
}
</script>

<template>
  <SettingsSection title="Приглашения">
    <ul v-if="invitations.length" class="inv-list">
      <li v-for="inv in invitations" :key="inv.id" class="inv-item">
        <UserLink :user="inv.invitedUser" />
        <span class="inv-type">{{ typeLabels[inv.type] ?? inv.type }}</span>
        <RemoveButton @click="cancel(inv)">Отменить</RemoveButton>
      </li>
    </ul>
    <SecondaryText v-else-if="!loading">Активных приглашений нет</SecondaryText>

    <div class="invite-row">
      <UserAutocomplete v-model="username" placeholder="Имя пользователя" />
      <Button
        type="button"
        :loading="inviting"
        :disabled="!username.trim()"
        @click="invite"
      >
        Пригласить читателя
      </Button>
    </div>
  </SettingsSection>
</template>

<style scoped lang="sass">
.inv-list
  list-style: none
  display: flex
  flex-direction: column
  gap: $tiny
  margin-bottom: $small

.inv-item
  display: flex
  align-items: center
  gap: $small

.inv-type
  color: $text-muted
  font-size: $secondary-font-size

.invite-row
  display: flex
  align-items: flex-start
  gap: $small
  flex-wrap: wrap
</style>
