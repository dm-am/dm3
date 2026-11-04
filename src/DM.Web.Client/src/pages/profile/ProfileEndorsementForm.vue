<script setup lang="ts">
/**
 * Writing a recommendation about another participant, from the actions row of
 * his profile. Whether one may is the server's answer (endorsements/eligibility);
 * a "no" goes back to the profile. Plain text: a recommendation is shown verbatim.
 */
import { computed, ref } from "vue";
import { useRouter } from "vue-router";
import { userApi, type Username } from "@/entities/user";
import { ErrorPage } from "@/shared/ui/ErrorPage";
import { ErrorState } from "@/shared/ui/ErrorState";
import TextArea from "@/shared/ui/TextArea/TextArea.vue";
import Button from "@/shared/ui/Button/Button.vue";
import { useFetchData } from "@/shared/lib/composables/useFetchData";
import { describeFailureOrWait } from "@/shared/lib/errors";
import ProfileSubpageHeader from "./ProfileSubpageHeader.vue";
import { useProfileSubpage } from "./useProfileSubpage";
import { useEndorsementEligibility } from "./useEndorsementEligibility";
import { OWNS_REFUSAL } from "@/shared/api/interceptor";

/** UserEndorsementFieldLimits.TextMaxLength, compared by ClientFieldLimitsShould. */
const MAX_LENGTH = 5000;

const router = useRouter();
const { username, canonicalUsername, notFound, profileLink } =
  useProfileSubpage("Написать рекомендацию");

/**
 * Both failures are said on this page, under the field or in the strip above
 * it, the wait of a 429 included; an expired session and a dead connection stay
 * the interceptor's.
 */

// Asked again: the address is reachable directly, and the form waits for the server's yes.
const {
  canCreate: canWrite,
  asking,
  askError,
  ask,
} = useEndorsementEligibility(OWNS_REFUSAL);

const text = ref("");
const submitting = ref(false);
const submitError = ref("");

const isBlank = computed(() => !text.value.trim());

async function checkEligibility() {
  await ask(username.value as Username);
  // A failed question stays here with a retry; a "no" replaces this address with the profile.
  if (!askError.value && !canWrite.value) router.replace(profileLink.value);
}

useFetchData(
  () => checkEligibility(),
  [
    {
      param: (p) => p.username,
      callback: () => {
        text.value = "";
        submitError.value = "";
        checkEligibility();
      },
    },
  ],
);

async function submit() {
  submitting.value = true;
  submitError.value = "";
  const { error } = await userApi.createUserEndorsement(
    username.value as Username,
    { text: text.value },
    OWNS_REFUSAL,
  );
  submitting.value = false;
  if (error) {
    // The server's own sentence, under the field the text is still in; a wait in the
    // shared wording, the only one that names the seconds.
    submitError.value = describeFailureOrWait(
      error,
      "Не удалось отправить рекомендацию",
    );
    return;
  }
  // The recipient's received list, the new entry first.
  router.push({
    name: "received-endorsements",
    params: { username: username.value },
  });
}
</script>

<template>
  <ErrorPage v-if="notFound" :code="404" />
  <div v-else class="endorsement-form-page">
    <ProfileSubpageHeader
      label="Написать рекомендацию"
      :username="canonicalUsername"
    >
      Что вы можете сказать об игроке
      <router-link :to="profileLink">{{ canonicalUsername }}</router-link
      >. Одна рекомендация на адресата, править ее можно сутки после отправки.
    </ProfileSubpageHeader>

    <ErrorState
      v-if="askError"
      class="check-error"
      :message="askError"
      :retry="checkEligibility"
    />

    <!-- Nothing while the server decides: a form that appears and goes has made the offer. -->
    <template v-else-if="!asking && canWrite">
      <form-field
        label="Текст рекомендации (обычный текст, без форматирования)"
        name="endorsementText"
        :errors="submitError ? [submitError] : []"
      >
        <template #hint>Чем игрок запомнился в совместной игре</template>
        <text-area
          id="endorsementText"
          v-model="text"
          placeholder="Напишите, за что вы рекомендуете этого игрока..."
          :max-length="MAX_LENGTH"
          :disabled="submitting"
        />
      </form-field>

      <div class="form-actions">
        <Button
          variant="primary"
          :loading="submitting"
          :disabled="isBlank"
          @click="submit"
        >
          Отправить рекомендацию
        </Button>
        <router-link :to="profileLink">Отмена</router-link>
      </div>
    </template>
  </div>
</template>

<style scoped lang="sass">

.endorsement-form-page
  width: 100%

.check-error
  margin-top: $medium

.form-actions
  display: flex
  align-items: center
  gap: $medium
</style>
