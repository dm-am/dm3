<script setup lang="ts">
/**
 * RolesSection — manage blog staff. The two operations answer to different
 * intentions: BlogIntention.InviteAssistant is the owner's alone, while
 * removal (BlogService.RemoveAssistant) asks BlogIntention.Edit, which the
 * owner and administration hold. The host page computes both and passes them
 * in; everyone else the settings page admits reads the roster without
 * controls. Mirrors the game RolesSection; blogs expose no per-blog mentor
 * field on the DTO, so there is no mentor row here.
 */
import { computed, ref } from "vue";
import { storeToRefs } from "pinia";
import { useBlogDetailsStore, blogApi } from "@/entities/blog";
import { UserLink } from "@/entities/user";
import { UserAutocomplete } from "@/entities/user";
import Button from "@/shared/ui/Button/Button.vue";
import { SettingsSection } from "@/shared/ui/SettingsSection";
import { RemoveButton } from "@/shared/ui/RemoveButton";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { useToast } from "@/shared/lib/composables/useToast";
import { notifyFailure } from "@/shared/lib/errors";

defineProps<{
  /** Whether the viewer may invite an assistant. */
  canInvite: boolean;
  /** Whether the viewer may remove an assistant. */
  canRemove: boolean;
}>();

const store = useBlogDetailsStore();
const { blog } = storeToRefs(store);
const toast = useToast();

const assistants = computed(() => blog.value?.assistants ?? []);

const inviteUsername = ref("");
const inviting = ref(false);
const pendingRemove = ref<{ username: string } | null>(null);

async function invite() {
  if (!blog.value || !inviteUsername.value.trim()) return;
  inviting.value = true;
  const { error } = await blogApi.inviteAssistant(
    blog.value.id,
    inviteUsername.value.trim(),
  );
  inviting.value = false;
  if (error) {
    notifyFailure(error, "Не удалось пригласить ассистента");
    return;
  }
  toast.success("Приглашение ассистенту отправлено");
  inviteUsername.value = "";
  await store.loadBlog(blog.value.id);
}

async function confirmRemove() {
  const target = pendingRemove.value;
  pendingRemove.value = null;
  if (!blog.value || !target) return;
  const { error } = await blogApi.removeAssistant(
    blog.value.id,
    target.username,
  );
  if (error) {
    notifyFailure(error, "Не удалось удалить ассистента");
    return;
  }
  toast.success("Ассистент удален");
  await store.loadBlog(blog.value.id);
}
</script>

<template>
  <SettingsSection title="Управление ролями">
    <div class="role-block">
      <div class="role-label">Ассистенты</div>
      <ul v-if="assistants.length" class="staff-list">
        <li v-for="a in assistants" :key="a.id" class="staff-item">
          <UserLink :user="a" />
          <RemoveButton
            v-if="canRemove"
            @click="pendingRemove = { username: a.username }"
          >
            Удалить
          </RemoveButton>
        </li>
      </ul>
      <SecondaryText v-else>Ассистентов нет</SecondaryText>

      <div v-if="canInvite" class="invite-row">
        <UserAutocomplete
          v-model="inviteUsername"
          placeholder="Имя пользователя"
        />
        <Button
          type="button"
          :loading="inviting"
          :disabled="!inviteUsername.trim()"
          @click="invite"
        >
          Пригласить
        </Button>
      </div>
    </div>

    <ConfirmDialog
      :show="!!pendingRemove"
      title="Удаление ассистента"
      message="Удалить ассистента из блога?"
      confirm-label="Удалить"
      danger
      @confirm="confirmRemove"
      @cancel="pendingRemove = null"
    />
  </SettingsSection>
</template>

<style scoped lang="sass">
.role-block
  & + &
    margin-top: $medium
    padding-top: $medium
    border-top: 1px solid $border

.role-label
  color: $text-muted
  font-size: $secondary-font-size
  margin-bottom: $small

.staff-list
  list-style: none
  display: flex
  flex-direction: column
  gap: $tiny

.staff-item
  display: flex
  align-items: center
  gap: $small

.invite-row
  display: flex
  align-items: flex-start
  gap: $small
  margin-top: $small
</style>
