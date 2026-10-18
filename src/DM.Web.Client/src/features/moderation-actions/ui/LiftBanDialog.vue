<script setup lang="ts">
/**
 * "Снятие бана": the window a senior moderator lifts a ban early in.
 *
 * A form with the one field the server requires, the reason. The ban list
 * writes the lift as an entry of its own, with the reason under "Примечание от
 * модератора:" (BanLogCard.vue), and a lift without words would be the only
 * entry in the list nobody explained. The line above the field says whose ban
 * goes and that it goes now, and the window is a question before it is a
 * form: the lift is not undone, and taking something away is always confirmed.
 *
 * The window runs the request (Dialog `action`): it waits shut, prints a
 * refusal over its buttons or under the field the server named, and closes on
 * success alone. The reason is typed text, so the window asks before it is
 * dropped. Its shape is the one ChangeWarningPointsDialog has.
 */
import { computed, ref } from "vue";
import Dialog from "@/shared/ui/Layout/Dialog.vue";
import DialogTitle from "@/shared/ui/Layout/DialogTitle.vue";
import Form from "@/shared/ui/Form/Form.vue";
import FormField from "@/shared/ui/Form/FormField.vue";
import { moderationApi } from "@/entities/moderation";

const props = defineProps<{
  /** The ban being lifted. */
  banId: string;
  /** Whose ban it is, for the line above the field. */
  username: string;
}>();

const emit = defineEmits<{
  (e: "success"): void;
  (e: "cancel"): void;
}>();

const reason = ref("");

/** The length the server keeps, the same as the explanation of a change of points. */
const REASON_MAX = 2000;

const reasonTooLong = computed(() => reason.value.length > REASON_MAX);
const canSubmit = computed(
  () => reason.value.trim().length > 0 && !reasonTooLong.value,
);

async function send() {
  const result = await moderationApi.liftBan(props.banId, reason.value.trim(), {
    ownsRefusal: true,
    ownsRateLimit: true,
  });
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
// One field step down to the field, as between two fields.
.lift-context
  margin: 0 0 $medium
</style>
