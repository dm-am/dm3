<script setup lang="ts">
/**
 * BlacklistEditor — the shared blacklist panel for game and blog settings.
 * Both domains render the same UI (a user list with remove links plus an
 * add-by-username row); the thin game/blog wrappers own the store, the API
 * calls and the reload, and hand this component the current list plus `add`
 * and `remove` callbacks. `add` resolves to `true` on success so the input
 * can be cleared.
 */
import { ref } from "vue";
import type { User } from "@/entities/user";
import { UserLink, UserAutocomplete } from "@/entities/user";
import Button from "@/shared/ui/Button/Button.vue";
import { SettingsSection } from "@/shared/ui/SettingsSection";
import { RemoveButton } from "@/shared/ui/RemoveButton";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";

const props = defineProps<{
  items: User[];
  loading: boolean;
  add: (username: string) => Promise<boolean>;
  remove: (username: string) => Promise<void>;
}>();

const username = ref("");
const adding = ref(false);

async function submitAdd() {
  const value = username.value.trim();
  if (!value || adding.value) return;
  adding.value = true;
  const ok = await props.add(value);
  adding.value = false;
  if (ok) username.value = "";
}
</script>

<template>
  <SettingsSection title="Черный список">
    <ul v-if="items.length" class="bl-list">
      <li v-for="u in items" :key="u.id" class="bl-item">
        <UserLink :user="u" />
        <RemoveButton @click="remove(u.username)">Удалить</RemoveButton>
      </li>
    </ul>
    <SecondaryText v-else-if="!loading">Черный список пуст</SecondaryText>

    <div class="add-row">
      <UserAutocomplete v-model="username" placeholder="Имя пользователя" />
      <Button
        type="button"
        :loading="adding"
        :disabled="!username.trim()"
        @click="submitAdd"
      >
        Добавить
      </Button>
    </div>
  </SettingsSection>
</template>

<style scoped lang="sass">
.bl-list
  list-style: none
  display: flex
  flex-direction: column
  gap: $tiny
  margin-bottom: $small

.bl-item
  display: flex
  align-items: center
  gap: $small

.add-row
  display: flex
  align-items: flex-start
  gap: $small
</style>
