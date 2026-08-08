/**
 * Server-side fetch wrapper for the .NET API.
 *
 * All calls happen in Server Components, so the base URL can be an internal
 * address and no key or origin is ever exposed to the browser.
 */

export const API_BASE_URL = process.env.API_BASE_URL?.replace(/\/$/, '') ?? '';

/** When unset, the app runs entirely on the local fixture set. */
export const USE_API = API_BASE_URL.length > 0;

export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly path: string,
    message: string
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

export interface ApiOptions {
  locale: string;
  /** Seconds. Scores change on a scoring run, not continuously. */
  revalidate?: number;
  query?: Record<string, string | number | boolean | undefined>;
}

export async function apiGet<T>(path: string, options: ApiOptions): Promise<T> {
  const url = new URL(`${API_BASE_URL}${path}`);

  // Locale travels explicitly rather than relying on a forwarded cookie: the
  // server already knows it from the route segment, and the API's resolution
  // order puts ?lang first.
  url.searchParams.set('lang', options.locale);

  for (const [key, value] of Object.entries(options.query ?? {})) {
    if (value !== undefined) url.searchParams.set(key, String(value));
  }

  const response = await fetch(url, {
    headers: { Accept: 'application/json', 'Accept-Language': options.locale },
    next: { revalidate: options.revalidate ?? 60 },
  });

  if (!response.ok) {
    throw new ApiError(
      response.status,
      path,
      `GET ${path} failed with ${response.status} ${response.statusText}`
    );
  }

  return (await response.json()) as T;
}
