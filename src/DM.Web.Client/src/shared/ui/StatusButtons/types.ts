/**
 * One offered status transition: the value the caller's store sends, the words
 * on the control, and whether it is destructive enough to ask first.
 *
 * The blog and the game each build this list from their own state machine
 * (features/*-actions/model/transitions.ts) — the shapes agree, the contents
 * do not, and this type is only the agreement.
 */
export interface StatusTransitionOption<T extends string = string> {
  value: T;
  label: string;
  /** Destructive / significant transition — worth a confirmation prompt. */
  danger?: boolean;
}
