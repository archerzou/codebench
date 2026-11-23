interface ImportMetaEnv {
  /** Base URL of the React app, e.g. http://app.codebench.localhost (public by design). */
  readonly PUBLIC_APP_URL?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
