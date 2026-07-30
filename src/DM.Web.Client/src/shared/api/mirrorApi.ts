import Api from "./client";

/** One deployment of the website reachable under its own host. */
export interface Mirror {
  id: string;
  name: string;
  webUrl: string;
  isCurrent: boolean;
}

export interface MirrorList {
  currentMirrorId: string;
  mirrors: Mirror[];
}

/**
 * Mirror list (`GET /v1/mirrors`).
 *
 * A mirror is a deployment of this website, not a business concept: no slice
 * owns one, and the only consumer is the region switcher in the chrome. That is
 * why this call sits next to the transport instead of travelling with the
 * account client, where it used to live.
 */
export default new (class MirrorApi {
  public getMirrors() {
    return Api.get<MirrorList>("mirrors");
  }
})();
