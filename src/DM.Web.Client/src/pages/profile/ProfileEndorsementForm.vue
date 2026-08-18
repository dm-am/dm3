<script setup lang="ts">
/**
 * ProfileEndorsementForm — the screen that writes a recommendation about
 * another participant. Reached from the actions row of that participant's
 * profile ("Написать рекомендацию", beside "Написать сообщение"), and the
 * only way a recommendation is created at all: both listings of them existed
 * with nothing on the site able to add a row.
 *
 * Who may write one is the server's word and only the server's: the page asks
 * GET endorsements/eligibility, which is evaluated by the same code the POST
 * refuses by (signed in, not oneself, past probation, played in the same game,
 * no recommendation for this pair yet). A refusal is printed in the server's
 * own sentence instead of being paraphrased here — a second copy of those
 * rules on the client would drift from the first.
 *
 * The header, the 404 on an unknown owner and the document title are the
 * profile-subpage composable's, the same as the six list subpages next door.
 *
 * Plain text, not BBCode. A recommendation is stored and displayed verbatim
 * (UserEndorsementDtos.cs, TestimonialCard.vue), so an editor with a toolbar
 * would promise formatting that never renders — the same field the website
 * testimonial form uses is the honest one.
 */
import { computed, ref } from "vue";
import { useRouter } from "vue-router";
import { userApi, type Username } from "@/entities/user";
import { ErrorPage } from "@/shared/ui/ErrorPage";
import { ErrorState } from "@/shared/ui/ErrorState";
import TextArea from "@/shared/ui/TextArea/TextArea.vue";
import Button from "@/shared/ui/Button/Button.vue";
import { useFetchData } from "@/shared/lib/composables/useFetchData";
import { pluralize } from "@/shared/lib/utils/pluralize";
import { describeFailure } from "@/shared/lib/errors";
import ProfileSubpageHeader from "./ProfileSubpageHeader.vue";
import { useProfileSubpage } from "./useProfileSubpage";
import { useEndorsementEligibility } from "./useEndorsementEligibility";

/** Mirrors CreateUserEndorsementRequest on the server. */
const MIN_LENGTH = 10;
const MAX_LENGTH = 5000;

/** The noun agrees with the number, through the one plural rule. */
const lengthHint = `минимум ${MIN_LENGTH} ${pluralize(MIN_LENGTH, "символ", "символа", "символов")}`;

const router = useRouter();
const { username, canonicalUsername, notFound, profileLink } =
  useProfileSubpage("Написать рекомендацию");

// Asked again here rather than carried over from the profile: this URL is
// reachable directly, and a form that draws itself before the server has
// agreed is the offer the site must not make.
const {
  canCreate: canWrite,
  refusal,
  asking,
  askError,
  ask,
} = useEndorsementEligibility();

const text = ref("");
const submitting = ref(false);
const submitError = ref("");

const tooShort = computed(() => text.value.trim().length < MIN_LENGTH);

function checkEligibility() {
  return ask(username.value as Username);
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
  if (tooShort.value || submitting.value || !canWrite.value) return;
  submitting.value = true;
  submitError.value = "";
  const { error } = await userApi.createUserEndorsement(
    username.value as Username,
    { text: text.value.trim() },
  );
  submitting.value = false;
  if (error) {
    // Every refusal of this endpoint is a sentence of the server's own, and
    // it belongs under the field the text is still sitting in: a refusal must
    // cost nothing that was typed.
    submitError.value = describeFailure(
      error,
      "Не удалось отправить рекомендацию",
    );
    return;
  }
  // Where the recommendation now lives — the recipient's received list, with
  // the new entry at the top of it.
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

    <!-- Nothing is drawn while the server is still deciding: a form that
         appears and then disappears has already made the offer. -->
    <template v-else-if="!asking">
      <template v-if="canWrite">
        <form-field
          label="Текст рекомендации (обычный текст, без форматирования)"
          name="endorsementText"
          :errors="submitError ? [submitError] : []"
        >
          <template #hint
            >Чем игрок запомнился в совместной игре: {{ lengthHint }}</template
          >
          <text-area
            id="endorsementText"
            v-model="text"
            placeholder="Напишите, за что вы рекомендуете этого игрока..."
            :max-length="MAX_LENGTH"
            :disabled="submitting"
          />
        </form-field>

        <div class="form-actions">
          <Button :loading="submitting" :disabled="tooShort" @click="submit">
            Отправить рекомендацию
          </Button>
          <router-link :to="profileLink">Отмена</router-link>
        </div>
      </template>

      <!-- The server's own sentence, not a paraphrase of it. -->
      <secondary-text v-else class="refusal">
        {{ refusal || "Сейчас рекомендацию написать нельзя" }}
      </secondary-text>
    </template>
  </div>
</template>

<style scoped lang="sass">
@import "@/assets/styles/Inputs"

.endorsement-form-page
  width: 100%

.check-error
  margin-top: $medium

.form-actions
  display: flex
  align-items: center
  gap: $medium

.refusal
  display: block
  margin-top: $medium
</style>
