import { vi } from 'vitest';

// client.ts/identity.ts deliberately use relative URLs ("/api/...") since a real browser always
// has a page origin to resolve them against (the Vite dev proxy, or same-origin production
// hosting) — but Node's own Request/fetch (unlike a browser) has no implicit origin and throws on
// a relative URL. This wrapper only resolves that gap for a test run; it doesn't change the
// modules under test.
const RealRequest = globalThis.Request;

class RelativeUrlRequest extends RealRequest {
  constructor(input: RequestInfo | URL, init?: RequestInit) {
    super(
      typeof input === 'string' && input.startsWith('/')
        ? `http://localhost${input}`
        : input,
      init,
    );
  }
}

export function stubFetchWithRelativeUrls(fetchMock: typeof fetch) {
  vi.stubGlobal('fetch', fetchMock);
  vi.stubGlobal('Request', RelativeUrlRequest);
}
