<script setup lang="ts">
/**
 * RolesSection — manage game staff. Master and assistant may invite an
 * assistant; only the master may remove an assistant (removeAssistant). The
 * mentor (premoderation curator, a global role) is shown read-only — there is
 * no per-game mentor-assignment endpoint.
 */
import { computed, ref } from "vue";
import { storeToRefs } from "pinia";
import { useGameDetailsStore, gameApi } from "@/entities/game";
import { UserLink } from "@/entities/user";
import { UserAutocomplete } from "@/entities/user";
import Button from "@/shared/ui/Button/Button.vue";
import { SettingsSection } from "@/shared/ui/SettingsSection";
import { RemoveButton } from "@/shared/ui/RemoveButton";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { useToast } from "@/shared/lib/composables/useToast";

const store = useGameDetailsStore();
const { game, isMaster } = storeToRefs(store);
const toast = useToast();

const assistants = computed(() => game.value?.fullAssistants ?? []);
const mentor = computed(() => game.value?.mentor ?? null);
const pendingAssistant = computed(() => game.value?.pendingAssistant ?? null);

const inviteUsername = ref("");
const inviting = ref(false);
const pendingRemove = ref<{ username: string } | null>(null);

async function invite() {
  if (!game.value || !inviteUsername.value.trim()) return;
  inviting.value = true;
  const { error } = await gameApi.inviteAssistant(
    game.value.id,
    inviteUsername.value.trim(),
  );
  inviting.value = false;
  if (error) {
    toast.error("Не удалось пригласить ассистента");
    return;
  }
  toast.success("Приглашение ассистенту отправлено");
  inviteUsername.value = "";
  await store.loadGame(game.value.publicId ?? game.value.id);
}

async function confirmRemove() {
  const target = pendingRemove.value;
  pendingRemove.value = null;
  if (!game.value || !target) return;
  const { error } = await gameApi.removeAssistant(
    game.value.id,
    target.username,
  );
  if (error) {
    toast.error("Не удалось удалить ассистента");
    return;
  }
  toast.success("Ассистент удален");
  await store.loadGame(game.value.publicId ?? game.value.id);
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
            v-if="isMaster"
            @click="pendingRemove = { username: a.username }"
          >
            Удалить
          </RemoveButton>
        </li>
      </ul>
      <SecondaryText v-else>Ассистентов нет</SecondaryText>

      <SecondaryText v-if="pendingAssistant" class="pending">
        Ожидает подтверждения: <UserLink :user="pendingAssistant" />
      </SecondaryText>

      <div class="invite-row">
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

    <div class="role-block">
      <div class="role-label">Ментор (премодерация)</div>
      <UserLink v-if="mentor" :user="mentor" />
      <SecondaryText v-else>Ментор не назначен</SecondaryText>
    </div>

    <ConfirmDialog
      :show="!!pendingRemove"
      title="Удаление ассистента"
      message="Удалить ассистента из игры?"
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

.pending
  display: block
  margin-top: $small

.invite-row
  display: flex
  align-items: flex-start
  gap: $small
  margin-top: $small
</style>
