// Every named route declares one of title, section and dynamicTitle in its own
// meta: an inherited title names every child alike (routeTitles.spec.ts).
// export {} keeps this an augmentation of vue-router, not a replacement.
declare module "vue-router" {
  interface RouteMeta {
    requiresAuth?: boolean;
    /** The brand suffix is appended in afterEach. */
    title?: string;
    /**
     * Section inside a zone: the zone shell composes "{сущность} | {секция}",
     * entity first, since it tells two truncated tabs apart.
     */
    section?: string;
    /** Composed by the page or the zone shell out of fetched data. */
    dynamicTitle?: true;
    /** The left sidebar mounts the panel of the game, blog or moderation. */
    gameZone?: boolean;
    blogZone?: boolean;
    moderationZone?: boolean;
    /**
     * A page a letter leads to: the shell drops both sidebars, and the guest
     * card of its one action stands alone.
     */
    fromLetter?: boolean;
  }
}

export {};
