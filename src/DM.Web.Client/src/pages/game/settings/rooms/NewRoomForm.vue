<script setup lang="ts">
// DM2's form of a new room; its settings are set in its row of the table.
import { reactive, ref } from "vue";
import {
  useGameDetailsStore,
  RoomType,
  RoomAccessType,
  type CreateRoomInput,
} from "@/entities/game";
import { Form, FormField } from "@/shared/ui/Form";
import { useActionResult } from "@/shared/lib/composables/useActionResult";
import { ROOM_TITLE_MAX_LENGTH } from "@/shared/lib/constants/game";

const store = useGameDetailsStore();

const create = reactive({
  title: "",
  type: RoomType.Default,
  // Closed, as DM2 starts a new room.
  accessType: RoomAccessType.Private,
});
const creating = ref(false);

const createResult = useActionResult();

async function createRoom() {
  if (!create.title.trim()) return;
  creating.value = true;
  createResult.clear();
  const payload: CreateRoomInput = {
    title: create.title,
    type: create.type,
    accessType: create.accessType,
    viewPrivateText: false,
    viewDiceResults: false,
    diceEnabled: false,
  };
  const error = await store.createRoom(payload);
  creating.value = false;
  if (error) {
    createResult.refuse(error, "Не удалось создать комнату");
    return;
  }
  // The new row in the table and the cleared name are the answer.
  create.title = "";
}
</script>

<template>
  <Form
    class="new-room-form"
    :valid="create.title.trim().length > 0"
    :loading="creating"
    action="Создать"
    :refusal="createResult.refusal.value"
    @submit="createRoom"
  >
    <FormField class="row" label="Название:" name="new-room-title">
      <input
        id="new-room-title"
        v-model="create.title"
        type="text"
        :maxlength="ROOM_TITLE_MAX_LENGTH"
      />
    </FormField>
    <FormField class="row" label="Тип:">
      <div class="choices">
        <label class="choice">
          <input
            v-model="create.type"
            type="radio"
            name="new-room-type"
            :value="RoomType.Default"
          />
          Посты
        </label>
        <label class="choice">
          <input
            v-model="create.type"
            type="radio"
            name="new-room-type"
            :value="RoomType.Chat"
          />
          Чат
        </label>
      </div>
    </FormField>
    <FormField class="row" label="Доступ:">
      <div class="choices">
        <label class="choice">
          <input
            v-model="create.accessType"
            type="radio"
            name="new-room-access"
            :value="RoomAccessType.Private"
          />
          Закрытая
        </label>
        <label class="choice">
          <input
            v-model="create.accessType"
            type="radio"
            name="new-room-access"
            :value="RoomAccessType.Open"
          />
          Открытая
        </label>
      </div>
    </FormField>
  </Form>
</template>

<style scoped lang="sass">
@use "@/assets/styles/FormRows" as *

// The class of the form outweighs FormField's own layout of its root.
.new-room-form .row
  +form-row

.choices
  display: flex
  flex-wrap: wrap
  align-items: center
  gap: $small $medium
  min-height: $control-height

.choice
  +choice
</style>
