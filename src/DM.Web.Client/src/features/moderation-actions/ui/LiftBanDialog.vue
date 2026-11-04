<script setup lang="ts">
// The reason is required: the ban list shows a lift as an entry of its own, explained.
import { computed, ref } from "vue";
import Dialog from "@/shared/ui/Layout/Dialog.vue";
import DialogTitle from "@/shared/ui/Layout/DialogTitle.vue";
import Form from "@/shared/ui/Form/Form.vue";
import FormField from "@/shared/ui/Form/FormField.vue";
import { banApi } from "@/entities/moderation";
import { OWNS_REFUSAL } from "@/shared/api/interceptor";
import { REASON_MAX_LENGTH } from "../lib/limits";

const props = defineProps<{
  banId: string;
  username: string;
}>();

const emit = defineEmits<{
  (e: "success"): void;
  (e: "cancel"): void;
}>();

const reason = ref("");

const reasonTooLong = computed(() => reason.value.length > REASON_MAX_LENGTH);
const canSubmit = computed(
  () => reason.value.trim().length > 0 && !reasonTooLong.value,
);

async function send() {
  const result = await banApi.liftBan(props.banId, reason.value, OWNS_REFUSAL);
  if (!result.error) emit("success");
  return result;
}
</script>

<template>
  <Dialog
    :action="send"
    failure="Не удалось снять бан"
    :dirty="reason.trim().length > 0"
    discard-text="Набранная причина пропадет"
    @close="emit('cancel')"
  >
    <DialogTitle>Снятие бана</DialogTitle>

    <p class="lift-context">
      Бан <strong>{{ username }}</strong> снимется сразу, не дожидаясь срока
    </p>

    <Form :valid="canSubmit" action="Снять" cancel="Отмена">
      <FormField
        label="Причина снятия"
        name="lift-reason"
        field="reason"
        :errors="reasonTooLong ? ['Слишком длинное значение'] : []"
      >
        <textarea id="lift-reason" v-model="reason" rows="4"></textarea>
      </FormField>
    </Form>
  </Dialog>
</template>

<style scoped lang="sass">
.lift-context
  margin: 0 0 $medium
</style>
