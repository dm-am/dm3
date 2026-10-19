<script setup lang="ts">
// Suggests everyone, the long absent too: the troll who stays away is the one a blacklist is for.
import { ref } from "vue";
import { UserAutocomplete } from "@/entities/user";
import type { GeneralError } from "@/shared/api/models/common";
import { useActionResult } from "@/shared/lib/composables/useActionResult";
import Button from "@/shared/ui/Button/Button.vue";
import { ActionResult } from "@/shared/ui/ActionResult";

const props = defineProps<{
  add: (username: string) => Promise<GeneralError | null>;
  /** Names the field does not suggest. */
  exclude?: readonly string[];
}>();

const username = ref("");
const adding = ref(false);

const addResult = useActionResult();

async function submitAdd() {
  const value = username.value.trim();
  if (!value || adding.value) return;
  adding.value = true;
  addResult.clear();
  const error = await props.add(value);
  adding.value = false;
  if (error) {
    addResult.refuse(error, "Пользователь не добавлен в черный список");
    return;
  }
  username.value = "";
}
</script>

<template>
  <div class="bl-add">
    <div class="bl-add-row">
      <UserAutocomplete
        v-model="username"
        placeholder="Имя пользователя"
        :exclude="exclude"
        include-inactive
        @update:model-value="addResult.clear()"
      />
      <Button
        type="button"
        variant="primary"
        :loading="adding"
        :disabled="!username.trim() || addResult.waiting.value"
        @click="submitAdd"
      >
        Добавить
      </Button>
    </div>
    <ActionResult :result="addResult" />
  </div>
</template>

<style scoped lang="sass">
.bl-add-row
  display: flex
  align-items: flex-start
  gap: $small
</style>
