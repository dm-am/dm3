import {
  SubscriptionSettings,
  SubscriptionTargetType,
  type Subscription,
} from "@/shared/api/models/subscriptions";

/**
 * A subscription as the server writes it.
 *
 * The API writes every enum by its name (JsonStringEnumConverter, set for the
 * whole API in JsonConfiguration): the target type as "Game", and the settings,
 * a set of flags, as the name of a preset ("BlogReaderDefault"), as names
 * joined by ", " ("InApp, AuthorGameEvents"), or as a number where a bit has no
 * name. The client's model holds numbers, because the rest of the client
 * computes with them: the page files a row under "Игры" by comparing the type
 * to SubscriptionTargetType.Game, and the subscribe button reads a checkbox
 * off a bit of the settings. Compared as they arrived, every row of the page
 * was of type "Неизвестно", every count was 0, no row had a link, and every
 * checkbox of the button stood empty.
 *
 * So the names are read into the numbers here, at the one door a subscription
 * comes in by. What the client sends goes out as numbers, which the same
 * converter accepts.
 *
 * The status of a game or a blog target (targetStatus) is a name here as well,
 * and stays one: the client's ModuleStatus is the union of those names, and
 * the page compares it as it came.
 */
export type WireSubscription = Omit<Subscription, "targetType" | "settings"> & {
  targetType: string | number;
  settings: string | number;
};

/** "Game" as SubscriptionTargetType.Game. */
export function targetTypeFromWire(
  value: string | number,
): SubscriptionTargetType {
  if (typeof value === "number") return value;
  const named =
    SubscriptionTargetType[value as keyof typeof SubscriptionTargetType];
  // A type this client does not know stays what it was: the page files it
  // under none of its tabs and names it "Неизвестно", which is the truth.
  return typeof named === "number"
    ? named
    : (value as unknown as SubscriptionTargetType);
}

/** "InApp, AuthorGameEvents" as InApp | AuthorGameEvents. */
export function settingsFromWire(value: string | number): SubscriptionSettings {
  if (typeof value === "number") return value;
  let bits = 0;
  for (const part of value.split(",")) {
    const name = part.trim();
    const named =
      SubscriptionSettings[name as keyof typeof SubscriptionSettings];
    if (typeof named === "number") bits |= named;
    // A combination with a bit that has no name is written as its number.
    else if (/^\d+$/.test(name)) bits |= Number(name);
  }
  return bits;
}

/** One subscription off the wire, in the client's model. */
export function subscriptionFromWire(wire: WireSubscription): Subscription {
  return {
    ...wire,
    targetType: targetTypeFromWire(wire.targetType),
    settings: settingsFromWire(wire.settings),
  };
}
