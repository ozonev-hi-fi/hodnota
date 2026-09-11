import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { stubFetchWithRelativeUrls } from './relativeUrlFetchStub.ts';

// Same reasoning as client.test.ts: apiClient captures globalThis.fetch at module-load time, and
// Node's own Request/fetch (unlike a browser) has no implicit origin for a relative URL — this
// wrapper only resolves that gap for the test run.
const fetchMock = vi.fn();

async function loadIdentity() {
  vi.resetModules();
  return import('./identity.ts');
}

beforeEach(() => {
  stubFetchWithRelativeUrls(fetchMock);
  fetchMock.mockReset();
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('confirmEmail', () => {
  // Regression test: this endpoint's real success response is plain text ("Thank you for
  // confirming your email."), not JSON. openapi-fetch defaults every response to JSON parsing,
  // which previously threw a raw SyntaxError here instead of succeeding — and that raw error
  // leaked onto the ConfirmEmailPage UI verbatim.
  it('succeeds against a real plain-text response, not just a JSON one', async () => {
    fetchMock.mockResolvedValue(
      new Response('Thank you for confirming your email.', {
        status: 200,
        headers: { 'Content-Type': 'text/plain' },
      }),
    );
    const { confirmEmail } = await loadIdentity();

    await expect(confirmEmail('user-1', 'code-1')).resolves.toBeUndefined();
  });

  it('forwards changedEmail as a query param when confirming an email change', async () => {
    fetchMock.mockResolvedValue(
      new Response('Thank you for confirming your email.', {
        status: 200,
        headers: { 'Content-Type': 'text/plain' },
      }),
    );
    const { confirmEmail } = await loadIdentity();

    await confirmEmail('user-1', 'code-1', 'new@example.com');

    const request = fetchMock.mock.calls[0][0] as Request;
    expect(new URL(request.url).searchParams.get('changedEmail')).toBe(
      'new@example.com',
    );
  });
});
