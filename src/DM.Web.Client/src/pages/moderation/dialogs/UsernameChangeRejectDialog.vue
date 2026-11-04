<script setup lang="ts">
/**
 * Rejects a username change with a required reason. The window runs the
 * request; the typed reason is asked about before it is dropped.
 */
import { ref, computed } from "vue";
import {
  moderationApi,
  UsernameChangeRequestStatus,
  type UsernameChangeRequest,
} from "@/entities/moderation";
import Dialog from "@/shared/ui/Layout/Dialog.vue";
import DialogTitle from "@/shared/ui/Layout/DialogTitle.vue";
import Form from "@/shared/ui/Form/Form.vue";
import FormField from "@/shared/ui/Form/FormField.vue";
import { OWNS_REFUSAL } from "@/shared/api/interceptor";

const props = defineProps<{
  request: UsernameChangeRequest;
}>();

const emit = defineEmits<{
  (e: "success", request: UsernameChangeRequest): void;
  (e: "cancel"): void;
}>();

const comment = ref("");

const canSubmit = computed(() => comment.value.trim().length > 0);

async function send() {
  const result = await moderationApi.resolveUsernameChangeRequest(
    props.request.id,
    {
      status: UsernameChangeRequestStatus.Rejected,
      comment: comment.value,
    },
    OWNS_REFUSAL,
  );
  if (!result.error) emit("success", props.request);
  return result;
}
</script>

<template>
  <Dialog
    :action="send"
    failure="Не удалось отклонить запрос"
    :dirty="comment.trim().length > 0"
    discard-text="Набранная причина пропадет"
    @close="emit('cancel')"
  >
    <DialogTitle>Отклонение запроса</DialogTitle>

    <p class="reject-context">
      Отклонить запрос на смену имени от
      <strong>{{ request.currentUsername }}</strong
      >?
    </p>

    <Form :valid="canSubmit" action="Отклонить" cancel="Отмена">
      <FormField
        label="Причина отклонения"
        name="reject-reason"
        field="comment"
      >
        <textarea
          id="reject-reason"
          v-model="comment"
          placeholder="Укажите причину отклонения..."
          rows="4"
        ></textarea>
      </FormField>
    </Form>
  </Dialog>
</template>

<style scoped lang="sass">
// One field step down to the field, as between two fields.
.reject-context
  margin: 0 0 $medium
</style>
