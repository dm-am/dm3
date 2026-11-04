<script setup lang="ts">
// One card for the open log and the moderation list, so a warning is worded in one place.
// The text is the copy the server took at issue, not the content as edited since.
import { computed } from "vue";
import type { UserRef } from "@/shared/api/models/common";
import { UserLink } from "@/entities/user/@x/moderation";
import { AvatarImg } from "@/shared/ui/AvatarImg";
import { CaptionPart } from "@/shared/ui/CaptionLine";
import { TruncatedContent } from "@/shared/ui/TruncatedContent";
import { RefusalNote } from "@/shared/ui/Layout";
import { formatDateFull } from "@/shared/lib/utils/datetime";
import { vBbcodeInteractive } from "@/shared/directives";
import { trimHtmlWhitespace } from "@/shared/lib/utils/trimHtmlWhitespace";
import type { WarningRecord } from "../api/warningApi";
import {
  placeLabel,
  pointsChangeLabel,
  warningKindLabel,
} from "../lib/warningLabels";
import RecordBlock from "./RecordBlock.vue";
import RecordVerdict from "./RecordVerdict.vue";
import { COMMENT_MAX_HEIGHT } from "@/shared/lib/constants/content";

// What the open log entry and the moderation row both satisfy.
type WarningCardModel = WarningRecord & {
  user?: UserRef;
  moderator?: UserRef;
  entityType?: string | null;
  accessRestricted?: boolean | null;
};

const props = defineProps<{ warning: WarningCardModel }>();

const evidenceHtml = computed(() =>
  trimHtmlWhitespace(props.warning.evidence ?? ""),
);

const onProfile = computed(() => props.warning.entityType === "Profile");

// Issued on a ticket about a person: the log sends no kind, the moderation row "Unknown".
const namesNothing = computed(
  () =>
    !onProfile.value &&
    !props.warning.accessRestricted &&
    (!props.warning.entityType ||
      props.warning.entityType === "Unknown" ||
      (!props.warning.placeKind && !props.warning.evidence)),
);

const snapshot = computed(() => {
  const profile = onProfile.value ? props.warning.profile : null;
  return profile && (profile.picture || profile.status || profile.info)
    ? profile
    : null;
});

const aboutHtml = computed(() =>
  trimHtmlWhitespace(snapshot.value?.info ?? ""),
);

const place = computed(() =>
  onProfile.value
    ? "Профиль пользователя"
    : placeLabel(props.warning.placeKind, props.warning.placeTitle),
);

const placeTo = computed(() => {
  if (onProfile.value) {
    return props.warning.user
      ? { name: "profile", params: { username: props.warning.user.username } }
      : null;
  }
  return props.warning.entityUrl ?? null;
});

const createdDate = computed(() =>
  formatDateFull(props.warning.entityCreatedUtc),
);

// Nothing is green: a warning is never good.
const toneOf = (points: number) => (points > 0 ? "negative" : "neutral");
</script>

<template>
  <article class="warning-card">
    <RecordBlock>
      <!-- Conditional, so a row with neither has no empty text line. -->
      <template
        v-if="warning.accessRestricted || warning.evidence || snapshot"
        #default
      >
        <!-- A place the reader does not read, or none found: one block for every such place. -->
        <RefusalNote v-if="warning.accessRestricted" class="warning-restricted"
          >Доступ ограничен</RefusalNote
        >
        <TruncatedContent
          v-else-if="warning.evidence"
          :truncatable="true"
          :max-height="COMMENT_MAX_HEIGHT"
          :watch-key="evidenceHtml"
        >
          <div
            v-bbcode-interactive="evidenceHtml"
            class="warning-evidence bbcode-content"
            v-html="evidenceHtml"
          />
        </TruncatedContent>
        <!-- The parts of ProfilePage.vue in its order; no silhouette without an avatar. -->
        <div v-else-if="snapshot" class="warning-profile">
          <AvatarImg
            v-if="snapshot.picture"
            :picture="snapshot.picture"
            :alt="warning.user?.username ?? ''"
            :size="220"
            img-class="warning-profile__picture"
            prefer-original
            no-default
          />
          <div v-if="snapshot.status" class="warning-profile__status">
            Статус:{{ " "
            }}<span class="warning-profile__status-value">{{
              snapshot.status
            }}</span>
          </div>
          <div v-if="snapshot.info" class="warning-profile__info">
            <h3 class="warning-profile__about-label">О себе</h3>
            <TruncatedContent
              :truncatable="true"
              :max-height="COMMENT_MAX_HEIGHT"
              :watch-key="aboutHtml"
            >
              <div
                v-bbcode-interactive="aboutHtml"
                class="warning-profile__about bbcode-content"
                v-html="aboutHtml"
              />
            </TruncatedContent>
          </div>
        </div>
      </template>
      <template #caption>
        <span v-if="namesNothing" class="warning-user"
          >Пользователь: <UserLink v-if="warning.user" :user="warning.user"
        /></span>
        <template v-else>
          <CaptionPart v-if="place" joint="," class="warning-place"
            ><router-link v-if="placeTo" :to="placeTo">{{ place }}</router-link
            ><template v-else>{{ place }}</template></CaptionPart
          >
          <CaptionPart
            v-if="warning.user"
            :joint="warning.entityCreatedUtc ? ',' : undefined"
            class="warning-author"
            >Автор: <UserLink :user="warning.user"
          /></CaptionPart>
          <span v-if="warning.entityCreatedUtc" class="warning-date">{{
            createdDate
          }}</span>
        </template>
        <slot name="record-controls" />
      </template>
    </RecordBlock>

    <ul class="record-verdict-list">
      <RecordVerdict
        class="warning-verdict"
        :kind="warningKindLabel(warning.originalPoints)"
        :tone="toneOf(warning.originalPoints)"
        :author="warning.moderator?.username"
        :date="warning.createdUtc"
      >
        <span class="warning-reason bbcode-content" v-html="warning.reason" />
        <template #controls><slot name="controls" /></template>
      </RecordVerdict>

      <RecordVerdict
        v-if="warning.pointsChangedUtc"
        class="warning-change"
        :kind="pointsChangeLabel(warning.points)"
        :tone="toneOf(warning.points)"
        :author="warning.pointsChangedBy"
        :date="warning.pointsChangedUtc"
      >
        <span v-if="warning.pointsChangeText" class="warning-change-text">{{
          warning.pointsChangeText
        }}</span>
      </RecordVerdict>
    </ul>
  </article>
</template>

<style scoped lang="sass">
@use "@/assets/styles/ReviewParts" as *

// Drawn as the reviews under a post are.
.record-verdict-list
  +review-list

// ProfilePage.vue, .col-identity.
.warning-profile
  display: flex
  flex-direction: column
  align-items: flex-start

.warning-profile__picture
  display: block
  width: 220px
  max-width: 100%
  height: auto
  border: 1px solid $border
  background-color: $bg-element

  & + *
    margin-top: $medium

.warning-profile__status
  font-size: $font-size
  line-height: 1.5
  color: $text
  text-align: left

.warning-profile__status-value
  overflow-wrap: anywhere

.warning-profile__info
  align-self: stretch

  &:not(:first-child)
    margin-top: $medium + $small

// As the profile heads it, Tabs.vue .tabs--headings.
.warning-profile__about-label
  margin: 0 0 $medium
  font-size: $title-font-size
  font-weight: bold
  line-height: normal
  text-transform: uppercase
  letter-spacing: 0.5px
  color: $heading
  text-align: left
</style>
