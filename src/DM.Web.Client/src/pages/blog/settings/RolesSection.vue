<script setup lang="ts">
/**
 * The blog staff: inviting an assistant is the owner's (InviteAssistant),
 * removal asks BlogIntention.Edit; the page passes both. An invited assistant
 * joins the roster only on acceptance, so the block below names who is invited.
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
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { formatDateFull } from "@/shared/lib/utils/datetime";
import { useActionResult } from "@/shared/lib/composables/useActionResult";
import { ActionResult } from "@/shared/ui/ActionResult";
import { OWNS_REFUSAL } from "@/shared/api/interceptor";

defineProps<{
  canInvite: boolean;
  canRemove: boolean;
}>();

const store = useBlogDetailsStore();
const { blog } = storeToRefs(store);

const assistants = computed(() => blog.value?.assistants ?? []);

const inviteUsername = ref("");
const inviting = ref(false);
const pendingRemove = ref<{ username: string } | null>(null);

const pending = ref<BlogInvitation[]>([]);

const sentAt = (invitation: BlogInvitation) =>
  new Date(invitation.createdUtc).getTime();

const sentLabel = (invitation: BlogInvitation) =>
  `отправлено ${formatDateFull(invitation.createdUtc)}`;

/**
 * The assistant invitations still out, oldest first. The blog DTO stops at
 * accepted assistants, so they are read from the invitation endpoint; the kind
 * is spelled in lower case (BlogRoleExtensions.ToApiString).
 */
async function loadPending() {
  if (!blog.value) return;
  const { data, error } = await blogApi.getInvitations(blog.value.id);
  // A failed read leaves the block as it is.
  if (error) return;
  pending.value = (data?.resources ?? [])
    .filter((invitation) => invitation.type === "assistant")
    .sort((a, b) => sentAt(a) - sentAt(b));
}

onMounted(loadPending);

const inviteResult = useActionResult();

async function invite() {
  if (!blog.value || !inviteUsername.value.trim()) return;
  inviting.value = true;
  inviteResult.clear();
  const { data, error } = await blogApi.inviteAssistant(
    blog.value.id,
    inviteUsername.value.trim(),
  );
  inviting.value = false;
  if (error) {
    inviteResult.refuse(error, "Не удалось пригласить ассистента");
    return;
  }
  // The row from the answer, replacing the invitation it invalidated; put in
  // before the reload, which may then fail.
  if (data) {
    pending.value = [
      ...pending.value.filter(
        (i) => i.invitedUser.username !== data.invitedUser.username,
      ),
      data,
    ];
  }
  inviteUsername.value = "";
  // Not the blog: an invitation adds nobody to the roster.
  await loadPending();
}

// The window waits for it, prints a refusal in itself and closes on success alone.
async function confirmRemove() {
  const target = pendingRemove.value;
  if (!blog.value || !target) return;
  const result = await blogApi.removeAssistant(
    blog.value.id,
    target.username,
    OWNS_REFUSAL,
  );
  if (!result.error) void store.loadBlog(blog.value.id);
  return result;
}
</script>

<template>
  <SettingsSection title="Управление ролями">
    <div class="role-block">
      <div class="role-label">Ассистенты</div>
      <ul v-if="assistants.length" class="staff-list">
        <li v-for="a in assistants" :key="a.id" class="staff-item">
          <UserLink :user="a" hide-badge />
          <RemoveButton
            v-if="canRemove"
            @click="pendingRemove = { username: a.username }"
          >
            Удалить
          </RemoveButton>
        </li>
      </ul>
      <SecondaryText v-else>Ассистентов нет</SecondaryText>

      <div v-if="pending.length" class="pending">
        <div class="role-label">Ожидают подтверждения</div>
        <ul class="staff-list">
          <li v-for="inv in pending" :key="inv.id" class="staff-item">
            <UserLink :user="inv.invitedUser" hide-badge />
            <span class="sent">{{ sentLabel(inv) }}</span>
          </li>
        </ul>
      </div>

      <div v-if="canInvite" class="invite-row">
        <UserAutocomplete
          v-model="inviteUsername"
          placeholder="Имя пользователя"
        />
        <Button
          type="button"
          variant="primary"
          :loading="inviting"
          :disabled="!inviteUsername.trim()"
          @click="invite"
        >
          Пригласить
        </Button>
      </div>
      <ActionResult v-if="canInvite" :result="inviteResult" />
    </div>

    <ConfirmDialog
      :show="!!pendingRemove"
      title="Удалить ассистента?"
      message="Ассистент потеряет права в блоге"
      confirm-label="Удалить"
      failure="Не удалось удалить ассистента"
      :action="confirmRemove"
      @update:show="(shown) => !shown && (pendingRemove = null)"
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

.pending
  margin-top: $small

.sent
  color: $text-muted
  font-size: $secondary-font-size

.invite-row
  display: flex
  align-items: flex-start
  gap: $small
  margin-top: $small
</style>
