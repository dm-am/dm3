<script setup lang="ts">
/**
 * ChatEventCard — DEV-ONLY: the body every disclosure variant opens, written
 * once so the catalog compares the way a card is revealed instead of three
 * hand-written copies of the card.
 *
 * The head is inline flow, so it copies as one line: "Название, идет, до
 * 22:48, участников: 12, организатор: X". The description arrives as rendered
 * HTML and goes through the same content component the site uses everywhere.
 */
import { computed } from "vue";
import { ContentText } from "@/shared/ui/Content";
import { DETAILS, STATE_WORD, type MockEvent } from "./chatEventsMock";

const props = defineProps<{
  event: MockEvent;
}>();

/** "Astrellan, Miriamel, GrayWanderer и еще 9" — the list plus the remainder. */
const people = computed(() => {
  const shown = DETAILS.participants.join(", ");
  const rest = props.event.participantCount - DETAILS.participants.length;
  return rest > 0 ? `${shown} и еще ${rest}` : shown;
});
</script>

<template>
  <div class="card">
    <div class="card-head">
      <span class="card-title">{{ event.title }}</span
      ><span class="card-meta"
        >, {{ STATE_WORD[event.status].toLowerCase() }}, {{ event.timeText
        }}<template v-if="!event.isOpen">, закрытый</template>, организатор:
        <router-link
          class="card-organizer"
          :to="{ name: 'profile', params: { username: DETAILS.organizer } }"
          >{{ DETAILS.organizer }}</router-link
        ></span
      >
    </div>
    <div class="card-people">Участники: {{ people }}</div>
    <ContentText class="card-description" :html="DETAILS.description" />
  </div>
</template>

<style scoped lang="sass">
@import "@/assets/styles/Inputs"

.card-head
  line-height: 1.4

.card-title
  color: $text
  font-weight: 600

.card-meta
  color: $text-muted

.card-organizer
  +muted-link

.card-people
  margin-top: $tiny
  color: $text-muted

// Line height comes from the global .bbcode-content on the same element.
.card-description
  margin-top: $small
</style>
