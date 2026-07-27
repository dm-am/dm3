export { default as UserLink } from "./UserLink.vue";
export { default as UserRating } from "./UserRating.vue";
export { default as UserAutocomplete } from "./UserAutocomplete.vue";
export { default as UserMultiSelect } from "./UserMultiSelect.vue";
// AvatarImg lives in shared/ui (used by shared components too); re-exported
// here so user-related consumers keep importing it from the entity barrel.
export { AvatarImg } from "@/shared/ui/AvatarImg";
