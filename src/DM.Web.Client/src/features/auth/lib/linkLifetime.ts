import { pluralize } from "@/shared/lib/utils/pluralize";

/**
 * The activation link's lifetime as the server sets it by default
 * (TokenConfiguration), said only where no answer has named the hours yet.
 * LinkLifetimeShould holds the number to the server's.
 */
const FALLBACK_LINK_LIFETIME_HOURS = 48;

/** The note of a receipt: how long the link in the letter lives, in the hours the answer named. */
export const linkLifetimeNote = (hours = FALLBACK_LINK_LIFETIME_HOURS) =>
  `Ссылка действительна ${hours} ${pluralize(hours, "час", "часа", "часов")}`;

/** The receipt of a resent activation letter, said alike wherever it is resent from. */
export const ACTIVATION_RESENT =
  "Мы отправили повторное письмо со ссылкой для активации на";
