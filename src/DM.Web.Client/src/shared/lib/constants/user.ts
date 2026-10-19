/**
 * How long after the last visit a user is shown online: a copy of
 * ActivityPolicy.OnlinePeriod for the dot the browser recomputes on its own
 * clock. The spec next to this file holds the two equal.
 */
export const ONLINE_THRESHOLD_MINUTES = 5;
export const ONLINE_THRESHOLD_MS = ONLINE_THRESHOLD_MINUTES * 60 * 1000;

/**
 * Game posts a user needs before they stop being a newbie: a copy of
 * ProbationPolicy.NewbiePostThreshold for the hints of the users filter, which
 * the API does not serve. The spec next to this file holds the two equal.
 */
export const NEWBIE_POST_THRESHOLD = 100;

/**
 * The bounds of a username (UsernamePolicy.cs), a password (PasswordPolicyConfiguration.cs)
 * and a reason for a username change (CreateUsernameChangeRequestValidator.cs).
 * ClientFieldLimitsShould compares the numbers.
 */
export const USERNAME_MIN_LENGTH = 2;
export const USERNAME_MAX_LENGTH = 20;
export const PASSWORD_MIN_LENGTH = 8;
export const USERNAME_CHANGE_REASON_MAX_LENGTH = 500;
