<script setup lang="ts">
import { ref, onMounted, watch } from "vue";
import {
  gameApi,
  useGameDisplay,
  GameStatusBadge,
  type Game,
} from "@/entities/game";
import { Tooltip } from "@/shared/ui/Tooltip";

const props = defineProps<{
  username: string;
}>();

const { buildTooltip } = useGameDisplay();

const masterGames = ref<Game[]>([]);
const playerGames = ref<Game[]>([]);
const loading = ref(true);

async function fetchGames() {
  loading.value = true;

  const [masterResult, playerResult] = await Promise.all([
    gameApi.getGamesByMaster(props.username),
    gameApi.getGamesByPlayer(props.username),
  ]);

  masterGames.value = masterResult.data?.resources || [];
  playerGames.value = playerResult.data?.resources || [];
  loading.value = false;
}

onMounted(fetchGames);
watch(() => props.username, fetchGames);

const hasGames = ref(false);
watch([masterGames, playerGames], () => {
  hasGames.value = masterGames.value.length > 0 || playerGames.value.length > 0;
});
</script>

<template>
  <section class="profile-games">
    <h3 class="section-title">Игры</h3>

    <div v-if="loading" class="loading">Загрузка...</div>

    <template v-else-if="hasGames">
      <div v-if="masterGames.length" class="games-group">
        <h4 class="group-title">Как мастер</h4>
        <div class="games-list">
          <Tooltip
            v-for="game in masterGames"
            :key="game.id"
            :text="buildTooltip(game)"
          >
            <router-link
              :to="{ name: 'game', params: { id: game.publicId || game.id } }"
              class="game-link"
            >
              <span class="game-title">{{ game.title }}</span>
              <GameStatusBadge
                :status="game.status"
                :is-recruiting="game.recruitment?.isOpen"
                :is-subsequent="game.recruitment?.isSubsequent"
                :closed-reason="game.closedReason"
                class="game-status"
                :class="game.status.toLowerCase()"
              />
            </router-link>
          </Tooltip>
        </div>
      </div>

      <div v-if="playerGames.length" class="games-group">
        <h4 class="group-title">Как игрок</h4>
        <div class="games-list">
          <Tooltip
            v-for="game in playerGames"
            :key="game.id"
            :text="buildTooltip(game)"
          >
            <router-link
              :to="{ name: 'game', params: { id: game.publicId || game.id } }"
              class="game-link"
            >
              <span class="game-title">{{ game.title }}</span>
              <GameStatusBadge
                :status="game.status"
                :is-recruiting="game.recruitment?.isOpen"
                :is-subsequent="game.recruitment?.isSubsequent"
                :closed-reason="game.closedReason"
                class="game-status"
                :class="game.status.toLowerCase()"
              />
            </router-link>
          </Tooltip>
        </div>
      </div>
    </template>

    <div v-else class="no-games">Пользователь не участвует ни в одной игре</div>
  </section>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.profile-games
  background: $bg-element
  border-radius: $border-radius
  padding: $medium
  margin-bottom: $medium

.section-title
  color: $text
  margin: 0 0 $medium
  font-size: 1rem

.loading
  color: $text-muted
  font-size: $secondary-font-size

.games-group
  margin-bottom: $medium

  &:last-child
    margin-bottom: 0

.group-title
  color: $text-muted
  font-size: $secondary-font-size
  margin: 0 0 $small
  text-transform: uppercase

.games-list
  display: flex
  flex-direction: column
  gap: $tiny

.game-link
  display: flex
  justify-content: space-between
  align-items: center
  padding: $small
  background: $bg-element-overlay
  border-radius: $border-radius
  text-decoration: none
  transition: background 0.2s

  &:hover
    background: $bg-element-hover

.game-title
  color: $link
  font-weight: 500

.game-status
  font-size: $secondary-font-size
  padding: $tiny $small
  border-radius: $border-radius

  &.active
    color: $accent-green
    background: rgba($accent-green, 0.1)

  &.draft
    color: $heading
    background: rgba($heading, 0.1)

  &.closed
    color: $text-muted
    background: $bg-element

.no-games
  color: $text-meta
  font-style: italic
</style>
