<script setup lang="ts">
/**
 * BlacklistAddRow — the way a name is put on every blacklist of the site: a
 * field for the username and "Добавить" beside it, the last line of the table
 * of the list (BlacklistEditor), and not a window. It is the row "Управление
 * ролями" invites an assistant with: the field of the site's other name
 * fields, the main button, a step of 8 between them.
 *
 * `add` sends the request and resolves to its failure, or null once the list
 * holds the name. "Добавить" is hatched until a name is chosen from the
 * suggestions, while the request is on the wire and while a rate limit counts
 * down. A refusal stands under the row with the chosen name kept in the field,
 * and the plate the interceptor held for it is taken down (useActionResult); a
 * success empties the field.
 *
 * The field does not suggest the names in `exclude` (the server would refuse
 * them) and looks among everyone, the people who have not come by for a month
 * too: the troll who stays away is the one a blacklist is for.
 */
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

/** A refused "Добавить", under the field the name was chosen in. */
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
// The invite row of "Управление ролями": field and button on one line, a step
// of 8 apart, their tops level.
.bl-add-row
  display: flex
  align-items: flex-start
  gap: $small
</style>
